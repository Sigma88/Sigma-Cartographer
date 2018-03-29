using System;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;


namespace SigmaRandomPlugin
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    class MapExporter : MonoBehaviour
    {
        static bool exportAny
        {
            get { return exportHeightMap || exportNormalMap || exportSlopeMap || exportColorMap || exportSatelliteMap || exportOceanMap; }
            set { exportHeightMap = exportNormalMap = exportSlopeMap = exportColorMap = exportSatelliteMap = exportOceanMap = value; }
        }

        static bool exportHeightMap = false;
        static bool exportNormalMap = false;
        static bool exportSlopeMap = false;
        static bool exportColorMap = true;
        static bool exportOceanMap = false;
        static bool exportSatelliteMap = false;
        static bool exportBiomeMap = false;

        internal static CelestialBody body;
        static int width = 2048;
        static int tile = 1024;
        static string exportFolder = "";
        static bool leaflet = false;

        static bool oceanFloor = true;
        static double LAToffset = 0;
        static double LONoffset = 0;
        static Dictionary<double, Color> altitudeColor = null;
        static float normalStrength = 1;
        Color slopeMin = new Color(0.2f, 0.3f, 0.4f);
        Color slopeMax = new Color(0.9f, 0.6f, 0.5f);
        static List<int> printTile = new List<int>();
        static int? printFrom = null;
        static int? printTo = null;

        static int current = 0;

        void Start()
        {
            UrlDir.UrlConfig[] nodes = GameDatabase.Instance?.GetConfigs("SigmaCartographer");

            foreach (var node in nodes)
            {
                foreach (var planetInfo in node.config.GetNodes("Maps"))
                {
                    bool.TryParse(planetInfo.GetValue("heightMap"), out exportHeightMap);

                    bool.TryParse(planetInfo.GetValue("normalMap"), out exportNormalMap);

                    bool.TryParse(planetInfo.GetValue("slopeMap"), out exportSlopeMap);

                    if (!bool.TryParse(planetInfo.GetValue("colorMap"), out exportColorMap))
                    {
                        exportColorMap = true;
                    }

                    bool.TryParse(planetInfo.GetValue("oceanMap"), out exportOceanMap);

                    bool.TryParse(planetInfo.GetValue("satelliteMap"), out exportSatelliteMap);

                    bool.TryParse(planetInfo.GetValue("biomeMap"), out exportBiomeMap);

                    if (!exportAny && !exportBiomeMap) continue;

                    string bodyName = planetInfo.GetValue("body");
                    body = FlightGlobals.Bodies.FirstOrDefault(b => b.name == bodyName) ?? FlightGlobals.GetHomeBody();

                    if (!int.TryParse(planetInfo.GetValue("width"), out width))
                    {
                        width = 2048;
                    }

                    if (!int.TryParse(planetInfo.GetValue("tile"), out tile))
                    {
                        tile = 1024;
                    }

                    TryParse.Path(planetInfo.GetValue("exportFolder"), out exportFolder);

                    bool.TryParse(planetInfo.GetValue("leaflet"), out leaflet);

                    if (!bool.TryParse(planetInfo.GetValue("oceanFloor"), out oceanFloor))
                    {
                        oceanFloor = true;
                    }

                    if (!double.TryParse(planetInfo.GetValue("LAToffset"), out LAToffset))
                    {
                        LAToffset = 0;
                    }

                    if (!double.TryParse(planetInfo.GetValue("LONoffset"), out LONoffset))
                    {
                        LONoffset = 0;
                    }

                    if (planetInfo.HasNode("AltitudeColor"))
                    {
                        altitudeColor = Parse(planetInfo.GetNode("AltitudeColor"), altitudeColor);
                    }

                    if (altitudeColor == null)
                    {
                        altitudeColor = new Dictionary<double, Color>
                        {
                            { 0, Color.black },
                            { 1, Color.white }
                        };
                    }

                    if (!float.TryParse(planetInfo.GetValue("normalStrength"), out normalStrength))
                    {
                        normalStrength = 1;
                    }

                    if (!TryParse.Color(planetInfo.GetValue("slopeMin"), out slopeMin))
                    {
                        slopeMin = new Color(0.2f, 0.3f, 0.4f);
                    }

                    if (!TryParse.Color(planetInfo.GetValue("slopeMax"), out slopeMax))
                    {
                        slopeMax = new Color(0.9f, 0.6f, 0.5f);
                    }

                    printTile = new List<int>();

                    foreach (string s in planetInfo.GetValues("printTile"))
                    {
                        int i = 0;
                        if (int.TryParse(s, out i))
                        {
                            printTile.Add(i);
                        }
                    }

                    int temp = 0;

                    if (int.TryParse(planetInfo.GetValue("printFrom"), out temp))
                    {
                        printFrom = temp;
                    }
                    else
                    {
                        printFrom = null;
                    }

                    if (int.TryParse(planetInfo.GetValue("printTo"), out temp))
                    {
                        printTo = temp;
                    }
                    else
                    {
                        printTo = null;
                    }

                    if (!body.ocean)
                    {
                        oceanFloor = true;
                        exportOceanMap = false;
                    }

                    if (body.pqsController == null)
                    {
                        exportAny = false;
                    }

                    if (body.BiomeMap == null)
                    {
                        exportBiomeMap = false;
                    }

                    if (!exportAny && !exportBiomeMap) continue;

                    GeneratePQSMaps();
                }
            }
        }

        void GeneratePQSMaps()
        {
            // Textures
            Texture2D heightMap = new Texture2D(tile, tile, TextureFormat.ARGB32, true);
            Texture2D normalMap = new Texture2D(tile, tile, TextureFormat.ARGB32, true);
            Texture2D slopeMap = new Texture2D(tile, tile, TextureFormat.RGB24, true);
            Texture2D colorMap = new Texture2D(tile, tile, TextureFormat.ARGB32, true);
            Texture2D oceanMap = new Texture2D(tile, tile, TextureFormat.ARGB32, true);
            Texture2D satelliteMap = new Texture2D(tile, tile, TextureFormat.RGB24, true);
            Texture2D biomeMap = new Texture2D(tile, tile, TextureFormat.RGB24, true);

            // Arrays
            double[] terrainHeightValues = new double[(tile + 2) * (tile + 2)];
            Color[] heightMapValues = new Color[tile * tile];
            Color[] colorMapValues = new Color[tile * tile];
            Color[] oceanMapValues = new Color[tile * tile];
            Color[] biomeMapValues = new Color[tile * tile];

            // Edges
            int? firstY = null;
            int? lastY = null;


            // reset current
            current = 0;

            // Get PQS
            PQS pqs = null;

            if (exportAny)
            {
                pqs = body.pqsController;
                pqs.isBuildingMaps = true;
                pqs.isFakeBuild = true;
            }

            // Get the mods
            Action<PQS.VertexBuildData> modOnVertexBuildHeight = (Action<PQS.VertexBuildData>)Delegate.CreateDelegate(
                typeof(Action<PQS.VertexBuildData>),
                pqs,
                typeof(PQS).GetMethod("Mod_OnVertexBuildHeight", BindingFlags.Instance | BindingFlags.NonPublic));
            Action<PQS.VertexBuildData> modOnVertexBuild = (Action<PQS.VertexBuildData>)Delegate.CreateDelegate(
                typeof(Action<PQS.VertexBuildData>),
                pqs,
                typeof(PQS).GetMethod("Mod_OnVertexBuild", BindingFlags.Instance | BindingFlags.NonPublic));

            for (int j = width / 2 - tile; j >= 0; j -= tile)
            {
                for (int i = 0; i < width; i += tile)
                {
                    if (Print(current))
                    {
                        // Loop through the pixels
                        for (int y = -1; y < tile + 1; y++)
                        {
                            for (int x = -1; x < tile + 1; x++)
                            {
                                // Longitude
                                double lon = ((i + x) * 360d / width + LONoffset) % 360d - 180d;

                                // Latitude
                                double lat = ((j + y) * 360d / width - LAToffset) % 180d - 90d;
                                if (lat < -90) lat += 180;

                                // Export
                                if (exportAny)
                                {
                                    // Create a VertexBuildData
                                    PQS.VertexBuildData data = new PQS.VertexBuildData
                                    {
                                        directionFromCenter = body.GetRelSurfaceNVector(lat, lon).normalized,
                                        vertHeight = pqs.radius
                                    };

                                    // Build from the Mods 
                                    modOnVertexBuildHeight(data);

                                    if (exportHeightMap || exportNormalMap || exportSlopeMap || exportSatelliteMap)
                                    {
                                        // Adjust the height
                                        double height = (data.vertHeight - pqs.radiusMin) / pqs.radiusDelta;

                                        if (!oceanFloor & data.vertHeight < pqs.radius)
                                            height = (pqs.radius - pqs.radiusMin) / pqs.radiusDelta;

                                        if (height < 0)
                                            height = 0;
                                        else if (height > 1)
                                            height = 1;

                                        // Set the Pixels
                                        if (exportHeightMap && x > -1 && y > -1 && x < tile && y < tile)
                                        {
                                            Color color = Color.black;

                                            for (int k = 0; k < altitudeColor?.Count; k++)
                                            {
                                                KeyValuePair<double, Color> element1 = altitudeColor.ElementAt(k);

                                                if (k == altitudeColor?.Count - 1)
                                                {
                                                    color = element1.Value;
                                                }
                                                else
                                                {
                                                    KeyValuePair<double, Color> element2 = altitudeColor.ElementAt(k + 1);

                                                    if (element2.Key > height)
                                                    {
                                                        color = Color.Lerp(element1.Value, element2.Value, (float)((height - element1.Key) / (element2.Key - element1.Key)));
                                                        break;
                                                    }
                                                }
                                            }

                                            heightMapValues[(y * tile) + x] = color;
                                        }

                                        if (exportNormalMap || exportSlopeMap || exportSatelliteMap)
                                        {
                                            // Find First and last Latitude pixels
                                            double latN = ((j + y - 1) * 360d / width - LAToffset) % 180d - 90d;
                                            if (latN < -90) latN += 180;

                                            double latP = ((j + y + 1) * 360d / width - LAToffset) % 180d - 90d;
                                            if (latP < -90) latP += 180;

                                            if (latN - lat > 90)
                                                firstY = y;

                                            if (lat - latP > 90)
                                                lastY = y;

                                            terrainHeightValues[((y + 1) * (tile + 2)) + (x + 1)] = height * pqs.radiusDelta;
                                        }
                                    }

                                    if ((exportColorMap || exportSatelliteMap) && x > -1 && y > -1 && x < tile && y < tile)
                                    {
                                        modOnVertexBuild(data);

                                        // Adjust the Color
                                        Color color = data.vertColor.A(1f);
                                        if (!oceanFloor && data.vertHeight < pqs.radius)
                                            color = pqs.mapOceanColor.A(1f);

                                        // Set the Pixels
                                        colorMapValues[(y * tile) + x] = color;
                                    }

                                    if (body.ocean && (exportOceanMap || exportSatelliteMap) && x > -1 && y > -1 && x < tile && y < tile)
                                    {
                                        // Adjust the Color
                                        Color color = data.vertHeight < pqs.radius ? pqs.mapOceanColor.A(1f) : new Color(0, 0, 0, 0);

                                        // Set the Pixels
                                        oceanMapValues[(y * tile) + x] = color;
                                    }
                                }

                                if (exportBiomeMap && x > -1 && y > -1 && x < tile && y < tile)
                                {
                                    CBAttributeMapSO.MapAttribute biome = body.BiomeMap.GetAtt(lat * Math.PI / 180d, lon * Math.PI / 180d);
                                    Color color = biome.mapColor;
                                    biomeMapValues[(y * tile) + x] = color;
                                }
                            }
                        }

                        // Apply the maps
                        if (exportHeightMap)
                            heightMap.SetPixels(heightMapValues);
                        if (exportNormalMap || exportSlopeMap || exportSatelliteMap)
                            CalculateSlope(terrainHeightValues, pqs, firstY, lastY, ref normalMap, ref slopeMap);
                        if (exportColorMap)
                            colorMap.SetPixels(colorMapValues);
                        if (exportOceanMap)
                            oceanMap.SetPixels(oceanMapValues);
                        if (exportSatelliteMap)
                            CreateSatelliteMap(colorMapValues, oceanMapValues, ref normalMap, ref satelliteMap);
                        if (exportBiomeMap)
                            biomeMap.SetPixels(biomeMapValues);

                        // Serialize them to disk
                        string folder = leaflet ? (current % (width / tile) + "/") : "";
                        string fileName = leaflet ? (current / (width / tile)) + ".png" : "Tile" + current.ToString("D4") + ".png";

                        if (exportHeightMap)
                        {
                            Directory.CreateDirectory(exportFolder + "HeightMap/" + folder);
                            File.WriteAllBytes(exportFolder + "HeightMap/" + folder + fileName, heightMap.EncodeToPNG());
                            File.WriteAllLines
                            (
                                exportFolder + "HeightMap/Info.txt",
                                new string[]
                                {
                                    "HeightMap info",
                                    "",
                                    "Body = " + body.transform.name,
                                    "deformity = " + pqs.radiusDelta,
                                    "offset = " + (pqs.radiusMin - pqs.radius)
                                }
                            );
                        }

                        if (exportNormalMap)
                        {
                            Directory.CreateDirectory(exportFolder + "NormalMap/" + folder);
                            File.WriteAllBytes(exportFolder + "NormalMap/" + folder + fileName, normalMap.EncodeToPNG());
                        }

                        if (exportSlopeMap)
                        {
                            Directory.CreateDirectory(exportFolder + "SlopeMap/" + folder);
                            File.WriteAllBytes(exportFolder + "SlopeMap/" + folder + fileName, slopeMap.EncodeToPNG());
                        }

                        if (exportColorMap)
                        {
                            Directory.CreateDirectory(exportFolder + "ColorMap/" + folder);
                            File.WriteAllBytes(exportFolder + "ColorMap/" + folder + fileName, colorMap.EncodeToPNG());
                        }

                        if (exportOceanMap)
                        {
                            Directory.CreateDirectory(exportFolder + "OceanMap/" + folder);
                            File.WriteAllBytes(exportFolder + "OceanMap/" + folder + fileName, oceanMap.EncodeToPNG());
                        }

                        if (exportSatelliteMap)
                        {
                            Directory.CreateDirectory(exportFolder + "SatelliteMap/" + folder);
                            File.WriteAllBytes(exportFolder + "SatelliteMap/" + folder + fileName, satelliteMap.EncodeToPNG());
                        }

                        if (exportBiomeMap)
                        {
                            Directory.CreateDirectory(exportFolder + "BiomeMap/" + folder);
                            File.WriteAllBytes(exportFolder + "BiomeMap/" + folder + fileName, biomeMap.EncodeToPNG());

                            List<string> attributes = new string[] { "BiomeMap info", "", "Body = " + body.transform.name }.ToList();
                            foreach (var biome in body.BiomeMap.Attributes)
                            {
                                attributes.Add("Biome =\t" + biome.name + "\t" + biome.mapColor);
                            }

                            File.WriteAllLines(exportFolder + "BiomeMap/Info.txt", attributes.ToArray());
                        }
                    }
                    current++;
                }
            }

            // Close the Renderer
            pqs.isBuildingMaps = false;
            pqs.isFakeBuild = false;

            // CleanUp
            DestroyImmediate(heightMap);
            DestroyImmediate(normalMap);
            DestroyImmediate(slopeMap);
            DestroyImmediate(colorMap);
            DestroyImmediate(oceanMap);
            DestroyImmediate(satelliteMap);
            DestroyImmediate(biomeMap);
        }

        bool Print(int current)
        {
            bool from = (current >= printFrom) && !(current > printTo);
            bool to = (current <= printTo) && !(current < printFrom);
            bool tile = printTile.Contains(current);

            return from || to || tile || (printTile.Count == 0 && printFrom == null && printTo == null);
        }

        void CalculateSlope(double[] normalMapValues, PQS pqs, int? firstY, int? lastY, ref Texture2D normalMap, ref Texture2D slopeMap)
        {
            double dS = pqs.radius * 2 * Math.PI / width;

            for (int y = 0; y < tile; y++)
            {
                for (var x = 0; x < tile; x++)
                {
                    // force slope = 0 at the poles
                    if (y == firstY || y == lastY)
                    {
                        if (exportNormalMap || exportSatelliteMap)
                        {
                            if (y == firstY)
                                normalMap.SetPixel(x, y, new Color(1, 0, 0, 1));

                            if (y == lastY)
                                normalMap.SetPixel(x, y, new Color(0, 1, 0, 1));
                        }

                        if (exportSlopeMap)
                            slopeMap.SetPixel(x, y, slopeMin);
                    }
                    // otherwise calculate it from the terrain
                    else
                    {
                        int xN = x - 1;
                        int xP = x + 1;

                        int yN = y - 1;
                        int yP = y + 1;

                        // shift all by one since `normalMapValues` has an extra frame of 1 pixel
                        double dX = normalMapValues[((y + 1) * (tile + 2)) + (xP + 1)] - normalMapValues[((y + 1) * (tile + 2)) + (xN + 1)];
                        double dY = normalMapValues[((yP + 1) * (tile + 2)) + (x + 1)] - normalMapValues[((yN + 1) * (tile + 2)) + (x + 1)];

                        if (exportNormalMap || exportSatelliteMap)
                        {
                            double slopeX = (1 + dX / Math.Pow(dX * dX + dS * dS, 0.5) * normalStrength) / 2;
                            double slopeY = (1 - dY / Math.Pow(dY * dY + dS * dS, 0.5) * normalStrength) / 2;

                            normalMap.SetPixel(x, y, new Color((float)slopeY, (float)slopeY, (float)slopeY, (float)slopeX));
                        }

                        if (exportSlopeMap)
                        {
                            Vector3d vX = new Vector3d(dS, 0, dX);
                            Vector3d vY = new Vector3d(0, dS, dY);

                            Vector3d n = Vector3d.Cross(vX, vY);

                            double slope = Vector3d.Angle(new Vector3d(0, 0, 1), n) / 90d;

                            if (slope > 1) slope = 2 - slope;

                            slopeMap.SetPixel(x, y, Color.Lerp(slopeMin, slopeMax, (float)slope));
                        }
                    }
                }
            }
        }

        void CreateSatelliteMap(Color[] colorMap, Color[] oceanMap, ref Texture2D normalMap, ref Texture2D satelliteMap)
        {
            for (int y = 0; y < satelliteMap.height; y++)
            {
                for (int x = 0; x < satelliteMap.width; x++)
                {
                    float shadow = normalMap.GetPixel(x, y).a - 0.5f;
                    satelliteMap.SetPixel(x, y, Color.Lerp(colorMap[(y * tile) + x], shadow > 0 ? Color.black : Color.white, Math.Abs(shadow)));

                    if (body.ocean && oceanMap[(y * tile) + x].a == 1)
                        satelliteMap.SetPixel(x, y, oceanMap[(y * tile) + x]);
                }
            }
        }

        Dictionary<double, Color> Parse(ConfigNode node, Dictionary<double, Color> defaultValue)
        {
            for (int i = 0; i < node?.values?.Count; i++)
            {
                ConfigNode.Value val = node.values[i];

                defaultValue = defaultValue ?? new Dictionary<double, Color>();

                if (double.TryParse(val.name, out double alt) && TryParse.Color(val.value, out Color color))
                {
                    defaultValue.Add(alt, color);
                }
                else
                {
                    defaultValue = null;
                    break;
                }

                defaultValue.OrderBy(v => v.Key);
            }

            return defaultValue;
        }
    }

    static class TryParse
    {
        internal static bool Color(string s, out Color color)
        {
            bool valid = false;

            float r = 0;
            float g = 0;
            float b = 0;

            string[] array = s?.Split(',');

            valid = array?.Length > 2 && float.TryParse(array[0], out r) && float.TryParse(array[1], out g) && float.TryParse(array[2], out b);

            color = new Color(r, g, b);

            return valid;
        }

        internal static bool Path(string s, out string path)
        {
            path = "GameData/Sigma/Cartographer/PluginData/" + MapExporter.body.transform.name + "/";

            if (string.IsNullOrEmpty(s)) return false;

            try
            {
                if (!s.EndsWith("/")) s += "/";

                if (System.IO.Path.GetFullPath(path + s).StartsWith(System.IO.Path.GetFullPath(path)))
                {
                    path += s;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                Debug.Log("[SigmaLog SC] TryParse.Path: Specified path is not valid. (" + s + ")");
            }

            return false;
        }
    }
}
