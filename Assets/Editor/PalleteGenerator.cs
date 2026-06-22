using System.IO;
using UnityEditor;
using UnityEngine;

public static class PaletteGenerator
{
    [MenuItem("Tools/Generate SpaceWestern Palette")]
    public static void GeneratePalette()
    {
        string[] hexColors =
        {
            "#0C0A0D","#171419","#26222B","#403C48",
            "#5A5860","#797680","#A09CA8","#D4D0D8",

            "#55151A","#832024","#B92D2D","#E14B3A",

            "#66311B","#9A4A20","#D96C2B","#F0A03B",

            "#6B531E","#A17A24","#D6AA34","#FFD85A",

            "#5E4730","#856446","#B58C61","#E2C29A",

            "#233321","#375236","#507A4E","#82B47A",

            "#163B2C","#1D6848","#2BA56B","#64F0A1",

            "#1C2A47","#284373","#3C6AB0","#6CA8FF",

            "#132034","#203A5A","#2F5C8C","#5892D4",

            "#2A183B","#4A2B69","#6D4BA8","#9D7AE0",

            "#552539","#833452","#B84B74","#F38EB5",

            "#35251D","#5A3D2F","#87604A","#B98B68",

            "#5C341D","#8A4C25","#BC7134","#E2AA61",

            "#15333A","#1D5D66","#2F9AA8","#7AE6F5",

            "#C5BEC4","#D8D1D5","#E8E2E5","#F7F1F4"
        };

        int width = 8;
        int height = 7;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int i = 0; i < hexColors.Length; i++)
        {
            int x = i % width;
            int y = height - 1 - (i / width);

            if (ColorUtility.TryParseHtmlString(hexColors[i], out Color color))
            {
                tex.SetPixel(x, y, color);
            }
        }

        tex.filterMode = FilterMode.Point;
        tex.Apply();

        string path = Path.Combine(Application.dataPath, "SpaceWesternPalette.png");

        File.WriteAllBytes(path, tex.EncodeToPNG());

        AssetDatabase.Refresh();

        Debug.Log("Palette generated at: " + path);
    }
}