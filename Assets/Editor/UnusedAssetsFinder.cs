using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class UnusedAssetFinder : EditorWindow
{
private class AssetEntry
{
public string path;
public bool selected;
public System.Type type;
public long sizeBytes;
}

private enum AssetFilter
{
    All,
    Texture,
    Material,
    FBX,
    Audio,
    Prefab
}

private string folderPath = "Assets";
private string searchFilter = "";

private AssetFilter filter = AssetFilter.All;

private bool sortBySize = true;

private Vector2 scroll;

private List<AssetEntry> unusedAssets = new();

[MenuItem("Tools/Unused Asset Finder")]
public static void Open()
{
    GetWindow<UnusedAssetFinder>("Unused Asset Finder");
}

private void OnGUI()
{
    GUILayout.Space(5);

    GUILayout.Label(
        "Unused Asset Finder",
        EditorStyles.boldLabel);

    GUILayout.Space(5);

    EditorGUILayout.BeginHorizontal();

    folderPath = EditorGUILayout.TextField(
        "Folder",
        folderPath);

    if (GUILayout.Button("Browse", GUILayout.Width(80)))
    {
        string absolutePath =
            EditorUtility.OpenFolderPanel(
                "Select Folder",
                Application.dataPath,
                "");

        if (!string.IsNullOrEmpty(absolutePath))
        {
            string dataPath =
                Application.dataPath.Replace("\\", "/");

            if (absolutePath.StartsWith(dataPath))
            {
                folderPath =
                    "Assets" +
                    absolutePath.Substring(dataPath.Length);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Invalid Folder",
                    "Folder must be inside Assets.",
                    "OK");
            }
        }
    }

    EditorGUILayout.EndHorizontal();

    GUILayout.Space(5);

    searchFilter = EditorGUILayout.TextField(
        "Search",
        searchFilter);

    filter =
        (AssetFilter)EditorGUILayout.EnumPopup(
            "Filter",
            filter);

    sortBySize =
        EditorGUILayout.Toggle(
            "Sort By Size",
            sortBySize);

    GUILayout.Space(10);

    if (GUILayout.Button(
            "Scan For Unused Assets",
            GUILayout.Height(30)))
    {
        Scan();
    }

    GUILayout.Space(5);

    EditorGUILayout.BeginHorizontal();

    if (GUILayout.Button("Select All"))
    {
        foreach (var asset in unusedAssets)
            asset.selected = true;
    }

    if (GUILayout.Button("Deselect All"))
    {
        foreach (var asset in unusedAssets)
            asset.selected = false;
    }

    EditorGUILayout.EndHorizontal();

    GUILayout.Space(10);

    EditorGUILayout.LabelField(
        $"Found {unusedAssets.Count} unused assets",
        EditorStyles.helpBox);

    DrawHeader();

    scroll = EditorGUILayout.BeginScrollView(scroll);

    foreach (var asset in unusedAssets)
    {
        if (!PassesFilter(asset))
            continue;

        if (!string.IsNullOrEmpty(searchFilter))
        {
            if (!asset.path
                .ToLower()
                .Contains(searchFilter.ToLower()))
            {
                continue;
            }
        }

        DrawAssetRow(asset);
    }

    EditorGUILayout.EndScrollView();

    GUILayout.Space(5);

    int selectedCount =
        unusedAssets.Count(x => x.selected);

    long selectedBytes =
        unusedAssets
        .Where(x => x.selected)
        .Sum(x => x.sizeBytes);

    EditorGUILayout.HelpBox(
        $"Selected Assets: {selectedCount}\n" +
        $"Selected Size: {FormatSize(selectedBytes)}",
        MessageType.Info);

    GUI.backgroundColor = Color.red;

    if (GUILayout.Button(
            $"Delete Selected Assets ({selectedCount})",
            GUILayout.Height(40)))
    {
        DeleteSelected();
    }

    GUI.backgroundColor = Color.white;
}

private void DrawHeader()
{
    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

    GUILayout.Label("", GUILayout.Width(25));
    GUILayout.Label("Asset");
    GUILayout.Label("Type", GUILayout.Width(120));
    GUILayout.Label("Size", GUILayout.Width(90));

    EditorGUILayout.EndHorizontal();
}

private void DrawAssetRow(AssetEntry asset)
{
    EditorGUILayout.BeginHorizontal();

    asset.selected =
        EditorGUILayout.Toggle(
            asset.selected,
            GUILayout.Width(25));

    GUIStyle style =
        new GUIStyle(EditorStyles.linkLabel);

    if (GUILayout.Button(
            asset.path,
            style))
    {
        Object obj =
            AssetDatabase.LoadMainAssetAtPath(
                asset.path);

        Selection.activeObject = obj;

        EditorGUIUtility.PingObject(obj);
    }

    GUILayout.Label(
        asset.type != null
            ? asset.type.Name
            : "Unknown",
        GUILayout.Width(120));

    GUILayout.Label(
        FormatSize(asset.sizeBytes),
        GUILayout.Width(90));

    EditorGUILayout.EndHorizontal();
}

