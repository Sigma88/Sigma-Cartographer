namespace SigmaCartographerPlugin
{
    internal static class Debug
    {
        internal static bool debug = false;
        internal static string Tag = "[SigmaLog SC]";

        internal static void Log(string message)
        {
            if (debug)
            {
                LOG(message);
            }
        }

        internal static void Log(string Method, string message)
        {
            if (debug)
            {
                LOG(Method, message);
            }
        }

        internal static void LOG(string message)
        {
            UnityEngine.Debug.Log(Tag + ": " + message);
        }

        internal static void LOG(string Method, string message)
        {
            UnityEngine.Debug.Log(Tag + " " + Method + ": " + message);
        }
    }
}
