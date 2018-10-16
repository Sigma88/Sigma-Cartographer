using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace SigmaCartographerPlugin
{
    public static class PlanetRenderer
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
        static string exportFolder = "";
        static bool oceanFloor = false;
        static Texture texture = new Texture();
        static Color background = new Color(0, 0, 0, 0);
        static double accuracy = 3;


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

            if (!TryParse.Color(node.GetValue("backgroundColor"), out background))
            {
                background = new Color(0, 0, 0, 0);
            }

            if (!double.TryParse(node.GetValue("accuracy"), out accuracy))
            {
                accuracy = 3;
            }

            if (!body.ocean)
            {
                oceanFloor = true;
            }


            string[] sources = { "heightMap", "satelliteHeight", "normalMap", "slopeMap", "satelliteSlope", "colorMap", "satelliteMap", "oceanMap", "satelliteBiome" };

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
                    node.RemoveValues("width");
                    node.RemoveValues("tile");
                    node.RemoveValues("leaflet");
                    node.RemoveValues("flipV");
                    node.RemoveValues("flipH");
                    node.RemoveValues("LAToffset");
                    node.RemoveValues("LONoffset");
                    node.RemoveValues("printTile");
                    node.RemoveValues("printFrom");
                    node.RemoveValues("printTo");

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

                    node.AddValue("width", size);
                    node.AddValue("tile", size);
                    node.AddValue("LONoffset", "-90");
                    node.AddValue("flipH", "true");

                    settings = node;
                    break;
                }
            }
        }

        internal static void GetTexture()
        {
            if (source.StartsWith("FILEPATH/"))
            {
                texture.Set(Utility.CreateReadable(Resources.FindObjectsOfTypeAll<Texture2D>().FirstOrDefault(t => t.name == source.Substring(9))));
            }
            else if (source.StartsWith("MATERIAL/"))
            {
                string type = source.Substring(9);
                Texture2D main = null;
                Texture2D bump = null;

                if (type == "MainTexture" || type == "ScaledVersion")
                    main = Utility.CreateReadable(body.scaledBody?.GetComponent<Renderer>()?.material?.GetTexture("_MainTex") as Texture2D);
                if (type == "NormalMap" || type == "ScaledVersion")
                    bump = Utility.CreateReadable(body.scaledBody?.GetComponent<Renderer>()?.material?.GetTexture("_BumpMap") as Texture2D);

                if (type == "MainTexture")
                    texture.Set(main);
                else if (type == "NormalMap")
                    texture.Set(bump);
                else if (type == "ScaledVersion")
                {
                    MapGenerator.CreateSatelliteMap(ref main, ref bump, ref main);
                    texture.Set(main);
                }
            }
            else if (source == "biomeMap")
            {
                texture.Set(body.BiomeMap);
            }
            else if (settings != null)
            {
                MapGenerator.LoadSettings(settings);
                MapGenerator.GeneratePQSMaps("Render/");

                switch (source)
                {
                    case "heightMap": texture.Set(MapGenerator.heightMap); break;
                    case "normalMap": texture.Set(MapGenerator.normalMap); break;
                    case "slopeMap": texture.Set(MapGenerator.slopeMap); break;
                    case "colorMap": texture.Set(MapGenerator.colorMap); break;
                    case "satelliteMap": texture.Set(MapGenerator.satelliteMap); break;
                    case "oceanMap": texture.Set(MapGenerator.oceanMap); break;
                    case "satelliteHeight":
                    case "satelliteSlope":
                    case "satelliteBiome": texture.Set(MapGenerator.satelliteMap); break;
                }
            }
        }

        internal static void RenderPlanet()
        {
            if (size < 3) return;
            Texture2D picture = new Texture2D(size, size);
            size -= 2;

            Clear(picture);

            for (int x = 0; x < size * 2 * accuracy; x++)
            {
                for (int y = 0; y < size * accuracy; y++)
                {
                    // Calculate Lat & Lon
                    double lat = y * 180d / (size * accuracy) - 90d;
                    double lon = x * 360d / (size * 2d * accuracy) - 180d;

                    // Get position and plane
                    Vector3 position = LatLon.GetRelSurfaceNVector(lat, lon).normalized;
                    Vector3 plane = LatLon.GetRelSurfaceNVector(LAToffset, LONoffset).normalized;

                    // If the position is not visible end here
                    if (Vector3.Angle(plane, position) > 90) continue;

                    // Rotate the position
                    position = Quaternion.AngleAxis((float)LONoffset, Vector3.up) * position;
                    position = Quaternion.AngleAxis((float)LAToffset, Vector3.back) * position;

                    // Get the altitude
                    double altitude = 1;
                    if (body.pqsController != null)
                    {
                        altitude = body.Radius + body.TerrainAltitude(lat, lon, oceanFloor);
                        altitude /= GetMaxAltitude();
                    }
                    position *= (float)altitude;

                    // From -1 to 1
                    double px = position.z;
                    double py = position.y;

                    // From 0 to size
                    px = (px + 1) / 2 * size;
                    py = (py + 1) / 2 * size;

                    // Get the color
                    Color color = texture.GetColor(lat, lon);
                    color.a = 1;

                    // Apply the color
                    picture.SetPixel((int)Math.Round(px) + 1, (int)Math.Round(py) + 1, color);
                }
            }

            // Export
            Print(picture, "shape");
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

        static void Clear(Texture2D thumb)
        {
            for (int x = 0; x < thumb.width; x++)
            {
                for (int y = 0; y < thumb.height; y++)
                {
                    thumb.SetPixel(x, y, background);
                }
            }
        }

        static void Print(Texture2D texture, string name)
        {
            Debug.Log("SigmaLog: Thumbnail completed");
            byte[] png = texture.EncodeToPNG();
            Directory.CreateDirectory("GameData/Sigma/Cartographer/Assets/Sprites/");
            File.WriteAllBytes("GameData/Sigma/Cartographer/Assets/Sprites/" + body.transform.name + "_" + name + ".png", png);
            Debug.Log("SigmaLog: Thumbnail saved");
        }
    }

    internal class Texture
    {
        // Textures
        Texture2D texture = null;
        CBAttributeMapSO MapSO = null;


        // Set Textures
        internal void Set(Texture2D texture)
        {
            this.texture = texture;
        }

        internal void Set(CBAttributeMapSO MapSO)
        {
            this.MapSO = MapSO;
        }


        // Check Textures
        internal bool valid()
        {
            return (texture != null || MapSO != null);
        }


        // Read Textures
        internal Color GetColor(double lat, double lon)
        {
            Color color = new Color(0, 0, 0, 0);

            if (texture != null)
            {
                int tx = (int)(((((lon + 90) % 360) + 360) % 360) / 360d * texture.width);
                int ty = (int)((Math.Asin(Math.Sin(lat * Math.PI / 180d)) / Math.PI + 0.5d) * texture.height);//(int)((lat + 90) / 180d * texture.height);//

                color = texture.GetPixel(tx, ty);
            }
            else if (MapSO != null)
            {
                color = MapSO.GetAtt(lat * Math.PI / 180d, lon * Math.PI / 180d).mapColor;
            }

            return color;
        }


        // Create new Texture
        internal Texture()
        {
        }

        internal Texture(Texture2D texture)
        {
            this.texture = texture;
        }

        internal Texture(CBAttributeMapSO MapSO)
        {
            this.MapSO = MapSO;
        }
    }
}
