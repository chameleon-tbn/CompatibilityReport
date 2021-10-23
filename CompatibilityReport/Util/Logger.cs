using System;
using System.IO;

// This class is based on the Logger class from Enhanced District Services by Tim / chronofanz:
// https://github.com/chronofanz/EnhancedDistrictServices/blob/master/Source/Logger.cs

namespace CompatibilityReport.Util
{
    public static class Logger
    {
        public enum LogLevel
        {
            Info,
            Warning,
            Error,
            Debug
        }

        // Loglevel constants to make the Log calls a bit more readable
        public const LogLevel Warning = LogLevel.Warning;
        public const LogLevel Error   = LogLevel.Error;
        public const LogLevel Debug   = LogLevel.Debug;

        private static LogFiler debugLog;
        private static LogFiler updaterLog;


        /// <summary>Logs a message to this mods log file, and optionally to the game log.</summary>
        public static void Log(string message, LogLevel logLevel = LogLevel.Info)
        {
            if (!ModSettings.DebugMode)
            {
                if (logLevel == Debug)
                {
                    return;
                }

                GameLog($"{ (logLevel == LogLevel.Info ? "" : $"[{ logLevel.ToString().ToUpper() }] ") }{ message }");
            }
            else
            {
                if (debugLog == null)
                {
                    Toolkit.CreateDirectory(ModSettings.DebugLogPath);

                    string fullPath = Path.Combine(ModSettings.DebugLogPath, ModSettings.LogFileName);

                    debugLog = new LogFiler(fullPath, backup: ModSettings.DebugMode);

                    GameLog($"Debug logging for this mod can be found in \"{ Toolkit.Privacy(fullPath) }\".");
                }

                debugLog.WriteLine(message, logLevel, timestamp: true, duplicateToGameLog: logLevel != LogLevel.Debug);
            }
        }


        /// <summary>Logs a message to the updater log.</summary>
        public static void UpdaterLog(string message, LogLevel logLevel = LogLevel.Info)
        {
            if ((logLevel == Debug) && !ModSettings.DebugMode)
            {
                return;
            }

            if (updaterLog == null)
            {
                string fullPath = Path.Combine(ModSettings.UpdaterPath, ModSettings.UpdaterLogFileName);
                updaterLog = new LogFiler(fullPath, backup: true);

                Log($"Logging for the updater can be found in \"{ Toolkit.Privacy(fullPath) }\".");
            }

            updaterLog.WriteLine(message, logLevel, timestamp: true, duplicateToGameLog: false);
        }


        /// <summary>Closes the updater log.</summary>
        public static void CloseUpdaterLog()
        {
            updaterLog = null;
        }


        /// <summary>Logs an exception to this mods log or updater log, and to the game log unless indicated otherwise.</summary>
        public static void Exception(Exception ex, LogLevel logLevel = LogLevel.Info)
        {
            if ((logLevel == Debug) && !ModSettings.DebugMode)
            {
                return;
            }

            // Exception with full or shorter stacktrace.
            Log((logLevel == Debug) ? $"[DEBUG][EXCEPTION] { ex }" : $"[EXCEPTION] { ex.GetType().Name }: { ex.Message }\n{ ex.StackTrace }");
        }


        /// <summary>Logs a message to the game log.</summary>
        private static void GameLog(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            UnityEngine.Debug.Log($"{ ModSettings.InternalName }: { message }");
        }


        /// <summary>The LogFiler class writes the logging to file.</summary>
        private class LogFiler
        {
            private readonly StreamWriter file;
            private readonly string logfileFullPath;


            /// <summary>Default constructor.</summary>
            /// <remarks>'append' is not currently used.</remarks>
            public LogFiler(string fileFullPath, bool backup, bool append = false)
            {
                logfileFullPath = fileFullPath;

                // Remove backup file.
                Toolkit.DeleteFile($"{ logfileFullPath }.old");

                try
                {
                    if (!File.Exists(logfileFullPath))
                    {
                        file = File.CreateText(logfileFullPath);
                    }
                    else if (append && (new FileInfo(logfileFullPath).Length < ModSettings.LogMaxSize))
                    {
                        file = File.AppendText(logfileFullPath);

                        WriteLine($"\n\n{ new string('=', ModSettings.TextReportWidth) }\n\n", LogLevel.Info, timestamp: false, duplicateToGameLog: false);
                    }
                    else
                    {
                        if (append || backup)
                        {
                            // Make a backup before overwriting. 
                            Toolkit.CopyFile(logfileFullPath, $"{ logfileFullPath }.old");
                        }

                        file = File.CreateText(logfileFullPath);

                        if (append)
                        {
                            WriteLine($"Older info moved to \"{ Path.GetFileName(logfileFullPath) ?? "" }.old\".\n\n\n{ new string('=', ModSettings.TextReportWidth) }\n\n", 
                                LogLevel.Info, timestamp: false, duplicateToGameLog: false);
                        }
                    }

                    // Auto flush file buffer after every write.
                    file.AutoFlush = true;
                }
                catch (Exception ex)
                {
                    GameLog($"[ERROR] Can't create file \"{ Toolkit.Privacy(logfileFullPath) }\". { Toolkit.ShortException(ex) }");
                }
            }


            /// <summary>Writes a message to a log file, with optional loglevel and timestamp, and optionally duplicating to the game log.</summary>
            public void WriteLine(string message, LogLevel logLevel, bool timestamp, bool duplicateToGameLog)
            {
                if (string.IsNullOrEmpty(message))
                {
                    return;
                }

                string logLevelPrefix = (logLevel == LogLevel.Info) ? "" : $"[{ logLevel.ToString().ToUpper() }] ";

                try
                {
                    // Use a lock for when the updater gets its own executable but still uses the same log file.
                    lock (file)
                    {
                        file.WriteLine($"{ (timestamp ? $"{ DateTime.Now:yyyy-MM-dd HH:mm:ss} - " : "") }{ logLevelPrefix }{ message }");
                    }
                }
                catch
                {
                    GameLog($"[ERROR] Can't log to \"{ Toolkit.Privacy(logfileFullPath) }\".");
                    duplicateToGameLog = true;
                }

                if (duplicateToGameLog)
                {
                    GameLog($"{ logLevelPrefix }{ message }");
                }

                if (ModSettings.DebugMode)
                {
                    string lowerCaseMessage = message.ToLower();
                    if (lowerCaseMessage.Contains("\\appdata\\local") || lowerCaseMessage.Contains("c:\\users\\") || lowerCaseMessage.Contains("/users/"))
                    {
                        Log("Previously logged path probably needs more privacy.", LogLevel.Debug);
                    }
                }
            }
        }
    }
}
