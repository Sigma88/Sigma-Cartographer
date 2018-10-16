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
        }

        internal static void FlipH(ref Texture2D texture)
        {
            for (int y = 0; y < texture?.height; y++)
            {
                texture.SetPixels(0, y, texture.width, 1, texture.GetPixels(0, y, texture.width, 1).Reverse().ToArray());
            }
        }
        
        internal static Texture2D CreateReadable(Texture2D original)
        {
            // Checks
            if (original == null) return null;
            if (original.width == 0 || original.height == 0) return null;

            // Create the new texture
            Texture2D finalTexture = new Texture2D(original.width, original.height);

            // isn't read or writeable ... we'll have to get tricksy
            RenderTexture rt = RenderTexture.GetTemporary(original.width, original.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
            Graphics.Blit(original, rt);
            RenderTexture.active = rt;

            // Load new texture
            finalTexture.ReadPixels(new Rect(0, 0, finalTexture.width, finalTexture.height), 0, 0);

            // Kill the old one
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // Return
            return finalTexture;
        }
    }

    internal static class TryParse
    {
        internal static bool Color(string s, out Color color)
        {
            bool valid = false;

            float r = 0;
            float g = 0;
            float b = 0;

            string[] array = s?.Split(',');

            valid = array?.Length > 2 && float.TryParse(array[0], out r) && float.TryParse(array[1], out g) && float.TryParse(array[2], out b);

            color = valid ? new Color(r, g, b) : UnityEngine.Color.white;

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
                UnityEngine.Debug.Log("[SigmaLog SC] TryParse.Path: Specified path is not valid. (" + s + ")");
            }

            return false;
        }
    }
}
