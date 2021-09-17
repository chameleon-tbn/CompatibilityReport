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

        private static LogFiler regularLog;
        private static bool regularLogInitialized;

        private static LogFiler updaterLog;
        private static bool updaterLogInitialized;


        /// <summary>Logs a message to this mods log file, and optionally to the game log.</summary>
        public static void Log(string message, LogLevel logLevel = LogLevel.Info, bool duplicateToGameLog = false)
        {
            if ((logLevel == Debug) && !ModSettings.DebugMode)
            {
                return;
            }

            if (!regularLogInitialized)
            {
                string fullPath = Path.Combine(ModSettings.LogPath, ModSettings.LogFileName);
                regularLog = new LogFiler(fullPath, append: ModSettings.LogAppend);
                regularLogInitialized = true;

                GameLog($"Detailed logging for this mod can be found in \"{ Toolkit.Privacy(fullPath) }\".");
            }

            regularLog.WriteLine(message, logLevel, duplicateToGameLog, timestamp: true);
        }


        /// <summary>Logs a message to the updater log.</summary>
        public static void UpdaterLog(string message, LogLevel logLevel = LogLevel.Info)
        {
            if ((logLevel == Debug) && !ModSettings.DebugMode)
            {
                return;
            }

            if (!updaterLogInitialized)
            {
                string fullPath = Path.Combine(ModSettings.UpdaterPath, ModSettings.UpdaterLogFileName);
                updaterLog = new LogFiler(fullPath, append: false);
                updaterLogInitialized = true;

                Log($"Logging for the updater can be found in \"{ Toolkit.Privacy(fullPath) }\".");
            }

            updaterLog.WriteLine(message, logLevel, duplicateToGameLog: false, timestamp: true);
        }


        /// <summary>Closes the updater log.</summary>
        public static void CloseUpdateLog()
        {
            updaterLogInitialized = false;
            updaterLog = null;
        }


        /// <summary>Logs an exception to this mods log or updater log, and to the game log unless indicated otherwise.</summary>
        public static void Exception(Exception ex, LogLevel logLevel = LogLevel.Info, bool hideFromGameLog = false)
        {
            if ((logLevel == Debug) && !ModSettings.DebugMode)
            {
                return;
            }

            // Exception with full or shorter stacktrace.
            Log((logLevel == Debug) ? $"[EXCEPTION][DEBUG] { ex }" : $"[EXCEPTION] { ex.GetType().Name }: { ex.Message }\n{ ex.StackTrace }");

            if (!hideFromGameLog)
            {
                UnityEngine.Debug.LogException(ex);
            }                
        }


        /// <summary>Logs a message to the game log.</summary>
        private static void GameLog(string message)
        {
            UnityEngine.Debug.Log($"{ ModSettings.ModName }: { message }");
        }


        /// <summary>The LogFiler class writes the logging to file.</summary>
        private class LogFiler
        {
            private readonly StreamWriter file;
            private readonly string fileName;


            /// <summary>Default constructor.</summary>
            public LogFiler(string fileFullPath, bool append)
            {
                fileName = fileFullPath;

                try
                {
                    if (!File.Exists(fileName))
                    {
                        file = File.CreateText(fileName);
                    }
                    else if (append && (new FileInfo(fileName).Length < ModSettings.LogMaxSize))
                    {
                        file = File.AppendText(fileName);

                        WriteLine($"\n\n{ new string('=', ModSettings.TextReportWidth) }\n\n", LogLevel.Info, duplicateToGameLog: false, timestamp: false);
                    }
                    else
                    {
                        // Make a backup before overwriting. Can't use Toolkit.CopyFile here because it logs.
                        try
                        {
                            File.Copy(fileName, $"{ fileName }.old", overwrite: true);
                        }
                        catch (Exception ex2)
                        {
                            GameLog($"[WARNING] Can't create backup file \"{ Toolkit.Privacy(fileName) }.old\". { Toolkit.ShortException(ex2) }");
                        }

                        file = File.CreateText(fileName);

                        if (append)
                        {
                            WriteLine($"Older info moved to \"{ Toolkit.GetFileName(fileName) }.old\".\n\n\n{ new string('=', ModSettings.TextReportWidth) }\n\n", 
                                LogLevel.Info, duplicateToGameLog: false, timestamp: false);
                        }
                    }

                    // Auto flush file buffer after every write.
                    file.AutoFlush = true;
                }
                catch (Exception ex)
                {
                    GameLog($"[ERROR] Can't create file \"{ Toolkit.Privacy(fileName) }\". { Toolkit.ShortException(ex) }");
                }
            }


            /// <summary>Writes a message to a log file, with optional loglevel and timestamp, and optionally duplicating to the game log.</summary>
            public void WriteLine(string message, LogLevel logLevel, bool duplicateToGameLog, bool timestamp)
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
                    GameLog($"[ERROR] Can't log to \"{ Toolkit.Privacy(fileName) }\".");
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
