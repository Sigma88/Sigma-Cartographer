using System;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;


namespace SigmaCartographerPlugin
{
    internal static class MapGenerator
    {
        // Settings

        internal static bool exportAny
        {
            get { return exportHeightMap || exportNormalMap || exportSlopeMap || exportColorMap || exportOceanMap || satelliteAny; }
            set { exportHeightMap = exportNormalMap = exportSlopeMap = exportColorMap = exportOceanMap = satelliteAny = value; }
        }

        static bool satelliteAny
        {
            get { return exportSatelliteHeight || exportSatelliteSlope || exportSatelliteMap || exportSatelliteBiome; }
            set { exportSatelliteHeight = exportSatelliteSlope = exportSatelliteMap = exportSatelliteBiome = value; }
        }

        static bool exportHeightMap = false;
        static bool exportNormalMap = false;
        static bool exportSlopeMap = false;
        static bool exportColorMap = true;
        static bool exportOceanMap = false;
        internal static bool exportBiomeMap = false;

        static bool exportSatelliteHeight = false;
        static bool exportSatelliteSlope = false;
        static bool exportSatelliteMap = false;
        static bool exportSatelliteBiome = false;

        internal static CelestialBody body;
        static int width = 2048;
        static int tile = 1024;
        static string exportFolder = "";
        static bool leaflet = false;
        static bool flipV = false;
        static bool flipH = false;

        static bool oceanFloor = true;
        static Color oceanColor = new Color(0.1f, 0.1f, 0.2f, 1f);
        static double LAToffset = 0;
        static double LONoffset = 0;
        static List<KeyValuePair<double, Color>> altitudeColor = null;
        static float normalStrength = 1;
        static Color slopeMin = new Color(0.2f, 0.3f, 0.4f);
        static Color slopeMax = new Color(0.9f, 0.6f, 0.5f);
        static List<int> printTile = new List<int>();
        static int? printFrom = null;
        static int? printTo = null;

        // Textures
        internal static Texture2D heightMap;
        internal static Texture2D normalMap;
        internal static Texture2D slopeMap;
        internal static Texture2D colorMap;
        internal static Texture2D oceanMap;
        internal static Texture2D biomeMap;
        internal static Texture2D satelliteMap;


        static int current = 0;

