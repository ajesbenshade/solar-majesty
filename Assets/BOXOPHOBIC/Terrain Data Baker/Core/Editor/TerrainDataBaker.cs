// Cristian Pop - https://boxophobic.com/

using UnityEngine;
using UnityEditor;
using System.IO;
using Boxophobic.StyledGUI;
using System.Collections.Generic;
using Boxophobic.Utility;

namespace TerrainDataBaker
{
    public class TextureImporterData
    {
        public TextureImporterCompression textureCompression;
        public bool crunchedCompression;
        public int compressionQuality;
        public bool isReadable;
    }

    public class TerrainDataBaker : EditorWindow
    {
        const int GUI_MESH = 24;

        float GUI_HALF_EDITOR_WIDTH = 200;

        enum BakeType
        {
            ColorMap = 0,
            NormalMap = 10,
            MaskMap = 20,
        }

        enum BakeResolution
        {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
        }

        BakeResolution bakeResolution = BakeResolution._1024;

        string userFolder = "Assets/BOXOPHOBIC+";

        bool bakeColorMaps = true;
        bool bakeNormalMaps = true;
        bool bakeMaskMaps = true;

        bool baketerrainHeights = true;
        //bool bakeHolesMaps = true;
        bool bakeTerrainSplats = true;
        bool bakeTerrainNormals = true;

        bool saveHeightsAsEXRFormat = true;
        bool saveNormalsInWorldSpace = true;

        List<TerrainData> terrainObjects;
        bool showTerrainObjects = true;
        string dataPath = "";
        string latestPath = "";

        GUIStyle stylePopup;
        GUIStyle styleCenteredHelpBox;

        Color bannerColor;
        string bannerText;
        //string helpURL;
        static TerrainDataBaker window;
        Vector2 scrollPosition = Vector2.zero;

        [MenuItem("Window/BOXOPHOBIC/Terrain Data Baker/Baker", false, 1037)]
        public static void ShowWindow()
        {
            window = GetWindow<TerrainDataBaker>(false, "Terrain Data Baker", true);
            window.minSize = new Vector2(389, 220);
        }
        void OnEnable()
        {
            bannerColor = new Color(0.62f, 0.71f, 0.20f);
            bannerText = "Terrain Data Baker";
            //helpURL = "https://docs.google.com/document/d/1przDXu3yQmWUDIkTk5_J0dhGL0oOoaI2Gs6yv3Eh2HM/edit#";

            userFolder = BoxoUtils.GetUserFolder();
        }

        void OnGUI()
        {
            SetGUIStyles();

            GUI_HALF_EDITOR_WIDTH = this.position.width / 2.0f - 24;

            StyledGUI.DrawWindowBanner(bannerColor, bannerText);

            GUILayout.BeginHorizontal();
            GUILayout.Space(15);

            GUILayout.BeginVertical();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.Width(this.position.width - 28), GUILayout.Height(this.position.height - 80));

            if (terrainObjects.Count > 0)
            {
                DrawTerrainObjects();
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Button("\n<size=14>Select one or multiple terrains to get started!</size>\n", styleCenteredHelpBox);
                GUI.enabled = false;
            }

            GUILayout.Space(10);

