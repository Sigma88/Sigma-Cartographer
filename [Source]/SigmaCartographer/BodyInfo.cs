using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;


namespace SigmaCartographerPlugin
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    class BodyInfo : MonoBehaviour
    {
        static double definition = 0.5;
        static string[] text = new string[16];

        void Start()
        {
            List<string> info = null;

            ConfigNode bodyInfo = UserSettings.ConfigNode.GetNode("Info");

            if (bodyInfo != null)
            {
                info = info ?? new List<string>();

                definition = double.TryParse(bodyInfo.GetValue("definition"), out double parsed) ? parsed : definition;

                string[] body = bodyInfo.GetValues("body");

                for (int i = 0; i < body.Length; i++)
                {
                    if (!info.Contains(body[i]))
                        info.Add(body[i]);
                }
            }

            if (info != null)
            {
                int n = info.Count;

                if (n == 0)
                    info = FlightGlobals.Bodies.Select(p => p.transform.name).ToList();

                for (int i = 0; i < n; i++)
                {
                    string bodyName = info[i];
                    CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(p => p.transform.name == bodyName);

                    if (body?.pqsController != null)
                        FirstPass(body);
                }
            }
        }

        void FirstPass(CelestialBody body)
        {
            List<LLA> ALL = new List<LLA>();

            double terrain = 0;
            double surface = 0;
            double underwater = 0;

            for (double LON = -180 + definition / 2; LON < 180; LON += definition)
            {
                for (double LAT = -90 + definition / 2; LAT < 90; LAT += definition)
                {
                    double ALT = body.TerrainAltitude(LAT, LON, true);

                    ALL.Add(new LLA(LAT, LON, ALT));

                    if (ALT == 0) continue;

                    double area = Math.PI * (Math.Cos((LAT - definition / 2) / 180 * Math.PI) + Math.Cos((LAT + definition / 2) / 180 * Math.PI)) / (4 * 180 / definition * 360 / definition);

                    terrain += ALT * area;

                    if (ALT > 0)
                    {
                        surface += ALT * area;
                    }
                    else
                    {
                        underwater += area;
                    }
                }
            }

            Lowest(body, definition, ALL.OrderBy(v => v.alt).Take(100));
            Highest(body, definition, ALL.OrderByDescending(v => v.alt).Take(100));
            Print(body, terrain, surface, underwater);
        }

        void Lowest(CelestialBody body, double delta, IEnumerable<LLA> ALL)
        {
            if (delta > 0.0001)
            {
                List<LLA> BEST = new List<LLA>();

                int n = ALL.Count();

                for (int i = 0; i < n; i++)
                {
                    LLA LatLon = ALL.ElementAt(i);

                    for (double LON = LatLon.lon - delta / 2; LON <= LatLon.lon + delta / 2; LON += delta / 10)
                    {
                        for (double LAT = LatLon.lat - delta / 2; LAT <= LatLon.lat + delta / 2; LAT += delta / 10)
                        {
                            double ALT = body.TerrainAltitude(LAT, LON, true);
                            BEST.Add(new LLA(LAT, LON, ALT));
                        }
                    }
                }

                Lowest(body, delta / 10, BEST.OrderBy(v => v.alt).Take(100));
            }
            else
            {
                LLA BEST = ALL.OrderBy(v => v.alt).FirstOrDefault();

                text[0] = "Lowest Point";
                text[1] = "LAT = " + BEST.lat;
                text[2] = "LON = " + BEST.lon;
                text[3] = "ALT = " + BEST.alt;
                text[4] = "";
            }
        }

        void Highest(CelestialBody body, double delta, IEnumerable<LLA> ALL)
        {
            if (delta > 0.001)
            {
                List<LLA> BEST = new List<LLA>();

                int n = ALL.Count();

                for (int i = 0; i < n; i++)
                {
                    LLA LatLon = ALL.ElementAt(i);

                    for (double LON = LatLon.lon - delta / 2; LON <= LatLon.lon + delta / 2; LON += delta / 10)
                    {
                        for (double LAT = LatLon.lat - delta / 2; LAT <= LatLon.lat + delta / 2; LAT += delta / 10)
                        {
                            double ALT = body.TerrainAltitude(LAT, LON, true);
                            BEST.Add(new LLA(LAT, LON, ALT));
                        }
                    }
                }

                Highest(body, delta / 10, BEST.OrderByDescending(v => v.alt).Take(100));
            }
            else
            {
                LLA BEST = ALL.OrderByDescending(v => v.alt).FirstOrDefault();

                text[5] = "Highest Point";
                text[6] = "LAT = " + BEST.lat;
                text[7] = "LON = " + BEST.lon;
                text[8] = "ALT = " + BEST.alt;
                text[9] = "";
            }
        }

        void Print(CelestialBody body, double terrain, double surface, double underwater)
        {
            text[10] = "Average Elevation";
            text[11] = "Terrain = " + terrain;
            text[12] = "Surface = " + surface;
            text[13] = "";
            text[14] = "Water Coverage = " + Math.Round(100 * underwater, 2) + "%";

            string path = "GameData/Sigma/Cartographer/PluginData/" + body.transform.name + "/";

            Directory.CreateDirectory(path);
            File.WriteAllLines(path + "Info.txt", text);
        }
    }

    internal class LLA
    {
        internal double lat;
        internal double lon;
        internal double alt;

        internal LLA()
        {
        }

        internal LLA(double lat, double lon, double alt)
        {
            this.lat = lat;
            this.lon = lon;
            this.alt = alt;
        }
    }
}