        internal static void LoadSettings(ConfigNode node)
        {
            bool.TryParse(node.GetValue("heightMap"), out exportHeightMap);
            bool.TryParse(node.GetValue("satelliteHeight"), out exportSatelliteHeight);

            bool.TryParse(node.GetValue("normalMap"), out exportNormalMap);

            bool.TryParse(node.GetValue("slopeMap"), out exportSlopeMap);
            bool.TryParse(node.GetValue("satelliteSlope"), out exportSatelliteSlope);

            if (!bool.TryParse(node.GetValue("colorMap"), out exportColorMap))
            {
                exportColorMap = true;
            }
            bool.TryParse(node.GetValue("satelliteMap"), out exportSatelliteMap);

            bool.TryParse(node.GetValue("oceanMap"), out exportOceanMap);

            bool.TryParse(node.GetValue("biomeMap"), out exportBiomeMap);
            bool.TryParse(node.GetValue("satelliteBiome"), out exportSatelliteBiome);


            if (!exportAny && !exportBiomeMap) return;

            string bodyName = node.GetValue("body");
            body = FlightGlobals.Bodies.FirstOrDefault(b => b.name == bodyName) ?? FlightGlobals.GetHomeBody();

            if (!int.TryParse(node.GetValue("width"), out width))
            {
                width = 2048;
            }

            if (!int.TryParse(node.GetValue("tile"), out tile))
            {
                tile = 1024;
            }

            TryParse.Path(node.GetValue("exportFolder"), out exportFolder);

            bool.TryParse(node.GetValue("leaflet"), out leaflet);

            bool.TryParse(node.GetValue("flipV"), out flipV);

            bool.TryParse(node.GetValue("flipH"), out flipH);

            if (!bool.TryParse(node.GetValue("oceanFloor"), out oceanFloor))
            {
                oceanFloor = true;
            }

            if (!TryParse.Color(node.GetValue("oceanColor"), out oceanColor))
            {
                oceanColor = body?.pqsController?.mapOceanColor ?? new Color(0.1f, 0.1f, 0.2f, 1f);
            }

            if (!double.TryParse(node.GetValue("LAToffset"), out LAToffset))
            {
                LAToffset = 0;
            }

            if (!double.TryParse(node.GetValue("LONoffset"), out LONoffset))
            {
                LONoffset = 0;
            }

            if (node.HasNode("AltitudeColor"))
            {
                altitudeColor = Utility.Parse(node.GetNode("AltitudeColor"), altitudeColor);
            }

            if (altitudeColor == null)
            {
                altitudeColor = new List<KeyValuePair<double, Color>>
                        {
                            new KeyValuePair<double, Color>(0, Color.black),
                            new KeyValuePair<double, Color>(1, Color.white)
                        };
            }

            if (!float.TryParse(node.GetValue("normalStrength"), out normalStrength))
            {
                normalStrength = 1;
            }

            if (!TryParse.Color(node.GetValue("slopeMin"), out slopeMin))
            {
                slopeMin = new Color(0.2f, 0.3f, 0.4f);
            }

            if (!TryParse.Color(node.GetValue("slopeMax"), out slopeMax))
            {
                slopeMax = new Color(0.9f, 0.6f, 0.5f);
            }

            printTile = new List<int>();

            foreach (string s in node.GetValues("printTile"))
            {
                int i = 0;
                if (int.TryParse(s, out i))
                {
                    printTile.Add(i);
                }
            }

            int temp = 0;

            if (int.TryParse(node.GetValue("printFrom"), out temp))
            {
                printFrom = temp;
            }
            else
            {
                printFrom = null;
            }

            if (int.TryParse(node.GetValue("printTo"), out temp))
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
        }