            if (terrainObjects.Count > 1 && bakeResolution > BakeResolution._1024)
            {
                EditorGUILayout.HelpBox("Baking multiple terrains at high resolutions can be extremly slow and it can take a few minutes!", MessageType.Warning);
                GUILayout.Space(10);
            }
            else if (bakeResolution > BakeResolution._1024)
            {
                EditorGUILayout.HelpBox("Baking at high resolutions can be slow and it can take a few minutes!", MessageType.Info);
                GUILayout.Space(10);
            }
            else if (terrainObjects.Count > 1)
            {
                EditorGUILayout.HelpBox("Baking multiple terrains can be slow and it can take a few minutes!", MessageType.Info);
                GUILayout.Space(10);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bake Resolution", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
            bakeResolution = (BakeResolution)EditorGUILayout.EnumPopup(bakeResolution, stylePopup, GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bake Color Maps", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
            bakeColorMaps = EditorGUILayout.Toggle(bakeColorMaps);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bake Normal Maps", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
            bakeNormalMaps = EditorGUILayout.Toggle(bakeNormalMaps);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bake Mask Maps", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
            bakeMaskMaps = EditorGUILayout.Toggle(bakeMaskMaps);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bake Terrain Heights", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
            baketerrainHeights = EditorGUILayout.Toggle(baketerrainHeights);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bake Terrain Splats", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
            bakeTerrainSplats = EditorGUILayout.Toggle(bakeTerrainSplats);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bake Terrain Normals", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
            bakeTerrainNormals = EditorGUILayout.Toggle(bakeTerrainNormals);
            GUILayout.EndHorizontal();

            if (baketerrainHeights || bakeTerrainNormals)
            {
                GUILayout.Space(10);
            }

            if (baketerrainHeights)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Save Heights as EXR Format", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
                saveHeightsAsEXRFormat = EditorGUILayout.Toggle(saveHeightsAsEXRFormat);
                GUILayout.EndHorizontal();
            }

            if (bakeTerrainNormals)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Save Normals in World Space", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
                saveNormalsInWorldSpace = EditorGUILayout.Toggle(saveNormalsInWorldSpace);
                GUILayout.EndHorizontal();
            }

            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Bake Holes Maps", GUILayout.Width(GUI_HALF_EDITOR_WIDTH));
            //bakeHolesMaps = EditorGUILayout.Toggle(bakeHolesMaps);
            //GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("Bake Terrain Data", GUILayout.Height(24)))
            {
                GetSavePath();

                if (dataPath != "")
                {
                    if (bakeColorMaps)
                    {
                        BakeLayerData(BakeType.ColorMap);
                    }

                    if (bakeNormalMaps)
                    {
                        BakeLayerData(BakeType.NormalMap);
                    }

                    if (bakeMaskMaps)
                    {
                        BakeLayerData(BakeType.MaskMap);
                    }

                    if (baketerrainHeights)
                    {
                        BakeHeightData();
                    }

                    if (bakeTerrainNormals)
                    {
                        BakeNormalData();
                    }

                    //if (bakeHolesMaps)
                    //{
                    //    BakeHolesData();
                    //}

                    if (bakeTerrainSplats)
                    {
                        BakeSplatData();
                    }
                }
            }

            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            GUILayout.Space(13);
            GUILayout.EndHorizontal();
        }

        void OnSelectionChange()
        {
            GetTerrainObjects();

            Repaint();
        }

        void OnFocus()
        {
            GetTerrainObjects();

            Repaint();
        }

