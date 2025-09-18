using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// Place this script in an "Editor" folder in your Assets
[InitializeOnLoad]
public static class ObjectLocker
{
    private static HashSet<GameObject> lockedObjects = new HashSet<GameObject>();
    private static readonly string LOCKED_OBJECTS_KEY = "ObjectLocker_LockedObjects";

    // Public property to access locked objects
    public static HashSet<GameObject> LockedObjects => lockedObjects;

    static ObjectLocker()
    {
        // Load locked objects from EditorPrefs on startup
        LoadLockedObjects();

        // Subscribe to the scene view delegate
        SceneView.duringSceneGui += OnSceneGUI;

        // Subscribe to hierarchy window events for visual indicators only
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    }

    // Menu item to toggle lock state - Ctrl+L
    [MenuItem("GameObject/Toggle Lock %l", false, 0)]
    static void ToggleLock()
    {
        List<GameObject> toLock = new List<GameObject>();
        List<GameObject> toUnlock = new List<GameObject>();

        foreach (GameObject obj in Selection.gameObjects)
        {
            if (obj != null)
            {
                if (lockedObjects.Contains(obj))
                {
                    toUnlock.Add(obj);
                }
                else
                {
                    toLock.Add(obj);
                }
            }
        }

        // Process unlocks
        foreach (GameObject obj in toUnlock)
        {
            lockedObjects.Remove(obj);
            EditorUtility.SetDirty(obj);
        }

        // Process locks
        foreach (GameObject obj in toLock)
        {
            lockedObjects.Add(obj);
            EditorUtility.SetDirty(obj);
        }

        SaveLockedObjects();

        if (toLock.Count > 0)
            Debug.Log($"Locked {toLock.Count} object(s)");
        if (toUnlock.Count > 0)
            Debug.Log($"Unlocked {toUnlock.Count} object(s)");

        SceneView.RepaintAll();
        EditorApplication.RepaintHierarchyWindow();
    }

    // Menu item to unlock all objects
    [MenuItem("GameObject/Unlock All Objects", false, 0)]
    public static void UnlockAll()
    {
        int count = lockedObjects.Count;
        lockedObjects.Clear();
        SaveLockedObjects();
        Debug.Log($"Unlocked {count} object(s)");
        SceneView.RepaintAll();
        EditorApplication.RepaintHierarchyWindow();
    }

    // Validate menu items
    [MenuItem("GameObject/Toggle Lock %l", true)]
    static bool ValidateObjectSelected()
    {
        return Selection.gameObjects.Length > 0;
    }

    // Handle hierarchy window - ONLY show lock icon, don't prevent selection
    static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj != null && lockedObjects.Contains(obj))
        {
            // Draw lock icon in hierarchy
            Rect lockRect = new Rect(selectionRect.xMax - 20, selectionRect.y, 20, 20);
            GUI.Label(lockRect, "🔒");
        }
    }

    // Draw visual indicators and block selection in Scene view ONLY
    static void OnSceneGUI(SceneView sceneView)
    {
        // Clean up null references
        lockedObjects.RemoveWhere(obj => obj == null);

        // Block mouse events in scene view for locked objects
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !Event.current.alt)
        {
            // Use Unity's picking to see what would be selected
            GameObject hitObject = HandleUtility.PickGameObject(Event.current.mousePosition, false);

            if (hitObject != null && lockedObjects.Contains(hitObject))
            {
                // This specific object is locked, try to select its parent instead
                Transform parent = hitObject.transform.parent;

                if (parent != null && !lockedObjects.Contains(parent.gameObject))
                {
                    // Select the parent instead of the locked child
                    Event.current.Use();
                    Selection.activeGameObject = parent.gameObject;
                    Debug.Log($"'{hitObject.name}' is locked - selected parent '{parent.name}' instead");
                }
                else
                {
                    // No unlocked parent available, just block the selection
                    Event.current.Use();
                    Debug.LogWarning($"'{hitObject.name}' is locked in Scene view! Select it in Hierarchy and press Ctrl+L to unlock.");
                }
                return;
            }
        }

        // Block drag selection in Scene view
        if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
        {
            // If any selected object is locked, prevent the drag
            foreach (GameObject obj in Selection.gameObjects)
            {
                if (lockedObjects.Contains(obj))
                {
                    Event.current.Use();
                    // Remove locked objects from selection when dragging in Scene
                    Selection.objects = Selection.objects.Where(o => !lockedObjects.Contains(o as GameObject)).ToArray();
                    return;
                }
            }
        }

        // Draw lock icons on locked objects
        foreach (GameObject obj in lockedObjects)
        {
            if (obj == null) continue;

            // Get object position in GUI coordinates
            Vector3 worldPos = obj.transform.position;
            Vector3 guiPos = HandleUtility.WorldToGUIPoint(worldPos);

            // Skip if object is behind camera
            if (guiPos.z < 0) continue;

            // Draw a lock icon
            Handles.BeginGUI();

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 12;

            GUI.color = new Color(1, 0.3f, 0, 0.9f); // Red-orange for locked
            GUI.Box(new Rect(guiPos.x - 10, guiPos.y - 25, 20, 20), "🔒", style);

            // Draw object name
            style.normal.background = null;
            GUI.Label(new Rect(guiPos.x - 50, guiPos.y - 45, 100, 20), obj.name, style);

            GUI.color = Color.white;
            Handles.EndGUI();

            // Draw wireframe overlay
            if (obj.GetComponent<Renderer>() != null)
            {
                Handles.color = new Color(1, 0.3f, 0, 0.3f);
                Bounds bounds = obj.GetComponent<Renderer>().bounds;
                Handles.DrawWireCube(bounds.center, bounds.size);
            }
            else if (obj.GetComponent<Collider>() != null)
            {
                Handles.color = new Color(1, 0.3f, 0, 0.3f);
                Bounds bounds = obj.GetComponent<Collider>().bounds;
                Handles.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }

    // Save locked objects to EditorPrefs
    public static void SaveLockedObjects()
    {
        var instanceIDs = lockedObjects
            .Where(obj => obj != null)
            .Select(obj => obj.GetInstanceID())
            .ToArray();

        string data = string.Join(",", instanceIDs);
        EditorPrefs.SetString(LOCKED_OBJECTS_KEY, data);
    }

    // Load locked objects from EditorPrefs
    static void LoadLockedObjects()
    {
        lockedObjects.Clear();
        string data = EditorPrefs.GetString(LOCKED_OBJECTS_KEY, "");

        if (!string.IsNullOrEmpty(data))
        {
            string[] ids = data.Split(',');
            foreach (string id in ids)
            {
                if (int.TryParse(id, out int instanceID))
                {
                    GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                    if (obj != null)
                    {
                        lockedObjects.Add(obj);
                    }
                }
            }
        }
    }

    // Check if an object is locked
    public static bool IsLocked(GameObject obj)
    {
        return obj != null && lockedObjects.Contains(obj);
    }
}