        internal static void GeneratePQSMaps(string subfolder = "")
        {
            // If no exports are required, end here
            if (!exportAny && !exportBiomeMap) return;

            // Textures
            try
            {
                heightMap = new Texture2D(tile, Math.Min(tile, width / 2), TextureFormat.ARGB32, true);
                normalMap = new Texture2D(tile, Math.Min(tile, width / 2), TextureFormat.ARGB32, true);
                slopeMap = new Texture2D(tile, Math.Min(tile, width / 2), TextureFormat.RGB24, true);
                colorMap = new Texture2D(tile, Math.Min(tile, width / 2), TextureFormat.ARGB32, true);
                oceanMap = new Texture2D(tile, Math.Min(tile, width / 2), TextureFormat.ARGB32, true);
                biomeMap = new Texture2D(tile, Math.Min(tile, width / 2), TextureFormat.RGB24, true);
                satelliteMap = new Texture2D(tile, Math.Min(tile, width / 2), TextureFormat.RGB24, true);
            }
            catch
            {
                Debug.LOG("MapGenerator", "ERROR - Failed to create new textures.");
                return;
            }

            // Create arrays
            double[] terrainHeightValues;
            Color[] heightMapValues;
            Color[] colorMapValues;
            Color[] oceanMapValues;
            Color[] biomeMapValues;
            // Define arrays
            try
            {
                terrainHeightValues = new double[(tile + 2) * (tile + 2)];
                heightMapValues = new Color[tile * tile];
                colorMapValues = new Color[tile * tile];
                oceanMapValues = new Color[tile * tile];
                biomeMapValues = new Color[tile * tile];
            }
            catch
            {
                Debug.LOG("MapGenerator", "ERROR - Failed to create new arrays.");
                return;
            }

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
            Action<PQS.VertexBuildData, Boolean> modOnVertexBuildHeight = (Action<PQS.VertexBuildData, Boolean>)Delegate.CreateDelegate(
                typeof(Action<PQS.VertexBuildData, Boolean>),
                pqs,
                typeof(PQS).GetMethod("Mod_OnVertexBuildHeight", BindingFlags.Instance | BindingFlags.NonPublic));
            Action<PQS.VertexBuildData> modOnVertexBuild = (Action<PQS.VertexBuildData>)Delegate.CreateDelegate(
                typeof(Action<PQS.VertexBuildData>),
                pqs,
                typeof(PQS).GetMethod("Mod_OnVertexBuild", BindingFlags.Instance | BindingFlags.NonPublic));


            int count = width / 2 - tile;

            for (int j = count < 0 ? 0 : count; j >= 0; j -= tile)
            {
                for (int i = 0; i < width; i += tile)
                {
                    if (Print(current))
                    {
                        // Loop through the pixels
                        for (int y = -1; y < tile + 1; y++)
                        {
                            if (y > width / 2 + 1) break;

                            for (int x = -1; x < tile + 1; x++)
                            {
                                // Longitude
                                double lon = ((i + x) * 360d / width + LONoffset) % 360d - 180d;
                                lon = ((lon % 360) + 360) % 360;
                                // Latitude
                                double lat = ((j + y) * 360d / width - LAToffset) % 180d - 90d;
                                if (lat < -90) lat += 180;

                                // Generate
                                try
                                {
                                    if (exportAny)
                                    {
                                        // Create a VertexBuildData
                                        PQS.VertexBuildData data = new PQS.VertexBuildData
                                        {
                                            directionFromCenter = body.GetRelSurfaceNVector(lat, lon).normalized,
                                            vertHeight = pqs.radius
                                        };

                                        // Build from the Mods
                                        modOnVertexBuildHeight(data, true);

                                        if (exportHeightMap || exportNormalMap || exportSlopeMap || satelliteAny)
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
                                            if ((exportHeightMap || exportSatelliteHeight) && x > -1 && y > -1 && x < tile && y < tile)
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

                                            if (exportNormalMap || exportSlopeMap || satelliteAny)
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
                                            try
                                            {
                                                modOnVertexBuild(data);

                                                // Adjust the Color
                                                Color color = data.vertColor.A(1f);
                                                if (!oceanFloor && data.vertHeight < pqs.radius)
                                                    color = oceanColor;

                                                // Set the Pixels
                                                colorMapValues[(y * tile) + x] = color;
                                            }
                                            catch (Exception e)
                                            {
                                                Debug.LOG("MapGenerator", "ERROR - Failed to generate colors for colorMap/satelliteMap. \ncolorMapValues.length = " + colorMapValues.Length + "\n(y * tile) + x = " + x + ", y = " + y + ", tile = " + tile + "\n(y * tile) + x = " + ((y * tile) + x) + "\n\n" + e);
                                                return;
                                            }
                                        }

                                        if (body.ocean && exportOceanMap && x > -1 && y > -1 && x < tile && y < tile)
                                        {
                                            // Adjust the Color
                                            Color color = data.vertHeight < pqs.radius ? oceanColor : new Color(0, 0, 0, 0);

                                            // Set the Pixels
                                            oceanMapValues[(y * tile) + x] = color;
                                        }
                                    }

                                    if ((exportBiomeMap || exportSatelliteBiome) && x > -1 && y > -1 && x < tile && y < tile)
                                    {
                                        CBAttributeMapSO.MapAttribute biome = body.BiomeMap.GetAtt(lat * Math.PI / 180d, lon * Math.PI / 180d);
                                        Color color = biome.mapColor;
                                        biomeMapValues[(y * tile) + x] = color;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Debug.LOG("MapGenerator", "ERROR - Failed to generate colors for tile " + current + ".\n\n" + e + "\n\n" + e.StackTrace + "\n\n" + e.Source + "\n\n" + e.TargetSite);
                                    return;
                                }
                            }
                        }

                        // Apply the maps
                        try
                        {
                            if (exportHeightMap || exportSatelliteHeight)
                                heightMap.SetPixels(heightMapValues);
                            if (exportNormalMap || exportSlopeMap || satelliteAny)
                                CalculateSlope(terrainHeightValues, pqs, firstY, lastY, ref normalMap, ref slopeMap);
                            if (exportColorMap || exportSatelliteMap)
                                colorMap.SetPixels(colorMapValues);
                            if (exportOceanMap)
                                oceanMap.SetPixels(oceanMapValues);
                            if (exportBiomeMap || exportSatelliteBiome)
                                biomeMap.SetPixels(biomeMapValues);
                        }
                        catch
                        {
                            Debug.LOG("MapGenerator", "ERROR - Failed to apply colors for tile " + current + ".");
                            return;
                        }

                        // Serialize them to disk
                        int position = Flip(current);
                        string folder = leaflet ? (position % (width / tile) + "/") : "";
                        string fileName = leaflet ? (position / (width / tile)) + ".png" : "Tile" + position.ToString("D4") + ".png";

                        // Export
                        try
                        {
                            if (exportHeightMap)
                            {
                                if (flipH) Utility.FlipH(ref heightMap);
                                if (flipV) Utility.FlipV(ref heightMap);

                                ExportPQSMap(ref heightMap, subfolder + "HeightMap/", fileName);

                                File.WriteAllLines
                                (
                                    exportFolder + subfolder + "HeightMap/Info.txt",
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
                                if (flipH) Utility.FlipH(ref normalMap);
                                if (flipV) Utility.FlipV(ref normalMap);

                                ExportPQSMap(ref normalMap, subfolder + "NormalMap/", fileName);
                            }

                            if (exportSlopeMap)
                            {
                                if (flipH) Utility.FlipH(ref slopeMap);
                                if (flipV) Utility.FlipV(ref slopeMap);

                                ExportPQSMap(ref slopeMap, subfolder + "SlopeMap/", fileName);
                            }

                            if (exportColorMap)
                            {
                                if (flipH) Utility.FlipH(ref colorMap);
                                if (flipV) Utility.FlipV(ref colorMap);

                                ExportPQSMap(ref colorMap, subfolder + "ColorMap/", fileName);
                            }

                            if (exportOceanMap)
                            {
                                if (flipH) Utility.FlipH(ref oceanMap);
                                if (flipV) Utility.FlipV(ref oceanMap);

                                ExportPQSMap(ref oceanMap, subfolder + "OceanMap/", fileName);
                            }

                            if (exportBiomeMap)
                            {
                                if (flipH) Utility.FlipH(ref biomeMap);
                                if (flipV) Utility.FlipV(ref biomeMap);

                                ExportPQSMap(ref biomeMap, subfolder + "BiomeMap/", fileName);

                                List<string> attributes = new string[] { "BiomeMap info", "", "Body = " + body.transform.name }.ToList();
                                foreach (var biome in body.BiomeMap.Attributes)
                                {
                                    attributes.Add("Biome =\t" + biome.name + "\t" + biome.mapColor);
                                }
                                File.WriteAllLines(exportFolder + subfolder + "BiomeMap/Info.txt", attributes.ToArray());
                            }

                            if (satelliteAny)
                            {
                                if (exportSatelliteHeight)
                                {
                                    CreateSatelliteMap(ref heightMap, ref normalMap, ref satelliteMap);
                                    ExportPQSMap(ref satelliteMap, subfolder + "SatelliteHeight/", fileName);
                                }

                                if (exportSatelliteSlope)
                                {
                                    CreateSatelliteMap(ref slopeMap, ref normalMap, ref satelliteMap);
                                    ExportPQSMap(ref satelliteMap, subfolder + "SatelliteSlope/", fileName);
                                }

                                if (exportSatelliteMap)
                                {
                                    CreateSatelliteMap(ref colorMap, ref normalMap, ref satelliteMap);
                                    ExportPQSMap(ref satelliteMap, subfolder + "SatelliteMap/", fileName);
                                }

                                if (exportSatelliteBiome)
                                {
                                    CreateSatelliteMap(ref biomeMap, ref normalMap, ref satelliteMap);
                                    ExportPQSMap(ref satelliteMap, subfolder + "SatelliteBiome/", fileName);
                                }
                            }
                        }
                        catch
                        {
                            Debug.LOG("MapGenerator", "ERROR - Failed to export tile " + current + ".");
                            return;
                        }
                    }
                    current++;
                }
            }

            // Close the Renderer
            pqs.isBuildingMaps = false;
            pqs.isFakeBuild = false;
        }

        internal static void ExportPQSMap(ref Texture2D texture, string folder, string fileName)
        {
            Directory.CreateDirectory(exportFolder + folder);
            File.WriteAllBytes(exportFolder + folder + fileName, texture.EncodeToPNG());
        }

        internal static void CleanUp()
        {
            // CleanUp
            UnityEngine.Object.DestroyImmediate(heightMap);
            UnityEngine.Object.DestroyImmediate(normalMap);
            UnityEngine.Object.DestroyImmediate(slopeMap);
            UnityEngine.Object.DestroyImmediate(colorMap);
            UnityEngine.Object.DestroyImmediate(oceanMap);
            UnityEngine.Object.DestroyImmediate(biomeMap);
            UnityEngine.Object.DestroyImmediate(satelliteMap);
        }

        static int Flip(int n)
        {
            if (flipV)
            {
                n = ((width / tile / 2) - 1 - (n * tile / width)) * width / tile + (n % (width / tile));
            }

            if (flipH)
            {
                n = (n * tile / width) * width / tile + width / tile - 1 - (n % (width / tile));
            }

            return n;
        }

        static bool Print(int current)
        {
            bool from = (current >= printFrom) && !(current > printTo);
            bool to = (current <= printTo) && !(current < printFrom);
            bool tile = printTile.Contains(current);

            return from || to || tile || (printTile.Count == 0 && printFrom == null && printTo == null);
        }

        static void CalculateSlope(double[] normalMapValues, PQS pqs, int? firstY, int? lastY, ref Texture2D normalMap, ref Texture2D slopeMap)
        {
            double dS = pqs.radius * 2 * Math.PI / width;

            for (int y = 0; y < tile; y++)
            {
                if (y > width / 2) break;

                for (var x = 0; x < tile; x++)
                {
                    // force slope = 0 at the poles
                    if (y == firstY || y == lastY)
                    {
                        if (exportNormalMap || satelliteAny)
                        {
                            if (y == firstY)
                                normalMap.SetPixel(x, y, new Color(0.5f, 0.5f, 0.5f, 0.5f));

                            if (y == lastY)
                                normalMap.SetPixel(x, y, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                        }

                        if (exportSlopeMap || exportSatelliteSlope)
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

                        if (exportNormalMap || satelliteAny)
                        {
                            double slopeX = (1 + dX / Math.Pow(dX * dX + dS * dS, 0.5) * normalStrength) / 2;
                            double slopeY = (1 - dY / Math.Pow(dY * dY + dS * dS, 0.5) * normalStrength) / 2;

                            normalMap.SetPixel(x, y, new Color((float)slopeY, (float)slopeY, (float)slopeY, (float)slopeX));
                        }

                        if (exportSlopeMap || exportSatelliteSlope)
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

        internal static void CreateSatelliteMap(ref Texture2D baseMap, ref Texture2D normalMap, ref Texture2D satelliteMap)
        {
            for (int y = 0; y < satelliteMap.height; y++)
            {
                for (int x = 0; x < satelliteMap.width; x++)
                {
                    float shadow = normalMap.GetPixel(x, y).a - 0.5f;
                    satelliteMap.SetPixel(x, y, Color.Lerp(baseMap.GetPixel(x, y), shadow > 0 ? Color.black : Color.white, Math.Abs(shadow)));
                }
            }
        }
    }
}
