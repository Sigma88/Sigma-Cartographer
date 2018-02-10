using System.Linq;
using System.IO;
using UnityEngine;


namespace SigmaRandomPlugin
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    class BodyInfo : MonoBehaviour
    {
        static string[] info = new string[9];

        void Start()
        {
            UrlDir.UrlConfig[] nodes = GameDatabase.Instance?.GetConfigs("SigmaCartographer");

            foreach (var node in nodes)
            {
                foreach (var bodyInfo in node.config.GetNodes("Info"))
                {
                    foreach (var bodyName in bodyInfo.GetValues("body"))
                    {
                        CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(p => p.transform.name == bodyName);
                        if (body?.pqsController != null)
                        {
                            FirstPass(body);
                        }
                    }
                }
            }
        }

        void FirstPass(CelestialBody body)
        {
            double lowest = double.MaxValue;
            double highest = double.MinValue;

            Vector2d LatLonLo = new Vector2d();
            Vector2d LatLonHi = new Vector2d();

            for (double LON = -180; LON < 180; LON += 0.1)
            {
                for (double LAT = -90; LAT <= 90; LAT += 0.1)
                {
                    double ALT = body.TerrainAltitude(LAT, LON, true);

                    if (ALT < lowest)
                    {
                        lowest = ALT;
                        LatLonLo = new Vector2d(LAT, LON);
                    }
                    if (ALT > highest)
                    {
                        highest = ALT;
                        LatLonHi = new Vector2d(LAT, LON);
                    }
                }
            }
            Lowest(body, LatLonLo);
            Highest(body, LatLonHi);
        }

        void Lowest(CelestialBody body, Vector2d LatLon)
        {
            double altitude = double.MaxValue;
            double latitude = LatLon.x;
            double longitude = LatLon.y;

            for (double LON = LatLon.y - 0.1; LON <= LatLon.y + 0.1; LON += 0.001)
            {
                for (double LAT = LatLon.x - 0.1; LAT <= LatLon.x + 0.1; LAT += 0.001)
                {
                    double ALT = body.TerrainAltitude(LAT, LON, true);
                    if (ALT < altitude)
                    {
                        latitude = LAT;
                        longitude = LON;
                        altitude = ALT;
                    }
                }
            }

            info[0] = "Lowest Point";
            info[1] = "LAT = " + latitude;
            info[2] = "LON = " + longitude;
            info[3] = "ALT = " + altitude;
            info[4] = "";
        }

        void Highest(CelestialBody body, Vector2d LatLon)
        {
            double altitude = double.MinValue;
            double latitude = LatLon.x;
            double longitude = LatLon.y;

            for (double LON = LatLon.y - 0.1; LON <= LatLon.y + 0.1; LON += 0.001)
            {
                for (double LAT = LatLon.x - 0.1; LAT <= LatLon.x + 0.1; LAT += 0.001)
                {
                    double ALT = body.TerrainAltitude(LAT, LON, true);
                    if (ALT > altitude)
                    {
                        latitude = LAT;
                        longitude = LON;
                        altitude = ALT;
                    }
                }
            }

            info[5] = "Highest Point";
            info[6] = "LAT = " + latitude;
            info[7] = "LON = " + longitude;
            info[8] = "ALT = " + altitude;

            string path = "GameData/Sigma/Cartographer/PluginData/" + body.transform.name + "/";

            Directory.CreateDirectory(path);
            File.WriteAllLines(path + "Info.txt", info);
        }
    }
}
