#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[ExecuteInEditMode]
public class GridSpawner : EditorWindow
{
    GameObject prefab;
    Transform parentObject;
    Transform firstTilePos;
    public string goName = "Object";
    public int width = 5;
    public int height = 5;
    public float spacing = 1.44525f;
    [MenuItem("Tools/GridSpawner")]
    public static void ShowWindow()
    { 

        GetWindow<GridSpawner>("Grid Spawner");
    }

    private void OnGUI()
    {
        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
        goName = EditorGUILayout.TextField("Prefab Name", goName);
        width = EditorGUILayout.IntField("Width", width);
        height = EditorGUILayout.IntField("Height", height);
        spacing = EditorGUILayout.FloatField("Spacing", spacing);
        parentObject = (Transform)EditorGUILayout.ObjectField("Parent", parentObject, typeof(Transform), true);

        firstTilePos = (Transform)EditorGUILayout.ObjectField("First Tile Position", firstTilePos, typeof(Transform), true);
        if (GUILayout.Button("Generate Grid"))
        {
            Generate();
        }
    }
    private void Generate()
    {
        if (prefab == null || firstTilePos == null || parentObject==null)
        {
            Debug.LogError("No null values accepted for any field.");
            return;
        }
        // Crear nuevo grupo de undo
        int undoGroup = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Grid Generation");
        Undo.RegisterFullObjectHierarchyUndo(parentObject, "Grid Parent Change");

        Vector3 initPos = firstTilePos.position + new Vector3(spacing * 0.5f, 0, spacing * 0.5f);
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                Vector3 tempPos = initPos + new Vector3(i * spacing, 0, j * spacing);
                GameObject tile = PrefabUtility.InstantiatePrefab(prefab, parentObject) as GameObject;
                Undo.RegisterCreatedObjectUndo(tile, "Create Grid Tile");
                tile.transform.position = tempPos;
                
                tile.name = goName+ i + "_" + j;


            }
        }
        EditorSceneManager.MarkSceneDirty(parentObject.gameObject.scene);

        Undo.CollapseUndoOperations(undoGroup);
    }
}
#endif