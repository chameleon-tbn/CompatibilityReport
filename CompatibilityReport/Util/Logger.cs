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


        // Log a message to this mods log, and optionally to the game log.
        public static void Log(string message, LogLevel logLevel = LogLevel.Info, bool duplicateToGameLog = false)
        {
            if ((logLevel == Debug) && !ModSettings.DebugMode)
            {
                return;
            }

            if (!regularLogInitialized)
            {
                regularLog = new LogFiler(ModSettings.LogfileFullPath, append: ModSettings.LogAppend);

                regularLogInitialized = true;

                GameLog($"Detailed logging for this mod can be found in \"{ Toolkit.Privacy(ModSettings.LogfileFullPath) }\".");
            }

            regularLog.WriteLine(message, logLevel, duplicateToGameLog, timestamp: true);
        }


        // Log a message to the updater log.
        public static void UpdaterLog(string message, LogLevel logLevel = LogLevel.Info)
        {
            if (!updaterLogInitialized)
            {
                updaterLog = new LogFiler(ModSettings.UpdaterLogfileFullPath, append: false);

                updaterLogInitialized = true;

                GameLog($"Logging for the updater can be found in \"{ Toolkit.Privacy(ModSettings.UpdaterLogfileFullPath) }\".");
            }

            updaterLog.WriteLine(message, logLevel, duplicateToGameLog: false, timestamp: true);
        }


        // Log exception to mod log or updater log, and to the game log unless indicated otherwise.
        public static void Exception(Exception ex, bool hideFromGameLog = false, bool debugOnly = false)
        {
            if (debugOnly && !ModSettings.DebugMode)
            {
                return;
            }

            string logPrefix = debugOnly ? "[EXCEPTION][DEBUG]" : "[EXCEPTION]";

            if (ModSettings.DebugMode)
            {
                // Exception with full stacktrace.
                Log($"{ logPrefix } { ex }");
            }
            else
            {
                // Exception with shorter stacktrace.
                Log($"{ logPrefix } { Toolkit.ShortException(ex) }\n{ ex.StackTrace }");
            }

            if (!hideFromGameLog)
            {
                UnityEngine.Debug.LogException(ex);
            }                
        }


        // Log a message to the game log.
        private static void GameLog(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            UnityEngine.Debug.Log($"{ ModSettings.ModName }: { message }");
        }


        // The LogFiler class writes the logging to file, with optional loglevel and timestamp, and optionally duplicating to the game log.
        private class LogFiler
        {
            private readonly StreamWriter file;
            private readonly string fileName;

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
                            File.Copy(fileName, fileName + ".old", overwrite: true);
                        }
                        catch (Exception ex2)
                        {
                            GameLog($"[WARNING] Can't create backup file \"{ Toolkit.Privacy(fileName) }.old\". { Toolkit.ShortException(ex2) }");
                        }

                        file = File.CreateText(fileName);

                        if (append)
                        {
                            WriteLine($"Older info moved to \"{ Toolkit.GetFileName(fileName) }.old\".", LogLevel.Info, duplicateToGameLog: false, timestamp: false);
                            WriteLine($"\n\n{ new string('=', ModSettings.TextReportWidth) }\n\n", LogLevel.Info, duplicateToGameLog: false, timestamp: false);
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

            public void WriteLine(string message, LogLevel logLevel, bool duplicateToGameLog, bool timestamp)
            {
                if (string.IsNullOrEmpty(message))
                {
                    return;
                }

                string timeStamp = timestamp ? $"{ DateTime.Now:yyyy-MM-dd HH:mm:ss} - " : "";
                string logLevelPrefix = (logLevel == LogLevel.Info) ? "" : $"[{ Convert.ToString(logLevel).ToUpper() }] ";

                try
                {
                    // Use a lock for when the updater gets its own executable but still uses the same log file.
                    lock (file)
                    {
                        file.WriteLine(timeStamp + logLevelPrefix + message);
                    }
                }
                catch
                {
                    GameLog($"[ERROR] Can't log to \"{ Toolkit.Privacy(fileName) }\".");
                    duplicateToGameLog = true;
                }

                if (duplicateToGameLog)
                {
                    GameLog(logLevelPrefix + message);
                }

                string lowerCaseMessage = message.ToLower();
                if (lowerCaseMessage.Contains("\\appdata\\local") || lowerCaseMessage.Contains("c:\\users\\") || lowerCaseMessage.Contains("/users/"))
                {
                    Log("Previously logged path probably needs more privacy.", LogLevel.Debug);
                }
            }
        }
    }
}
