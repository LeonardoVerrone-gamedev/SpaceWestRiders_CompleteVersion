
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class GhostPrefabGeneratorEditor : EditorWindow
{
    private string outputFolder = "Assets/Prefabs/GhostCars";
    private string suffix = "_Ghost";

    private bool autoConfigurePlayback = true;
    private bool removeAllRootComponents = true;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Ghost Cars/Prefab Generator")]
    public static void OpenWindow()
    {
        GetWindow<GhostPrefabGeneratorEditor>(
            "Ghost Prefab Generator"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Ghost Car Prefab Generator",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Selecione um ou mais prefabs de carros de gameplay " +
            "na Project Window e gere suas versões Ghost.",
            MessageType.Info
        );

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Configurações",
            EditorStyles.boldLabel
        );

        outputFolder = EditorGUILayout.TextField(
            "Pasta de destino",
            outputFolder
        );

        suffix = EditorGUILayout.TextField(
            "Sufixo",
            suffix
        );

        autoConfigurePlayback = EditorGUILayout.Toggle(
            "Configurar Playback automaticamente",
            autoConfigurePlayback
        );

        removeAllRootComponents = EditorGUILayout.Toggle(
            "Remover componentes do Root",
            removeAllRootComponents
        );

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Selecionar pasta de destino"))
        {
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Selecionar pasta de destino",
                Application.dataPath,
                ""
            );

            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    outputFolder = "Assets" +
                        selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Pasta inválida",
                        "A pasta precisa estar dentro da pasta Assets do projeto.",
                        "OK"
                    );
                }
            }
        }

        EditorGUILayout.Space(15);

        GameObject[] selectedPrefabs = GetSelectedPrefabs();

        EditorGUILayout.LabelField(
            $"Prefabs selecionados: {selectedPrefabs.Length}",
            EditorStyles.boldLabel
        );

        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition,
            GUILayout.Height(120)
        );

        foreach (GameObject prefab in selectedPrefabs)
        {
            EditorGUILayout.LabelField(
                prefab.name,
                EditorStyles.miniLabel
            );
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        GUI.enabled = selectedPrefabs.Length > 0;

        if (GUILayout.Button(
            "GERAR GHOST PREFABS",
            GUILayout.Height(40)
        ))
        {
            GenerateGhostPrefabs(selectedPrefabs);
        }

        GUI.enabled = true;

        EditorGUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "Os prefabs originais não serão alterados. " +
            "Os Ghosts serão criados como novos arquivos.",
            MessageType.Warning
        );
    }

    // =====================================================
    // OBTER PREFABS SELECIONADOS
    // =====================================================

    private GameObject[] GetSelectedPrefabs()
    {
        return Selection.GetFiltered<GameObject>(
            SelectionMode.Assets
        )
        .Where(prefab =>
            PrefabUtility.IsPartOfPrefabAsset(prefab)
        )
        .ToArray();
    }

    // =====================================================
    // GERAR PREFABS
    // =====================================================

    private void GenerateGhostPrefabs(GameObject[] sourcePrefabs)
    {
        if (sourcePrefabs == null || sourcePrefabs.Length == 0)
            return;

        if (!removeAllRootComponents)
        {
            EditorUtility.DisplayDialog(
                "Operação cancelada",
                "Ative a opção de remover componentes do Root " +
                "para gerar os Ghost Prefabs corretamente.",
                "OK"
            );

            return;
        }

        if (!ValidateOutputFolder())
            return;

        int successCount = 0;
        int failedCount = 0;

        try
        {
            for (int i = 0; i < sourcePrefabs.Length; i++)
            {
                GameObject sourcePrefab = sourcePrefabs[i];

                if (sourcePrefab == null)
                    continue;

                EditorUtility.DisplayProgressBar(
                    "Gerando Ghost Prefabs",
                    sourcePrefab.name,
                    (float)i / sourcePrefabs.Length
                );

                if (GenerateSingleGhostPrefab(sourcePrefab))
                    successCount++;
                else
                    failedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Ghost Prefabs finalizados",
            $"Gerados: {successCount}\n" +
            $"Falhas: {failedCount}\n\n" +
            $"Destino: {outputFolder}",
            "OK"
        );
    }

    // =====================================================
    // GERAR UM ÚNICO PREFAB
    // =====================================================

    private bool GenerateSingleGhostPrefab(GameObject sourcePrefab)
    {
        GameObject prefabContents = null;

        try
        {
            prefabContents = PrefabUtility.LoadPrefabContents(
                AssetDatabase.GetAssetPath(sourcePrefab)
            );

            if (prefabContents == null)
                return false;

            // O Identity precisa existir para identificar o carro.
            SCR_CarIdentity identity =
                prefabContents.GetComponent<SCR_CarIdentity>();

            if (identity == null || identity.racerData == null)
            {
                Debug.LogError(
                    $"[GHOST GENERATOR] {sourcePrefab.name}: " +
                    "SCR_CarIdentity ou racerData não encontrado."
                );

                return false;
            }

            // Remove todos os componentes do Root,
            // preservando Transform e SCR_CarIdentity.
            RemoveRootComponents(prefabContents);

            // Adiciona o componente de playback.
            GhostPlayback playback =
                prefabContents.GetComponent<GhostPlayback>();

            if (playback == null)
            {
                playback = prefabContents.AddComponent<GhostPlayback>();
            }

            // Configura referências visuais.
            if (autoConfigurePlayback)
            {
                ConfigurePlayback(prefabContents, playback);
            }

            string outputPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{outputFolder}/{sourcePrefab.name}{suffix}.prefab"
            );

            bool success;

            PrefabUtility.SaveAsPrefabAsset(
                prefabContents,
                outputPath,
                out success
            );

            if (!success)
            {
                Debug.LogError(
                    $"[GHOST GENERATOR] Falha ao salvar {outputPath}"
                );

                return false;
            }

            Debug.Log(
                $"[GHOST GENERATOR] Ghost criado: {outputPath}\n" +
                $"Character ID: {identity.racerData.characterID}"
            );

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[GHOST GENERATOR] Erro em {sourcePrefab.name}: {e}"
            );

            return false;
        }
        finally
        {
            if (prefabContents != null)
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }
    }

    // =====================================================
    // REMOVER COMPONENTES DO ROOT
    // =====================================================

    private void RemoveRootComponents(GameObject root)
    {
        int safetyLimit = 20;
        int iteration = 0;

        bool removedSomething = true;

        while (removedSomething && iteration < safetyLimit)
        {
            removedSomething = false;
            iteration++;

            Component[] components =
                root.GetComponents<Component>();

            foreach (Component component in components)
            {
                // Transform nunca pode ser removido.
                if (component == null ||
                    component is Transform ||
                    component is SCR_CarIdentity ||
                    component is GhostPlayback)
                {
                    continue;
                }

                try
                {
                    Undo.DestroyObjectImmediate(component);
                    removedSomething = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"[GHOST GENERATOR] Não foi possível remover " +
                        $"{component.GetType().Name} de {root.name}: {e.Message}"
                    );
                }
            }
        }

        // Verificação final.
        Component[] remainingComponents =
            root.GetComponents<Component>();

        foreach (Component component in remainingComponents)
        {
            if (component == null ||
                component is Transform ||
                component is SCR_CarIdentity ||
                component is GhostPlayback)
            {
                continue;
            }

            Debug.LogError(
                $"[GHOST GENERATOR] Componente restante no Root: " +
                $"{component.GetType().Name} em {root.name}"
            );
        }

        Debug.Log(
            $"[GHOST GENERATOR] Limpeza concluída: {root.name} " +
            $"({iteration} iterações)."
        );
    }

    // =====================================================
    // CONFIGURAR GHOST PLAYBACK
    // =====================================================

    private void ConfigurePlayback(
        GameObject root,
        GhostPlayback playback)
    {
        Transform[] children =
            root.GetComponentsInChildren<Transform>(true);

        Transform carBody = FindTransform(
            children,
            "carbody",
            "body",
            "chassis",
            "car_body"
        );

        Transform frontLeft = FindTransform(
            children,
            "frontleftwheel",
            "front_left_wheel",
            "wheelfl",
            "wheel_fl"
        );

        Transform frontRight = FindTransform(
            children,
            "frontrightwheel",
            "front_right_wheel",
            "wheelfr",
            "wheel_fr"
        );

        Transform rearLeft = FindTransform(
            children,
            "rearleftwheel",
            "rear_left_wheel",
            "wheelrl",
            "wheel_rl"
        );

        Transform rearRight = FindTransform(
            children,
            "rearrightwheel",
            "rear_right_wheel",
            "wheelrr",
            "wheel_rr"
        );

        SerializedObject serializedPlayback =
            new SerializedObject(playback);

        serializedPlayback.Update();

        SerializedProperty bodyProperty =
            serializedPlayback.FindProperty("carBody");

        SerializedProperty wheelsProperty =
            serializedPlayback.FindProperty("wheels");

        if (bodyProperty != null)
        {
            bodyProperty.objectReferenceValue = carBody;
        }

        if (wheelsProperty != null)
        {
            wheelsProperty.arraySize = 4;

            wheelsProperty.GetArrayElementAtIndex(0)
                .objectReferenceValue = frontLeft;

            wheelsProperty.GetArrayElementAtIndex(1)
                .objectReferenceValue = frontRight;

            wheelsProperty.GetArrayElementAtIndex(2)
                .objectReferenceValue = rearLeft;

            wheelsProperty.GetArrayElementAtIndex(3)
                .objectReferenceValue = rearRight;
        }

        serializedPlayback.ApplyModifiedPropertiesWithoutUndo();

        if (carBody == null)
        {
            Debug.LogWarning(
                $"[GHOST GENERATOR] CarBody não identificado em {root.name}."
            );
        }

        if (frontLeft == null ||
            frontRight == null ||
            rearLeft == null ||
            rearRight == null)
        {
            Debug.LogWarning(
                $"[GHOST GENERATOR] Uma ou mais rodas não foram " +
                $"identificadas em {root.name}. Confira no Inspector."
            );
        }
    }

    // =====================================================
    // LOCALIZAR TRANSFORMS
    // =====================================================

    private Transform FindTransform(
        Transform[] transforms,
        params string[] possibleNames)
    {
        foreach (Transform candidate in transforms)
        {
            string normalizedName = NormalizeName(candidate.name);

            foreach (string possibleName in possibleNames)
            {
                if (normalizedName == NormalizeName(possibleName))
                    return candidate;
            }
        }

        return null;
    }

    private string NormalizeName(string value)
    {
        return value
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    // =====================================================
    // VALIDAR E CRIAR PASTA
    // =====================================================

    private bool ValidateOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(outputFolder) ||
            !outputFolder.StartsWith("Assets"))
        {
            EditorUtility.DisplayDialog(
                "Pasta inválida",
                "O destino precisa estar dentro de Assets.",
                "OK"
            );

            return false;
        }

        string absolutePath = Path.GetFullPath(outputFolder);

        if (!Directory.Exists(absolutePath))
        {
            Directory.CreateDirectory(absolutePath);
        }

        AssetDatabase.Refresh();

        return true;
    }
}
