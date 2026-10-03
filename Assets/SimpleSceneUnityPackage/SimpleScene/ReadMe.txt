SimpleScene Unity Package

SimpleScene
-----------
SimpleScene is a standalone serializable scene reference. In the Inspector it
accepts a SceneAsset; at runtime use myScene.Index with Unity's scene APIs.
SimpleScene has no dependency on the scene graph or Scene Set systems.

Project scene graph
-------------------
Set a Base Scene in Project Settings > SimpleScene, then choose Rebuild Build
Settings and graph. The collector starts at that scene and follows serialized
references through MonoBehaviours, referenced prefabs, and ScriptableObjects.
Only reachable prefabs are inspected; unreferenced project prefabs are not put
in the build.

The collected data retains scene edges as well as asset links. Each link stores
the root scene, source asset, GameObject hierarchy path, component type,
serialized property path, and reference count. This is intended to support a
future graph visualization without another expensive project scan.

Scene Sets
----------
A Scene Set is a ScriptableObject holding one or more SimpleScenes. It is a
generic loading configuration: use it for menus, missions, save locations,
streaming regions, or checkpoints. Create one with Assets > Create > Simple
Scene > Scene Set, then assign it directly to ChangeSceneSet or call:

    SceneSetLoader.Load(mySceneSet);

For a preload/activate flow, call Load(mySceneSet, true), then call
SceneSetLoader.ActivatePreloadedScenes when ready.

The Exclude From Build checkbox on each Scene Set leaves it usable in the
Editor while excluding its scenes from graph rebuilds and player builds. A
Scene Set Database is optional: it is a registry with an Initial Scene Set and
is automatically treated as a graph root for Resources-style bootstrapping.
It also has an Exclude From Build checkbox for editor-only databases.

Editor tools
------------
Tools > SimpleScene > Scene Set Saver stores the currently open scenes in a
Scene Set. Scene Set Loader opens the scenes in a selected Scene Set. The old
checkpoint type names remain as obsolete compatibility shims only; new code
should use SceneSet, SceneSetDatabase, SceneSetLoader, and ChangeSceneSet.
Existing ScenesInCheckpointDatabase assets expose a Context Menu action to
migrate their legacy enum entries into Scene Set assets.
