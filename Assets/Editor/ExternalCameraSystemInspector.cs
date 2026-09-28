#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(F1ExternalCameraManager))]
public class F1ExternalCameraManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        F1ExternalCameraManager manager =
            (F1ExternalCameraManager)target;

        DrawDefaultInspector();

        EditorGUILayout.Space(15);

        EditorGUILayout.LabelField(
            "Camera Control",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Selecione qual corredor as câmeras externas devem acompanhar.",
            MessageType.Info
        );

        EditorGUILayout.Space(5);

        // =====================================================
        // FOLLOW FIRST
        // =====================================================

        GUI.backgroundColor = new Color(0.25f, 0.8f, 1f);

        if (GUILayout.Button(
            "FOLLOW FIRST",
            GUILayout.Height(35)))
        {
            Undo.RecordObject(manager, "Follow First");

            manager.FollowFirst();

            EditorUtility.SetDirty(manager);
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);

        // =====================================================
        // RACERS
        // =====================================================

        SerializedProperty racersProperty =
            serializedObject.FindProperty("racers");

        if (racersProperty == null)
            return;

        for (int i = 0; i < racersProperty.arraySize; i++)
        {
            SerializedProperty racerProperty =
                racersProperty.GetArrayElementAtIndex(i);

            RacerStatus racer =
                racerProperty.objectReferenceValue as RacerStatus;

            if (racer == null)
                continue;

            GUI.backgroundColor = Color.gray;

            string racerName =
                string.IsNullOrEmpty(racer.gameObject.name)
                    ? $"Racer {i + 1}"
                    : racer.gameObject.name;

            if (GUILayout.Button(
                $"FOLLOW {i + 1}  -  {racerName}",
                GUILayout.Height(30)))
            {
                Undo.RecordObject(manager, "Follow Racer");

                manager.FollowRacer(i);

                EditorUtility.SetDirty(manager);
            }

            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(10);

        // =====================================================
        // REFRESH
        // =====================================================

        if (GUILayout.Button("Refresh Racers"))
        {
            Undo.RecordObject(manager, "Refresh Racers");

            manager.RefreshRacers();

            EditorUtility.SetDirty(manager);
        }

        if (GUILayout.Button("Refresh Cameras"))
        {
            Undo.RecordObject(manager, "Refresh Cameras");

            manager.RefreshCameras();

            EditorUtility.SetDirty(manager);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

#endif
