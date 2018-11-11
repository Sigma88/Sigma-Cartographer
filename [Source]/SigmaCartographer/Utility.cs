using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace SigmaCartographerPlugin
{
    internal static class Utility
    {
        internal static List<KeyValuePair<double, Color>> Parse(ConfigNode node, List<KeyValuePair<double, Color>> defaultValue)
        {
            for (int i = 0; i < node?.values?.Count; i++)
            {
                ConfigNode.Value val = node.values[i];

                defaultValue = defaultValue ?? new List<KeyValuePair<double, Color>>();

                if (double.TryParse(val.name, out double alt) && TryParse.Color(val.value, out Color color))
                {
                    defaultValue.Add(new KeyValuePair<double, Color>(alt, color));
                }
                else
                {
                    defaultValue = null;
                    break;
                }

                defaultValue = defaultValue.OrderBy(v => v.Key).ToList();
            }

            return defaultValue;
        }

        internal static void FlipV(ref Texture2D texture)
        {
            for (int x = 0; x < texture?.width; x++)
            {
                texture.SetPixels(x, 0, 1, texture.height, texture.GetPixels(x, 0, 1, texture.height).Reverse().ToArray());
            }
            texture.Apply();
        }

        internal static void FlipH(ref Texture2D texture)
        {
            for (int y = 0; y < texture?.height; y++)
            {
                texture.SetPixels(0, y, texture.width, 1, texture.GetPixels(0, y, texture.width, 1).Reverse().ToArray());
            }
            texture.Apply();
        }

        static Texture2D _black;
        internal static Texture2D black
        {
            get
            {
                if (!_black)
                {
                    _black = new Texture2D(1, 1);
                    _black.SetPixel(0, 0, new Color(0, 0, 0, 1));
                    _black.Apply();
                }

                return _black;
            }
        }
    }

    internal static class TryParse
    {
        internal static bool Color(string s, out Color? color)
        {
            if (Color(s, out Color c))
            {
                color = c;
                return true;
            }
            else
            {
                color = null;
                return false;
            }
        }

        internal static bool Color(string s, out Color color)
        {
            bool valid = false;

            float r = 0;
            float g = 0;
            float b = 0;
            float a = 1;

            string[] array = s?.Split(',');

            valid = array?.Length > 2 && float.TryParse(array[0], out r) && float.TryParse(array[1], out g) && float.TryParse(array[2], out b);

            if (valid && array?.Length > 3)
            {
                if (!float.TryParse(array[3], out a))
                {
                    valid = false;
                }
            }

            color = valid ? new Color(r, g, b, a) : new Color(1, 1, 1, 1);

            return valid;
        }

        internal static bool Path(string s, out string path)
        {
            path = "GameData/Sigma/Cartographer/PluginData/" + MapGenerator.body.transform.name + "/";

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
                Debug.LOG("TryParse.Path", "ERROR - Specified path is not valid. (" + s + ")");
            }

            return false;
        }
    }
}
