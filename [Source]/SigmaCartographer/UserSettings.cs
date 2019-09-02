using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace SigmaCartographerPlugin
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal class UserSettings : MonoBehaviour
    {
        internal static ConfigNode ConfigNode
        {
            get
            {
                return GameDatabase.Instance?.GetConfigs(nodeName)?.FirstOrDefault(n => n.url == (folder.Substring(9) + file + "/" + nodeName))?.config;
            }
        }

        static string folder = "GameData/Sigma/Cartographer/";
        static string file = "Settings";
        internal static string nodeName = "SigmaCartographer";

        void Awake()
        {
            string path = Assembly.GetExecutingAssembly().Location.Replace('\\', '/');
            while (path.Substring(1).Contains("GameData/"))
                path = path.Substring(1 + path.Substring(1).IndexOf("GameData/"));
            if (path != folder + "Plugins/" + Path.GetFileName(path))
                UnityEngine.Debug.Log(Debug.Tag + " WARNING: Incorrect plugin location => " + path);

            if (!Directory.Exists(folder))
            {
                UnityEngine.Debug.Log(Debug.Tag + " WARNING: Missing folder => " + folder);
                return;
            }

            if (!File.Exists(folder + file + ".cfg"))
            {
                UnityEngine.Debug.Log(Debug.Tag + " WARNING: Missing file => " + folder + file + ".cfg");
                UnityEngine.Debug.Log(Debug.Tag + "          Writing file => " + folder + file + ".cfg");
                //File.WriteAllLines(folder + file + ".cfg", new[] { nodeName + " {}" });
                //------
                //The following writes the default Settings.cfg file
                //but includes commented lines that can act as documentation
                //for users.
                //STH 2019-0831
                File.WriteAllLines(
                    folder + file + ".cfg",
                    new string[]
                    {
                        "//The configuration file has the following format:",
                        "//"+nodeName,
                        "//{",
                        "// Maps",
                        "// {",
                        "//     body = Kerbin   // the name of the body           (default = Kerbin)",
                        "//     width = 2048    // the total width of the texture (default = 2048)",
                        "//     tile = 1024     // the width of one﻿ tile          (default = 1024)",
                        "//     flipV = false   // flip the image vertically      (default = false)",
                        "//     flipH = false   // flip the image horizontally    (default = false)",
                        "//     exportFolder = Sigma/Cartographer/Maps        // path for the export folder",
                        "//     leaflet = false // export in folders divided by columns and rows (default = false)",
                        "//",
                        "//     heightMap = false       // export height map        (default = false)",
                        "//     normalMap = false       // ?                        (default = false)",
                        "//     slopeMap = false        // export slope map         (default = false)",
                        "//     colorMap = true         // ?                        (default = true)",
                        "//     oceanMap = false        // export surface ocean     (default = false)",                     
                        "//     biomeMap = false        // export biome map         (default = false)",
                        "//     satelliteHeight = false // if true, forces heightMap to be true (default = false)",
                        "//     satelliteSlope = false  // if true, forces slopeMap to be true  (default = false)",
                        "//     satelliteMap = false    // if true, forces colorMap to be true  (default = false)",
                        "//     satelliteBiome = false  // if true, forces biomeMap to be true  (default = false)",
                        "//",
                        "//     //To have Sigma Cartographer export images only if they don't exist:",
                        "//     overwriteHeightMap = false      //(default = true)",
                        "//     overwriteNormalMap = false      //(default = true)",
                        "//     overwriteSlopeMap = false       //(default = true)",
                        "//     overwriteColorMap = false       //(default = true)",
                        "//     overwriteOceanMap = false       //(default = true)",
                        "//     overwriteBiomeMap = false       //(default = true)",
                        "//",
                        "//     overwriteSatelliteHeight = true //(default = true)",
                        "//     overwriteSatelliteSlope = true  //(default = true)",
                        "//     overwriteSatelliteMap = true    //(default = true)",
                        "//     overwriteSatelliteBiome = true  //(default = true)",
                        "//-----------",
                        "//     oceanFloor = true      // include the ocean floor on maps. (default = true)",
                        "//",
                        "//     normalStrength = 1     // strength of normals (default = 1)",
                        "//     slopeMin = 0.2,0.3,0.4 // color for  0° slope (default = 0.2,0.3,0.4)",
                        "//     slopeMax = 0.9,0.6,0.5 // color for 90° slope (default = 0.9,0.6,0.5)",
                        "//",
                        "//     LAToffset = 0          // offset latitude  (default = 0)",
                        "//     LONoffset = 0          // offset longitude (default = 0)",
                        "//",
                        "//     //if you want to print only selected tiles",
                        "//     //add as many of these as you want",
                        "//     //if you don't add any of these",
                        "//     //all tiles will be exported",
                        "//     printTile = 0",
                        "//     printTile = 1",
                        "// }",
                        "// Info",
                        "// {",
                        "//     //List all the bodies for which you want info on",
                        "//     //the lowest and highest point on the planet.",
                        "//     //Examples:",    
                        "//     body = Kerbin",
                        "//     body = Mun",
                        "// }",
                        "//}",
                        "",
                        nodeName + " {}"
                    }
                );
                //-------
                return;
            }

            if (ConfigNode.Load(folder + file + ".cfg")?.HasNode(nodeName) != true)
            {
                UnityEngine.Debug.Log(Debug.Tag + " WARNING: Missing node => " + folder + file + "/" + nodeName);

                File.AppendAllText(folder + file + ".cfg", "\r\n" + nodeName + " {}");
            }
        }

        void Start()
        {
            var configs = GameDatabase.Instance.GetConfigs(nodeName);

            for (int i = 0; i < configs?.Length; i++)
            {
                if (configs[i].url != (folder.Substring(9) + file + "/" + nodeName))
                    configs[i].parent.configs.Remove(configs[i]);
            }
        }
    }
}
