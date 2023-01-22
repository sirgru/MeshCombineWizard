## What does it do?
Running the wizard will combine all the meshes on the chosen Game Object and its children which share the same material. If there is more than a single material in all sub-objects, sub-objects will be created in the result so that each corresponds to a single material. A prefab will be created from the combined Game Object, with all the newly created merged meshes. The original will be set inactive in the scene and the combined Game Object will be put in its position.

## How does it work?
Put the provided script in a folder called _Editor_. In the Menu Bar a new entry will appear ("Ennoble Tools/Mesh Combine Wizard"). Picking this option will show the wizard dialog. The parent of objects to be combined should be assigned to a field called _Combine Parent_, which may be auto-assigned based on existing editor selection. 

Variable _Result Path_ is an optional string representing a path relative to the 'Assets/' folder, inside which the result mesh and prefab will be created. Leave blank to place the result in the 'Assets/' folder.

Generate secondary UVs option is explained by Unity's documentation:

> When you import a model asset, you can instruct Unity to compute a lightmap UV layout for it using [[ModelImporter-generateSecondaryUV]] or the Model Import Settings Inspector. This function allows you to do the same to procedurally generated meshes.

## Benefits:
* Lowers the amount of draw calls, usually dramatically.
* Does not need to draw all objects in the same batch regardless of wheter they are on screen or not, as opposed to static batching.
* Does not compromise workflow - no need to merge modular pieces in an external 3D tool. Quick iteration time when changing the combined design.
* Works with combined objects that have more than 64k verts.
* Does not compromise the original object's pivot point.

## Known limitations:
* Does not support objects with multiple materials on submeshes. Such meshes should be split in an external 3D tool, so that there is only a single material per mesh.
* Does not migrate components from the original other than MeshFilter and MeshRenderer.
* The default setting is using 32 bit indexes. From https://docs.unity3d.com/ScriptReference/Rendering.IndexFormat.UInt32.html (Note that GPU support for 32-bit indices is not guaranteed on all platforms; for example Android devices with Mali-400 GPU do not support them. When using 32-bit indices on such platforms, warning message will be logged and mesh will not render.) If the combined mesh has less than 65535 vertices, it is safe to leave the option unchecked in the window (otherwise meshes will be scrambled). 

## Versions:
3.0 - Fixed obsoletion warnings. Added option for secondary UVs generation. Added a few more options.
2.0 - Added proper support for meshes with over 65K verts. Hardened the script and commented for public consumption.
1.0 - Original release

## State
This small script is updated to work with Unity 2022.2. It is not being regularly maintained.

