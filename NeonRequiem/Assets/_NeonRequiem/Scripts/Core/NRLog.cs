using System.Diagnostics;

namespace NeonRequiem.Core
{
    /// <summary>Severity of a log entry.</summary>
    public enum LogLevel
    {
        Verbose = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>
    /// Destination for log output. The default sink forwards to the Unity console; tests install
    /// their own sink so pure logic can be exercised without the engine.
    /// </summary>
    public interface ILogSink
    {
        void Write(LogLevel level, string category, string message, UnityEngine.Object context);
    }

    /// <summary>
    /// Project-wide logging facade. Every message carries a category so console output can be
    /// filtered per subsystem ("[NR:Input] ..."). Verbose calls are compiled out of release builds.
    /// </summary>
    public static class NRLog
    {
        private sealed class UnityLogSink : ILogSink
        {
            public void Write(LogLevel level, string category, string message, UnityEngine.Object context)
            {
                string line = "[NR:" + category + "] " + message;
                switch (level)
                {
                    case LogLevel.Error:
                        UnityEngine.Debug.LogError(line, context);
                        break;
                    case LogLevel.Warning:
                        UnityEngine.Debug.LogWarning(line, context);
                        break;
                    default:
                        UnityEngine.Debug.Log(line, context);
                        break;
                }
            }
        }

        private static readonly ILogSink s_UnitySink = new UnityLogSink();
        private static ILogSink s_Sink = s_UnitySink;

        /// <summary>Entries below this level are dropped.</summary>
        public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        /// <summary>Active sink. Assigning null restores the Unity console sink.</summary>
        public static ILogSink Sink
        {
            get => s_Sink;
            set => s_Sink = value ?? s_UnitySink;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Verbose(string category, string message, UnityEngine.Object context = null)
        {
            Write(LogLevel.Verbose, category, message, context);
        }

        public static void Info(string category, string message, UnityEngine.Object context = null)
        {
            Write(LogLevel.Info, category, message, context);
        }

        public static void Warning(string category, string message, UnityEngine.Object context = null)
        {
            Write(LogLevel.Warning, category, message, context);
        }

        public static void Error(string category, string message, UnityEngine.Object context = null)
        {
            Write(LogLevel.Error, category, message, context);
        }

        private static void Write(LogLevel level, string category, string message, UnityEngine.Object context)
        {
            if (level < MinimumLevel)
            {
                return;
            }

            s_Sink.Write(level, category, message, context);
        }
    }
}
