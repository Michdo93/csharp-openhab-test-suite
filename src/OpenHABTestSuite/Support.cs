using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenHABTestSuite
{
    /// <summary>
    /// Receives the messages of the testers (Python: print). Default: info to standard output,
    /// errors to standard error.
    /// <code>TestLog.Handler = (level, text) => logger.Log(level == 0 ? LogLevel.Information : LogLevel.Error, text);</code>
    /// </summary>
    public static class TestLog
    {
        static readonly object Lock = new object();

        /// <summary>level 0 = info, 1 = error; null restores the default.</summary>
        public static Action<int, string>? Handler { get; set; }

        internal static void Info(string m) => Emit(0, m);
        internal static void Error(string m) => Emit(1, m);

        static void Emit(int level, string m)
        {
            lock (Lock)
            {
                var h = Handler;
                if (h != null) h(level, m);
                else if (level == 0) Console.WriteLine(m);
                else Console.Error.WriteLine(m);
            }
        }
    }

    internal static class Py
    {
        /// <summary>str(value) like Python: strings as they are, None, True/False, numbers.</summary>
        public static string Text(JsonNode? v)
        {
            if (v == null) return "None";
            switch (v.GetValueKind())
            {
                case JsonValueKind.Null: return "None";
                case JsonValueKind.String: return v.GetValue<string>();
                case JsonValueKind.True: return "True";
                case JsonValueKind.False: return "False";
                default: return v.ToJsonString();
            }
        }
    }
}
