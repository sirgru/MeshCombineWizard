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

    private static new readonly Vector2Int minSize = new Vector2Int(700, 430);
    private static new readonly Vector2Int maxSize = new Vector2Int(1200, 430);

    [MenuItem("Ennoble Tools/Mesh Combine Wizard")]
    static void CreateWizard()
    {
        MeshCombineWizard wizard = DisplayWizard<MeshCombineWizard>("Mesh Combine Wizard");
        EditorWindow window = GetWindow<MeshCombineWizard>();
        window.minSize = minSize;
        window.maxSize = maxSize;

        // If there is a single selected object, auto-assign it as combineParent
        if (Selection.activeGameObject != null)
        {
            wizard.combineParent = Selection.activeGameObject;
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Settings while combining meshes.", EditorStyles.boldLabel);

        GUILayout.Space(20);

        GUILayout.Label("Parent Object", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("The parent object should have the meshes, which we want to combine, as its children.");
        EditorGUILayout.LabelField("By default, the currently selected object in heirarchy will be assigned.");
        combineParent = EditorGUILayout.ObjectField("Parent Object:", combineParent, typeof(GameObject), true) as GameObject;

        GUILayout.Space(20);

        GUILayout.Label("Path Settings", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Keeping the path empty will create the combined mesh prefab in the root 'Assets' folder.");
        resultPath = EditorGUILayout.TextField("Result Path:", resultPath);

        GUILayout.Space(20);

        GUILayout.Label("Indices Settings", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Enable 32-bit index if the combined mesh has more than 65535 vertices. (to avoid scrambled meshes)");
        is32bit = EditorGUILayout.Toggle("Use 32-bit Index:", is32bit);

        GUILayout.Space(20);

        GUILayout.Label("Secondary UVs Settings", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("When you import a model, you can compute a lightmap UV for it using [[ModelImporter-generateSecondaryUV]]");
        EditorGUILayout.LabelField("or the Model Import Settings Inspector. This allows you to do the same to procedural meshes.");
        generateSecondaryUVs = EditorGUILayout.Toggle("Generate Secondary UVs:", generateSecondaryUVs);

        GUILayout.Space(20);

        if (GUILayout.Button("Combine Meshes"))
        {
            CombineMeshes();
        }
    }

    void CombineMeshes()
    {
        if (combineParent == null)
        {
            Debug.LogError("Mesh Combine Wizard: Parent object not assigned. Operation cancelled.");
            return;
        }

        string assetFolderResultPath = Path.Combine("Assets/", resultPath ?? "");
        if (!Directory.Exists(assetFolderResultPath))
        {
            Debug.LogError("Mesh Combine Wizard: Result path does not exist or is invalid. Operation cancelled.");
            return;
        }

        Vector3 originalPosition = combineParent.transform.position;
        combineParent.transform.position = Vector3.zero;

        Dictionary<Material, List<MeshFilter>> materialToMeshFilterList = new Dictionary<Material, List<MeshFilter>>();
        List<GameObject> combinedObjects = new List<GameObject>();

        MeshFilter[] meshFilters = combineParent.GetComponentsInChildren<MeshFilter>();

        foreach (var meshFilter in meshFilters)
        {
            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer == null || meshRenderer.sharedMaterial == null)
            {
                Debug.LogWarning("Mesh Combine Wizard: Skipping mesh without renderer or material.");
                continue;
            }

            Material material = meshRenderer.sharedMaterial;

            if (materialToMeshFilterList.ContainsKey(material))
                materialToMeshFilterList[material].Add(meshFilter);
            else
                materialToMeshFilterList.Add(material, new List<MeshFilter>() { meshFilter });
        }

        foreach (var entry in materialToMeshFilterList)
        {
            List<MeshFilter> meshesWithSameMaterial = entry.Value;
            string materialName = entry.Key.name + "_" + entry.Key.GetInstanceID();

            CombineInstance[] combine = new CombineInstance[meshesWithSameMaterial.Count];
            for (int i = 0; i < meshesWithSameMaterial.Count; i++)
            {
                combine[i].mesh = meshesWithSameMaterial[i].sharedMesh;
                combine[i].transform = meshesWithSameMaterial[i].transform.localToWorldMatrix;
            }

            Mesh combinedMesh = new Mesh { indexFormat = is32bit ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            combinedMesh.CombineMeshes(combine);

            //Generate Secondary UVs
            if (generateSecondaryUVs)
            {
                if (!UnityEditor.Unwrapping.GenerateSecondaryUVSet(combinedMesh))
                {
                    Debug.LogWarning("Mesh Combine Wizard: Could not generate secondary UVs. See https://docs.unity3d.com/2022.2/Documentation/ScriptReference/Unwrapping.GenerateSecondaryUVSet.html");
                }

                // If a version of Unity earlier than 2022 is being used then comment the above portion and uncomment the code below
                //UnityEditor.Unwrapping.GenerateSecondaryUVSet(combinedMesh);
            }

            string assetName = "CombinedMeshes_" + materialName;
            string assetPath = Path.Combine(assetFolderResultPath, assetName + ".asset");
            AssetDatabase.CreateAsset(combinedMesh, assetPath);

            GameObject combinedObject = new GameObject(assetName);
            MeshFilter filter = combinedObject.AddComponent<MeshFilter>();
            filter.sharedMesh = combinedMesh;
            MeshRenderer renderer = combinedObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = entry.Key;
            combinedObjects.Add(combinedObject);
        }

        GameObject resultGO = (combinedObjects.Count > 1) ? new GameObject("CombinedMeshes_" + combineParent.name) : combinedObjects[0];

        foreach (var combinedObject in combinedObjects)
            combinedObject.transform.parent = resultGO.transform;

        string prefabPath = Path.Combine(assetFolderResultPath, resultGO.name + ".prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(resultGO, prefabPath, InteractionMode.UserAction);

        combineParent.SetActive(false);
        combineParent.transform.position = originalPosition;
        resultGO.transform.position = originalPosition;
    }
}