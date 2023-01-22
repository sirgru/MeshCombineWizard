using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.IO;

public class MeshCombineWizard : ScriptableWizard
{
	public GameObject combineParent;
	public string resultPath = "";
	public bool is32bit = true;
	public bool generateSecondaryUVs = false;

	[MenuItem("Ennoble Tools/Mesh Combine Wizard")]
	static void CreateWizard()
	{
		var wizard = DisplayWizard<MeshCombineWizard>("Mesh Combine Wizard");

		// If there is selection, and the selection of one Scene object, auto-assign it
		var selectionObjects = Selection.objects;
		if (selectionObjects != null && selectionObjects.Length == 1) {
			var firstSelection = selectionObjects[0] as GameObject;
			if (firstSelection != null) {
				wizard.combineParent = firstSelection;
			}
		}
	}

	void OnWizardCreate()
	{
		// Verify there is existing object root, ptherwise bail.
		if (combineParent == null) {
			Debug.LogError("Mesh Combine Wizard: Parent of objects to combne not assigned. Operation cancelled.");
			return;
		}

		var assetFolderResultPath = Path.Combine("Assets/", resultPath ?? "");
		if (!Directory.Exists(assetFolderResultPath)) {
			Debug.LogError("Mesh Combine Wizard: 'Result Path' does not exist. Specified path must exist as relative to Assets folder. Operation cancelled.");
			return;
		}

		// Remember the original position of the object. 
		// For the operation to work, the position must be temporarily set to (0,0,0).
		Vector3 originalPosition = combineParent.transform.position;
		combineParent.transform.position = Vector3.zero;

		// Locals
		Dictionary<Material, List<MeshFilter>> materialToMeshFilterList = new Dictionary<Material, List<MeshFilter>>();
		List<GameObject> combinedObjects = new List<GameObject>();

		MeshFilter[] meshFilters = combineParent.GetComponentsInChildren<MeshFilter>();

		// Go through all mesh filters and establish the mapping between the materials and all mesh filters using it.
		foreach (var meshFilter in meshFilters) {
			var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
			if (meshRenderer == null) {
				Debug.LogWarning("Mesh Combine Wizard: The Mesh Filter on object " + meshFilter.name + " has no Mesh Renderer component attached. Skipping.");
				continue;
			}
			
			var materials = meshRenderer.sharedMaterials;
			if (materials == null) {
				Debug.LogWarning("Mesh Combine Wizard: The Mesh Renderer on object " + meshFilter.name + " has no material assigned. Skipping.");
				continue;
			}

			// If there are multiple materials on a single mesh, cancel.
			if (materials.Length > 1) {
				// Rollback: return the object to original position
				combineParent.transform.position = originalPosition;
				Debug.LogError("Mesh Combine Wizard: Objects with multiple materials on the same mesh are not supported. Create multiple meshes from this object's sub-meshes in an external 3D tool and assign separate materials to each. Operation cancelled.");
				return;
			}
			var material = materials[0];

			// Add material to mesh filter mapping to dictionary
			if (materialToMeshFilterList.ContainsKey(material)) materialToMeshFilterList[material].Add(meshFilter);
			else materialToMeshFilterList.Add(material, new List<MeshFilter>() { meshFilter });
		}

		// For each material, create a new merged object, in the scene and in the assets.
		foreach (var entry in materialToMeshFilterList) {
			List<MeshFilter> meshesWithSameMaterial = entry.Value;
			// Create a convenient material name
			string materialName = entry.Key.ToString().Split(' ')[0];

			CombineInstance[] combine = new CombineInstance[meshesWithSameMaterial.Count];
			for (int i = 0; i < meshesWithSameMaterial.Count; i++) {
				combine[i].mesh = meshesWithSameMaterial[i].sharedMesh;
				combine[i].transform = meshesWithSameMaterial[i].transform.localToWorldMatrix;
			}

			// Create a new mesh using the combined properties
			var format = is32bit? IndexFormat.UInt32 : IndexFormat.UInt16;
			Mesh combinedMesh = new Mesh { indexFormat = format };
			combinedMesh.CombineMeshes(combine);

			if (generateSecondaryUVs) {
				var secondaryUVsResult = Unwrapping.GenerateSecondaryUVSet(combinedMesh);
				if (!secondaryUVsResult) {
					Debug.LogWarning("Mesh Combine Wizard: Could not generate secondary UVs. See https://docs.unity3d.com/2022.2/Documentation/ScriptReference/Unwrapping.GenerateSecondaryUVSet.html");
				}
			}

			// Create asset
			materialName += "_" + combinedMesh.GetInstanceID();
			AssetDatabase.CreateAsset(combinedMesh, Path.Combine(assetFolderResultPath, "CombinedMeshes_" + materialName + ".asset"));

			// Create game object
			string goName = (materialToMeshFilterList.Count > 1)? "CombinedMeshes_" + materialName : "CombinedMeshes_" + combineParent.name;
			GameObject combinedObject = new GameObject(goName);
			var filter = combinedObject.AddComponent<MeshFilter>();
			filter.sharedMesh = combinedMesh;
			var renderer = combinedObject.AddComponent<MeshRenderer>();
			renderer.sharedMaterial = entry.Key;
			combinedObjects.Add(combinedObject);
		}

		// If there was more than one material, and thus multiple GOs created, parent them and work with result
		GameObject resultGO = null;
		if (combinedObjects.Count > 1) {
			resultGO = new GameObject("CombinedMeshes_" + combineParent.name);
			foreach (var combinedObject in combinedObjects) combinedObject.transform.parent = resultGO.transform;
		}
		else {
			resultGO = combinedObjects[0];
		}

		// Create prefab
		var prefabPath = Path.Combine(assetFolderResultPath, resultGO.name + ".prefab");
		PrefabUtility.SaveAsPrefabAssetAndConnect(resultGO, prefabPath, InteractionMode.UserAction);

		// Disable the original and return both to original positions
		combineParent.SetActive(false);
		combineParent.transform.position = originalPosition;
		resultGO.transform.position = originalPosition;
	}
}