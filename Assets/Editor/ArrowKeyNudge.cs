using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class ArrowKeyNudge
{
    private const string PrefKey = "ArrowKeyNudgeStep"; // EditorPrefs key
    private static float step = 0.1f; // default value

    static ArrowKeyNudge()
    {
        // Load saved value from EditorPrefs
        step = EditorPrefs.GetFloat(PrefKey, step);
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        if (Selection.activeTransform != null && e.type == EventType.KeyDown)
        {
            Vector3 move = Vector3.zero;

            switch (e.keyCode)
            {
                case KeyCode.UpArrow: move = Vector3.forward * step; break;
                case KeyCode.DownArrow: move = Vector3.back * step; break;
                case KeyCode.LeftArrow: move = Vector3.left * step; break;
                case KeyCode.RightArrow: move = Vector3.right * step; break;
            }

            if (move != Vector3.zero)
            {
                Undo.RecordObject(Selection.activeTransform, "Nudge");
                Selection.activeTransform.position += move;
                e.Use(); // consume the event
            }
        }
    }

    // Modern Unity Preferences entry
    [SettingsProvider]
    public static SettingsProvider CreateNudgeSettingsProvider()
    {
        var provider = new SettingsProvider("Preferences/Arrow Nudge", SettingsScope.User)
        {
            label = "Arrow Nudge",
            guiHandler = (searchContext) =>
            {
                float newStep = EditorGUILayout.FloatField("Nudge Amount", step);
                if (!Mathf.Approximately(newStep, step))
                {
                    step = newStep;
                    EditorPrefs.SetFloat(PrefKey, step);
                }
            }
        };

        return provider;
    }
}