// Custom Editor Window for managing locked objects
public class ObjectLockerWindow : EditorWindow
{
    private Vector2 scrollPos;

    [MenuItem("Window/Object Locker Manager")]
    public static void ShowWindow()
    {
        GetWindow<ObjectLockerWindow>("Object Locker");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Locked Objects Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("• Ctrl+L toggles lock on selected objects\n• Locked objects cannot be selected in Scene view\n• You can still select them in Hierarchy to unlock", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("Unlock All Objects"))
        {
            ObjectLocker.UnlockAll();
            Repaint();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Currently Locked Objects: {ObjectLocker.LockedObjects.Count}", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        var lockedObjects = ObjectLocker.LockedObjects;

        if (lockedObjects != null && lockedObjects.Count > 0)
        {
            List<GameObject> toUnlock = new List<GameObject>();

            foreach (GameObject obj in lockedObjects.ToList())
            {
                if (obj == null) continue;

                EditorGUILayout.BeginHorizontal();

                // Object field
                EditorGUILayout.ObjectField(obj, typeof(GameObject), true);

                // Focus button
                if (GUILayout.Button("Focus", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = obj;
                    SceneView.FrameLastActiveSceneView();
                }

                // Unlock button
                if (GUILayout.Button("Unlock", GUILayout.Width(60)))
                {
                    toUnlock.Add(obj);
                }

                EditorGUILayout.EndHorizontal();
            }

            // Unlock objects
            foreach (GameObject obj in toUnlock)
            {
                lockedObjects.Remove(obj);
                Selection.activeGameObject = obj;
            }

            if (toUnlock.Count > 0)
            {
                ObjectLocker.SaveLockedObjects();
                SceneView.RepaintAll();
                EditorApplication.RepaintHierarchyWindow();
                Repaint();
            }
        }
        else
        {
            EditorGUILayout.LabelField("No objects are currently locked.");
        }

        EditorGUILayout.EndScrollView();
    }
}

// Custom transform inspector that shows lock status
[CustomEditor(typeof(Transform))]
public class LockedTransformInspector : Editor
{
    private Editor defaultEditor;

    void OnEnable()
    {
        System.Type type = System.Type.GetType("UnityEditor.TransformInspector, UnityEditor");
        defaultEditor = CreateEditor(target, type);
    }

    void OnDisable()
    {
        if (defaultEditor != null)
            DestroyImmediate(defaultEditor);
    }

    public override void OnInspectorGUI()
    {
        Transform transform = target as Transform;

        if (transform != null && ObjectLocker.IsLocked(transform.gameObject))
        {
            EditorGUILayout.HelpBox("🔒 Object is locked in Scene view\nPress Ctrl+L to unlock", MessageType.Warning);

            // Still allow editing in Inspector even when locked
            defaultEditor.OnInspectorGUI();
        }
        else
        {
            defaultEditor.OnInspectorGUI();
        }
    }
}