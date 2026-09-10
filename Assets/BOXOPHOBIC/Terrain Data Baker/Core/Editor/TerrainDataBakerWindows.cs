using UnityEditor;
using UnityEngine;

namespace TerrainDataBaker
{
    public static class TerrainDataBakerWindows
    {
        [MenuItem("Window/BOXOPHOBIC/Terrain Data Baker/Discord Server", false, 8000)]
        public static void Discord()
        {
            Application.OpenURL("https://discord.com/invite/znxuXET");
        }

        [MenuItem("Window/BOXOPHOBIC/Terrain Data Baker/Publisher Page", false, 8001)]
        public static void MoreAssets()
        {
            Application.OpenURL("https://assetstore.unity.com/publishers/20529");
        }

        [MenuItem("Window/BOXOPHOBIC/Terrain Data Baker/Documentation", false, 8002)]
        public static void Documentation()
        {
            Application.OpenURL("https://docs.google.com/document/d/1przDXu3yQmWUDIkTk5_J0dhGL0oOoaI2Gs6yv3Eh2HM/edit#");
        }

        [MenuItem("Window/BOXOPHOBIC/Terrain Data Baker/Changelog", false, 8003)]
        public static void Changelog()
        {
            Application.OpenURL("https://docs.google.com/document/d/1przDXu3yQmWUDIkTk5_J0dhGL0oOoaI2Gs6yv3Eh2HM/edit#heading=h.1rbujejuzjce");
        }

        [MenuItem("Window/BOXOPHOBIC/Terrain Data Baker/Write A Review", false, 9999)]
        public static void WriteAReview()
        {
            //Application.OpenURL("");
        }
    }
}


