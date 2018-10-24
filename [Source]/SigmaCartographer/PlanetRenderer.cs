using System.IO;
using System.Linq;
using UnityEngine;


namespace SigmaCartographerPlugin
{
    internal static class PlanetRenderer
    {
        static ConfigNode settings = null;
        static CelestialBody body
        {
            get { return MapGenerator.body; }
            set { MapGenerator.body = value; }
        }
        static string source = "";
        static int size = 2048;
        static double LONoffset = 0;
        static double LAToffset = 0;
        static double lightLAT = 0;
        static double lightLON = 0;
        static string exportFolder = "";
        static bool oceanFloor = false;
        static Color background = Color.clear;
        static string name = "";

        static bool unlit = false;
        static Color? _Color = null;
        static Color? _SpecColor = null;
        static float? _Shininess = null;


        internal static void LoadSettings(ConfigNode node)
        {
            settings = null;

            if (!node.HasValue("texture")) return;

            string bodyName = node.GetValue("body");
            body = FlightGlobals.Bodies.FirstOrDefault(b => b.name == bodyName) ?? FlightGlobals.GetHomeBody();

            source = node.GetValue("texture");

            if (!int.TryParse(node.GetValue("size"), out size))
            {
                size = 2048;
            }

            TryParse.Path(node.GetValue("exportFolder"), out exportFolder);

            name = node.GetValue("name");

            if (!bool.TryParse(node.GetValue("oceanFloor"), out oceanFloor))
            {
                oceanFloor = true;
            }

            if (!double.TryParse(node.GetValue("LAToffset"), out LAToffset))
            {
                LAToffset = 0;
            }

            if (!double.TryParse(node.GetValue("LONoffset"), out LONoffset))
            {
                LONoffset = 0;
            }

            if (!double.TryParse(node.GetValue("lightLAT"), out lightLAT))
            {
                lightLAT = 0;
            }

            if (!double.TryParse(node.GetValue("lightLON"), out lightLON))
            {
                lightLON = 0;
            }

            if (!TryParse.Color(node.GetValue("backgroundColor"), out background))
            {
                background = Color.clear;
            }

            bool.TryParse(node.GetValue("unlit"), out unlit);

            TryParse.Color(node.GetValue("_Color"), out _Color);

            TryParse.Color(node.GetValue("_SpecColor"), out _SpecColor);

            if (float.TryParse(node.GetValue("_Shininess"), out float shininess))
                _Shininess = shininess;
            else
                _Shininess = null;

            if (!body.ocean)
            {
                oceanFloor = true;
            }


            string[] sources = { "heightMap", "satelliteHeight", "normalMap", "slopeMap", "satelliteSlope", "colorMap", "satelliteMap", "oceanMap", "biomeMap", "satelliteBiome" };

            for (int i = 0; i < sources.Length; i++)
            {
                if (source == sources[i])
                {
                    // Clean node
                    node.RemoveValues("heightMap");
                    node.RemoveValues("satelliteHeight");
                    node.RemoveValues("normalMap");
                    node.RemoveValues("slopeMap");
                    node.RemoveValues("satelliteSlope");
                    node.RemoveValues("colorMap");
                    node.RemoveValues("satelliteMap");
                    node.RemoveValues("oceanMap");
                    node.RemoveValues("biomeMap");
                    node.RemoveValues("satelliteBiome");
                    node.RemoveValues("leaflet");
                    node.RemoveValues("flipV");
                    node.RemoveValues("flipH");
                    node.RemoveValues("LAToffset");
                    node.RemoveValues("LONoffset");
                    node.RemoveValues("printTile");
                    node.RemoveValues("printFrom");
                    node.RemoveValues("printTo");

                    if (double.TryParse(node.GetValue("width"), out double width))
                    {
                        node.RemoveValues("tile");
                        node.AddValue("tile", width);
                    }
                    else
                    {
                        node.RemoveValues("width");
                        node.RemoveValues("tile");
                        node.AddValue("width", size);
                        node.AddValue("tile", size);
                    }

                    // Failsafe
                    node.AddValue("colorMap", "false");

                    // Checks
                    if (body.pqsController == null)
                    {
                        return;
                    }
                    if (source == "oceanMap" && !body.ocean)
                    {
                        return;
                    }
                    if (source == "satelliteBiome" && body.BiomeMap == null)
                    {
                        return;
                    }

                    if (source == "colorMap")
                        node.RemoveValues("colorMap");
                    else
                        node.AddValue(source, "true");

                    node.AddValue("LONoffset", "-90");
                    node.AddValue("flipH", "true");

                    settings = node;
                    break;
                }
            }
        }

