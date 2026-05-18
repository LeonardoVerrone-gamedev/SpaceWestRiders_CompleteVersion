using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ManualRecordingCamera : MonoBehaviour
{
    public List<CinemachineCamera> Cameras;

    public void SetCamera(int index)
    {
        if (index < 0 || index >= Cameras.Count || Cameras[index] == null) return;

        Cameras[index].Priority.Value = 30;

        foreach (CinemachineCamera vCam in Cameras)
        {
            if (vCam == null || Cameras.IndexOf(vCam) == index)
            {
                continue;
            }
            else
            {
                vCam.Priority.Value = 0;
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ManualRecordingCamera))]
public class ManualRecordingCameraEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Desenha o Inspector padrão (a lista de câmeras)
        DrawDefaultInspector();

        ManualRecordingCamera script = (ManualRecordingCamera)target;

        if (script.Cameras == null || script.Cameras.Count == 0)
        {
            EditorGUILayout.HelpBox("Adicione câmeras na lista para gerar os botões.", MessageType.Info);
            return;
        }

        GUILayout.Space(15);
        GUILayout.Label("Corte de Câmeras em Tempo Real", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // Cria um botão para cada câmera da lista
        for (int i = 0; i < script.Cameras.Count; i++)
        {
            CinemachineCamera cam = script.Cameras[i];
            
            // Define o nome do botão baseado no nome do GameObject da câmera (ou um fallback se estiver vazio)
            string buttonName = cam != null ? $"[Cam {i}] {cam.name}" : $"[Cam {i}] Vazia";

            // Se o botão for clicado...
            if (GUILayout.Button(buttonName, GUILayout.Height(30)))
            {
                // Registra o Undo para a Unity não se perder com as prioridades mudando em Runtime/Editor
                Undo.RecordObjects(script.Cameras.ToArray(), "Troca de Câmera Manual");
                script.SetCamera(i);
            }
        }
    }
}
#endif