private bool PassesFilter(AssetEntry asset)
{
    switch (filter)
    {
        case AssetFilter.Texture:
            return asset.type == typeof(Texture2D);

        case AssetFilter.Material:
            return asset.type == typeof(Material);

        case AssetFilter.Audio:
            return asset.type == typeof(AudioClip);

        case AssetFilter.Prefab:
            return asset.type == typeof(GameObject);

        case AssetFilter.FBX:
            return asset.path.EndsWith(".fbx");

        default:
            return true;
    }
}

static readonly string[] ProtectedFolders =
{
    "Assets/Settings",
    "Assets/Plugins",
    "Assets/Resources",
    "Assets/StreamingAssets",
    "Assets/AddressableAssetsData",
    "Assets/TextMesh Pro",
    "Assets/Localization",
    "Assets/URP"
};

private void Scan()
{
    unusedAssets.Clear();

    string[] allAssets =
        AssetDatabase.GetAllAssetPaths()
        .Where(x => x.StartsWith("Assets/"))
        .ToArray();

    HashSet<string> referencedAssets =
        new HashSet<string>();

    foreach (var scene in EditorBuildSettings.scenes)
    {
        if (scene.enabled)
        {
            referencedAssets.Add(scene.path);
        }
    }

    foreach (string asset in allAssets)
    {
        string[] dependencies =
            AssetDatabase.GetDependencies(
                asset,
                true);

        foreach (string dependency in dependencies)
        {
            if (dependency != asset)
            {
                referencedAssets.Add(dependency);
            }
        }
    }

    string[] guids =
        AssetDatabase.FindAssets(
            "",
            new[] { folderPath });

    var buildScenes =
            EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToHashSet();

    foreach (string guid in guids)
    {
        string assetPath =
            AssetDatabase.GUIDToAssetPath(guid);

        if (AssetDatabase.IsValidFolder(assetPath))
            continue;

        if (assetPath.EndsWith(".cs"))
            continue;

        if (assetPath.Contains("/Editor/"))
            continue;

        if (buildScenes.Contains(assetPath))
            continue;

        if (assetPath.EndsWith(".cs"))
    continue;

    if (assetPath.EndsWith(".asset"))
        continue;

    if (assetPath.EndsWith(".shader"))
        continue;

    if (assetPath.EndsWith(".shadergraph"))
        continue;

    if (assetPath.EndsWith(".shadersubgraph"))
        continue;

    if (assetPath.EndsWith(".asmdef"))
        continue;

    if (assetPath.EndsWith(".dll"))
        continue;

    if (assetPath.EndsWith(".inputactions"))
        continue;

        bool isProtected =
            ProtectedFolders.Any(assetPath.StartsWith);

        if (isProtected)
            continue;

        if (!referencedAssets.Contains(assetPath))
        {
            unusedAssets.Add(
                new AssetEntry
                {
                    path = assetPath,
                    selected = false,
                    type = AssetDatabase.GetMainAssetTypeAtPath(assetPath),
                    sizeBytes = GetFileSize(assetPath)
                });
        }
    }

    if (sortBySize)
    {
        unusedAssets =
            unusedAssets
            .OrderByDescending(x => x.sizeBytes)
            .ToList();
    }
    else
    {
        unusedAssets =
            unusedAssets
            .OrderBy(x => x.path)
            .ToList();
    }

    Debug.Log(
        $"Unused Asset Finder found {unusedAssets.Count} assets.");
}

private void DeleteSelected()
{
    List<AssetEntry> assetsToDelete =
        unusedAssets
        .Where(x => x.selected)
        .ToList();

    if (assetsToDelete.Count == 0)
        return;

    long totalSize =
        assetsToDelete.Sum(x => x.sizeBytes);

    bool confirm =
        EditorUtility.DisplayDialog(
            "Delete Assets",
            $"Delete {assetsToDelete.Count} assets?\n\n" +
            $"Total Size: {FormatSize(totalSize)}\n\n" +
            $"This cannot be undone.",
            "Delete",
            "Cancel");

    if (!confirm)
        return;

    AssetDatabase.StartAssetEditing();

    try
    {
        foreach (var asset in assetsToDelete)
        {
            AssetDatabase.DeleteAsset(asset.path);
        }
    }
    finally
    {
        AssetDatabase.StopAssetEditing();
    }

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();

    Scan();
}

private long GetFileSize(string assetPath)
{
    string fullPath =
        Path.Combine(
            Directory.GetCurrentDirectory(),
            assetPath);

    if (File.Exists(fullPath))
    {
        return new FileInfo(fullPath).Length;
    }

    return 0;
}

private string FormatSize(long bytes)
{
    float kb = bytes / 1024f;

    if (kb < 1024f)
    {
        return $"{kb:F1} KB";
    }

    return $"{(kb / 1024f):F2} MB";
}
}