        internal static void RenderPlanet()
        {
            PreviewGenerator.BackgroundColor = background;
            PreviewGenerator.LAToffset = LAToffset;
            PreviewGenerator.LONoffset = LONoffset;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.GetComponentInChildren<MeshFilter>().sharedMesh =
                body.scaledBody.GetComponent<MeshFilter>().sharedMesh;

            // Renderer
            MeshRenderer renderer = sphere.GetComponentInChildren<MeshRenderer>();

            // Material
            renderer.sharedMaterial = body.scaledBody.GetComponent<MeshRenderer>().sharedMaterial;

            // Shader
            if (renderer.sharedMaterial.shader.name.StartsWith("Terrain/"))
                renderer.sharedMaterial.shader = Shader.Find("Terrain/Scaled Planet (Simple)");

            // Setup Material
            if (_Color != null)
                renderer.sharedMaterial.SetColor("_Color", (Color)_Color);
            if (_SpecColor != null)
                renderer.sharedMaterial.SetColor("_SpecColor", (Color)_SpecColor);
            if (_Shininess != null)
                renderer.sharedMaterial.SetFloat("_Shininess", (float)_Shininess);

            // Get Texture
            if (GetTextures(ref renderer))
            {
                // Lighting
                if (unlit && renderer.sharedMaterial.HasProperty("_ResourceMap"))
                {
                    renderer.sharedMaterial.SetTexture("_ResourceMap", renderer.sharedMaterial.GetTexture("_MainTex"));
                    renderer.sharedMaterial.SetTexture("_MainTex", Utility.black);
                }

                // Generate
                Texture2D finalImage = PreviewGenerator.GenerateModelPreview(sphere.transform, size, size, lightLAT, lightLON);

                // Export
                ExportImage(ref finalImage, "Render/", (!string.IsNullOrEmpty(name) ? name + "/" : "") + "Image.png");
                Object.DestroyImmediate(finalImage);
            }

            // CleanUp
            Object.DestroyImmediate(sphere);
        }

        static bool GetTextures(ref MeshRenderer renderer)
        {
            if (source.StartsWith("FILEPATH/"))
            {
                Texture texture = Resources.FindObjectsOfTypeAll<Texture>().FirstOrDefault(t => t.name == source.Substring(9));
                if (texture == null) return false;

                renderer.sharedMaterial.SetTexture("_MainTex", texture);
                return true;
            }
            else if (source.StartsWith("INTERNAL/"))
            {
                if (source == "INTERNAL/satelliteMap")
                {
                    return true;
                }

                if (source == "INTERNAL/colorMap")
                {
                    renderer.sharedMaterial.SetTexture("_BumpMap", null);
                    return true;
                }

                if (body.BiomeMap != null)
                {
                    if (source == "INTERNAL/biomeMap" || source == "INTERNAL/satelliteBiome")
                    {
                        if (source == "INTERNAL/biomeMap")
                            renderer.sharedMaterial.SetTexture("_BumpMap", null);

                        renderer.sharedMaterial.SetTexture("_MainTex", body.BiomeMap.CompileToTexture());

                        return true;
                    }
                }

                return false;
            }
            else if (settings != null)
            {
                MapGenerator.LoadSettings(settings);
                MapGenerator.GeneratePQSMaps("Render/" + (!string.IsNullOrEmpty(name) ? name + "/" : ""), true);

                // MainTex
                switch (source)
                {
                    case "heightMap":
                    case "satelliteHeight": renderer.sharedMaterial.SetTexture("_MainTex", MapGenerator.heightMap); break;
                    case "normalMap": renderer.sharedMaterial.SetTexture("_MainTex", MapGenerator.normalMap); break;
                    case "slopeMap":
                    case "satelliteSlope": renderer.sharedMaterial.SetTexture("_MainTex", MapGenerator.slopeMap); break;
                    case "colorMap":
                    case "satelliteMap": renderer.sharedMaterial.SetTexture("_MainTex", MapGenerator.colorMap); break;
                    case "oceanMap": renderer.sharedMaterial.SetTexture("_MainTex", MapGenerator.oceanMap); break;
                    case "biomeMap":
                    case "satelliteBiome": renderer.sharedMaterial.SetTexture("_MainTex", MapGenerator.biomeMap); break;
                    default: return false;
                }

                // BumpMap
                switch (source)
                {
                    case "heightMap":
                    case "normalMap":
                    case "slopeMap":
                    case "colorMap":
                    case "oceanMap":
                    case "biomeMap": renderer.sharedMaterial.SetTexture("_BumpMap", null); break;
                    case "satelliteHeight":
                    case "satelliteSlope":
                    case "satelliteMap":
                    case "satelliteBiome": renderer.sharedMaterial.SetTexture("_BumpMap", MapGenerator.normalMap); break;
                }

                return true;
            }

            return false;
        }

        static double GetMaxAltitude()
        {
            double maxAltitude = body.pqsController.radiusMax;
            foreach (PQSMod_VertexHeightOblate pqs in body.GetComponentsInChildren<PQSMod_VertexHeightOblate>())
            {
                maxAltitude += pqs.height;
            }
            return maxAltitude;
        }

        static void Clear(Texture2D texture)
        {
            for (int x = 0; x < texture.width; x++)
            {
                for (int y = 0; y < texture.height; y++)
                {
                    texture.SetPixel(x, y, background);
                }
            }
        }

        static void ExportImage(ref Texture2D texture, string folder, string fileName)
        {
            Directory.CreateDirectory(exportFolder + folder);
            File.WriteAllBytes(exportFolder + folder + fileName, texture.EncodeToPNG());
        }
    }
}