        void SetGUIStyles()
        {
            stylePopup = new GUIStyle(EditorStyles.popup)
            {
                alignment = TextAnchor.MiddleCenter
            };

            styleCenteredHelpBox = new GUIStyle(GUI.skin.GetStyle("HelpBox"))
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
            };
        }

        void DrawTerrainObjects()
        {
            if (terrainObjects.Count > 0)
            {
                if (showTerrainObjects)
                {
                    if (StyledButton("Hide Terrain Selection"))
                        showTerrainObjects = !showTerrainObjects;
                }
                else
                {
                    if (StyledButton("Show Terrain Selection"))
                        showTerrainObjects = !showTerrainObjects;
                }

                if (showTerrainObjects)
                {
                    for (int i = 0; i < Selection.gameObjects.Length; i++)
                    {
                        StyleTerrainObject(i, terrainObjects);
                    }
                }
            }
        }

        void StyleTerrainObject(int index, List<TerrainData> terrainObjects)
        {
            if (terrainObjects.Count > index)
            {
                if (EditorGUIUtility.isProSkin)
                {
                    if (GUILayout.Button("<size=10><b><color=#b7d63c>" + terrainObjects[index].name + "</color></b></size>", styleCenteredHelpBox, GUILayout.Height(GUI_MESH)))
                    {
                        EditorGUIUtility.PingObject(terrainObjects[index]);
                    }
                }
                else
                {
                    if (GUILayout.Button("<size=10><b><color=#3b5900>" + terrainObjects[index].name + "</color></b></size>", styleCenteredHelpBox, GUILayout.Height(GUI_MESH)))
                    {
                        EditorGUIUtility.PingObject(terrainObjects[index]);
                    }
                }
            }
        }

        bool StyledButton(string text)
        {
            bool value = GUILayout.Button("<b>" + text + "</b>", styleCenteredHelpBox, GUILayout.Height(GUI_MESH));

            return value;
        }

        void GetTerrainObjects()
        {
            terrainObjects = new List<TerrainData>();

            for (int i = 0; i < Selection.gameObjects.Length; i++)
            {
                var selection = Selection.gameObjects[i];

                if (selection.GetComponent<Terrain>() != null)
                {
                    var terrainComponent = selection.GetComponent<Terrain>().terrainData;

                    if (terrainComponent != null)
                    {
                        if (!terrainObjects.Contains(terrainComponent))
                        {
                            terrainObjects.Add(terrainComponent);
                        }
                    }
                }
            }
        }

        void GetSavePath()
        {
            latestPath = SettingsUtils.LoadSettingsData(userFolder + "/User/Terrain Data Baker/Latest Folder.asset", "Assets");

            if (Directory.Exists(latestPath))
            {
                dataPath = latestPath;
            }
            else
            {
                dataPath = "Assets";
            }

            dataPath = EditorUtility.OpenFolderPanel("Save Maps to Folder", dataPath, "");

            if (dataPath != "")
            {
                dataPath = "Assets" + dataPath.Substring(Application.dataPath.Length);
                SettingsUtils.SaveSettingsData(userFolder + "/User/Terrain Data Baker/Latest Folder.asset", dataPath);
            }
            else
            {
                GUIUtility.ExitGUI();
            }
        }

        void BakeLayerData(BakeType bakeType)
        {
            for (int i = 0; i < terrainObjects.Count; i++)
            {
                var terrainData = terrainObjects[i];
                var terrainLayers = terrainData.terrainLayers;
                var terrainAlphas = terrainData.alphamapTextures;
                var terrainSize = terrainData.size;

                var texWidth = (int)bakeResolution;
                var texHeight = (int)bakeResolution;

                var channel = -1;
                var splat = 0;

                Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBAFloat, false);

                for (int d = 0; d < terrainLayers.Length; d++)
                {
                    if (d > 0 && Mathf.Repeat(d, 4) < 0.01f)
                    {
                        channel = -1;
                        splat += 1;
                    }

                    channel += 1;

                    TerrainLayer layer = terrainLayers[d];
                    Texture2D activeTexture = null;

                    if (bakeType == BakeType.ColorMap)
                    {
                        activeTexture = layer.diffuseTexture;
                    }
                    else if (bakeType == BakeType.NormalMap)
                    {
                        activeTexture = layer.normalMapTexture;
                    }
                    else if (bakeType == BakeType.MaskMap)
                    {
                        activeTexture = layer.maskMapTexture;
                    }

                    TextureImporterData storedImporterData = new TextureImporterData();

                    if (activeTexture != null)
                    {
                        TextureImporter activeImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(activeTexture)) as TextureImporter;
                        storedImporterData.textureCompression = activeImporter.textureCompression;
                        storedImporterData.crunchedCompression = activeImporter.crunchedCompression;
                        storedImporterData.isReadable = activeImporter.isReadable;
                        storedImporterData.compressionQuality = activeImporter.compressionQuality;

                        activeImporter.isReadable = true;
                        activeImporter.textureCompression = TextureImporterCompression.Uncompressed;
                        activeImporter.SaveAndReimport();
                    }

                    var tillingX = 1 / layer.tileSize.x * terrainSize.x;
                    var tillingY = 1 / layer.tileSize.y * terrainSize.z;

                    for (int x = 0; x < texWidth; x++)
                    {
                        for (int y = 0; y < texHeight; y++)
                        {
                            var alpha = 1.0f;
                            var alphaRGB = terrainAlphas[splat].GetPixelBilinear((float)x / texWidth, (float)y / texHeight);

                            if (d > 0 && channel == 0)
                            {
                                alpha = alphaRGB.r;
                            }
                            else if (channel == 1)
                            {
                                alpha = alphaRGB.g;
                            }
                            else if (channel == 2)
                            {
                                alpha = alphaRGB.b;
                            }
                            else if (channel == 3)
                            {
                                alpha = alphaRGB.a;
                            }

                            Color pixel = Color.black;

                            if (activeTexture != null)
                            {
                                if (bakeType == BakeType.NormalMap)
                                {
                                    var pixG = layer.normalMapTexture.GetPixelBilinear((float)x / texWidth * tillingX, (float)y / texHeight * tillingY).g;
                                    var pixA = layer.normalMapTexture.GetPixelBilinear((float)x / texWidth * tillingX, (float)y / texHeight * tillingY).a;

                                    pixel = new Color(pixG, pixA, 1, 1);
                                }
                                else
                                {
                                    pixel = activeTexture.GetPixelBilinear((float)x / texWidth * tillingX, (float)y / texHeight * tillingY);
                                }
                            }
                            else
                            {
                                if (bakeType == BakeType.NormalMap)
                                {
                                    pixel = new Color(0.5f, 0.5f, 1, 1);
                                }
                            }

                            var output = Color.Lerp(tex.GetPixelBilinear((float)x / texWidth, (float)y / texHeight), pixel, alpha);
                            tex.SetPixel(x, y, output);
                        }
                    }

                    if (activeTexture != null)
                    {
                        TextureImporter activeImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(activeTexture)) as TextureImporter;
                        activeImporter.textureCompression = storedImporterData.textureCompression;
                        activeImporter.crunchedCompression = storedImporterData.crunchedCompression;
                        activeImporter.compressionQuality = storedImporterData.compressionQuality;
                        activeImporter.isReadable = storedImporterData.isReadable;
                        activeImporter.SaveAndReimport();
                    }
                }

                var savePath = dataPath + "/" + terrainData.name + " " + bakeType.ToString() + ".png";

                byte[] imgBytes = tex.EncodeToPNG();
                File.WriteAllBytes(savePath, imgBytes);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                TextureImporter texImporter = AssetImporter.GetAtPath(savePath) as TextureImporter;
                texImporter.maxTextureSize = (int)bakeResolution;

                if (bakeType == BakeType.NormalMap)
                {
                    texImporter.textureType = TextureImporterType.NormalMap;
                }

                texImporter.SaveAndReimport();

                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture>(savePath));
            }
        }

        void BakeHeightData()
        {
            for (int i = 0; i < terrainObjects.Count; i++)
            {
                var terrainData = terrainObjects[i];
                var terrainMaxHeight = terrainData.heightmapScale.y;

                if (saveHeightsAsEXRFormat)
                {
                    terrainMaxHeight = 1.0f;
                }

                var texWidth = (int)bakeResolution;
                var texHeight = (int)bakeResolution;

                Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBAFloat, false);

                for (int x = 0; x < texWidth; x++)
                {
                    for (int y = 0; y < texHeight; y++)
                    {
                        var output = terrainData.GetInterpolatedHeight((float)x / texWidth, (float)y / texHeight) / terrainMaxHeight;
                        tex.SetPixel(x, y, new Color(output, output, output));
                    }
                }

                string savePath = dataPath + "/" + terrainData.name + " HeightMap";

                if (saveHeightsAsEXRFormat)
                {
                    savePath = savePath + ".exr";

                    byte[] imgBytes = tex.EncodeToEXR();
                    File.WriteAllBytes(savePath, imgBytes);
                }
                else
                {
                    savePath = savePath + ".png";

                    byte[] imgBytes = tex.EncodeToPNG();
                    File.WriteAllBytes(savePath, imgBytes);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                TextureImporter texImporter = AssetImporter.GetAtPath(savePath) as TextureImporter;
                texImporter.sRGBTexture = false;
                texImporter.maxTextureSize = (int)bakeResolution;
                texImporter.SaveAndReimport();

                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture>(savePath));
            }
        }

        void BakeHolesData()
        {
            for (int i = 0; i < terrainObjects.Count; i++)
            {
                var terrainData = terrainObjects[i];
                var terrainHoles = (Texture2D)terrainData.holesTexture;

                var texWidth = (int)bakeResolution;
                var texHeight = (int)bakeResolution;

                Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBAFloat, false);

                for (int x = 0; x < texWidth; x++)
                {
                    for (int y = 0; y < texHeight; y++)
                    {
                        var output = terrainHoles.GetPixelBilinear((float)x / texWidth, (float)y / texHeight).r;
                        tex.SetPixel(x, y, new Color(output, output, output));
                    }
                }

                var savePath = dataPath + "/" + terrainData.name + " HolesMap" + ".png";

                byte[] imgBytes = tex.EncodeToPNG();
                File.WriteAllBytes(savePath, imgBytes);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                TextureImporter texImporter = AssetImporter.GetAtPath(savePath) as TextureImporter;
                texImporter.maxTextureSize = (int)bakeResolution;
                texImporter.SaveAndReimport();

                AssetDatabase.Refresh();
            }
        }

        void BakeSplatData()
        {
            for (int i = 0; i < terrainObjects.Count; i++)
            {
                var terrainData = terrainObjects[i];
                var terrainAlphas = terrainData.alphamapTextures;

                // Some magic number to get the correct bilinear pixel!?
                //var xOffset = 0.985f;
                //var yOffset = 0.985f;

                for (int d = 0; d < terrainAlphas.Length; d++)
                {
                    var texWidth = (int)bakeResolution;
                    var texHeight = (int)bakeResolution;

                    Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBAFloat, false);

                    for (int x = 0; x < texWidth; x++)
                    {
                        for (int y = 0; y < texHeight; y++)
                        {
                            var output = terrainAlphas[d].GetPixelBilinear((float)x / texWidth, (float)y / texHeight);
                            tex.SetPixel(x, y, output);
                        }
                    }

                    var savePath = dataPath + "/" + terrainData.name + " SplatMap " + d + ".png";

                    byte[] imgBytes = tex.EncodeToPNG();
                    File.WriteAllBytes(savePath, imgBytes);

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    TextureImporter texImporter = AssetImporter.GetAtPath(savePath) as TextureImporter;
                    texImporter.maxTextureSize = (int)bakeResolution;
                    texImporter.SaveAndReimport();

                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture>(savePath));
                }
            }
        }

        void BakeNormalData()
        {
            for (int i = 0; i < terrainObjects.Count; i++)
            {
                var terrainData = terrainObjects[i];

                var texWidth = (int)bakeResolution;
                var texHeight = (int)bakeResolution;

                Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBAFloat, false);

                if (saveNormalsInWorldSpace)
                {
                    for (int x = 0; x < texWidth; x++)
                    {
                        for (int y = 0; y < texHeight; y++)
                        {
                            var output = terrainData.GetInterpolatedNormal((float)x / texWidth, (float)y / texHeight);
                            tex.SetPixel(x, y, new Color(output.x * 0.5f + 0.5f, 1.0f, output.z * 0.5f + 0.5f));
                        }
                    }
                }
                else
                {
                    for (int x = 0; x < texWidth; x++)
                    {
                        for (int y = 0; y < texHeight; y++)
                        {
                            var output = terrainData.GetInterpolatedNormal((float)x / texWidth, (float)y / texHeight);
                            tex.SetPixel(x, y, new Color(output.x * 0.5f + 0.5f, output.z * 0.5f + 0.5f, 1.0f));
                        }
                    }
                }

                var savePath = dataPath + "/" + terrainData.name + " NormalMap" + ".png";

                byte[] imgBytes = tex.EncodeToPNG();
                File.WriteAllBytes(savePath, imgBytes);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                TextureImporter texImporter = AssetImporter.GetAtPath(savePath) as TextureImporter;

                if (saveNormalsInWorldSpace)
                {
                    texImporter.textureType = TextureImporterType.Default;
                }
                else
                {
                    texImporter.textureType = TextureImporterType.NormalMap;
                }                

                texImporter.sRGBTexture = false;
                texImporter.maxTextureSize = (int)bakeResolution;
                texImporter.SaveAndReimport();

                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture>(savePath));
            }
        }
    }
}
