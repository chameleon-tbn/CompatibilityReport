using System;
using System.IO;


/// This class is based on the Logger class from Enhanced District Services by Tim / chronofanz:
/// https://github.com/chronofanz/EnhancedDistrictServices/blob/master/Source/Logger.cs


namespace CompatibilityReport.Util
{
    internal static class Logger
    {
        // LogLevel to differentiate between log messages
        internal enum LogLevel
        {
            Info,
            Warning,
            Error,
            Debug
        }

        // Loglevel constants to make the Log calls more readable
        internal const LogLevel info    = LogLevel.Info;
        internal const LogLevel warning = LogLevel.Warning;
        internal const LogLevel error   = LogLevel.Error;
        internal const LogLevel debug   = LogLevel.Debug;

        // The log, updater log and report instances; will be initialized on first use
        private static Filer log;
        private static Filer updaterLog;

        // Keep track if we already written to the log; used to initialize the file on first use
        private static bool logWritten;
        private static bool updaterLogWritten;


        // Here the actual file writing happens     [Todo 0.4] What is the use of deriving this from MonoBehaviour?
        private class Filer : UnityEngine.MonoBehaviour
        {
            private readonly StreamWriter file = null;

            private readonly string fileName = "";

            // Constructor
            internal Filer(string fileFullPath, bool append)
            {
                fileName = fileFullPath;

                try
                {
                    // Check if file already exists
                    if (!File.Exists(fileName))
                    {
                        // File does not exist: just create a new one
                        file = File.CreateText(fileName);
                    }
                    else if (!append || (new FileInfo(fileName).Length > ModSettings.LogMaxSize))
                    {
                        // If overwrite is chosen or the filesize exceeds the maximum, make a backup of the old file; can't use Toolkit.CopyFile here because it logs
                        try
                        {
                            File.Copy(fileName, fileName + ".old", overwrite: true);
                        }
                        catch (Exception ex2)
                        {
                            // Log error to the game log; only place we can safely log to at this point
                            GameLog($"[ERROR] Can't create backup file \"{ Toolkit.Privacy(fileName) }.old\". { ex2.GetType().Name }: { ex2.Message }");
                        }

                        // Overwrite old file by creating a new one
                        file = File.CreateText(fileName);

                        // Indicate were the old info went if append was chosen but the file exceeded max. size
                        if (append)
                        {
                            WriteLine($"Older info moved to \"{ Toolkit.GetFileName(fileName) }.old\".", noTimestamp: true);

                            WriteLine(ModSettings.sessionSeparator, noTimestamp: true);
                        }
                    }
                    else
                    {
                        // Append to existing file
                        file = File.AppendText(fileName);

                        // Write a separator to indicate a new session
                        WriteLine(ModSettings.sessionSeparator, noTimestamp: true);
                    }

                    // Auto flush file buffer after every write
                    file.AutoFlush = true;
                }
                catch (Exception ex)
                {
                    // Log error to the game log; only place we can safely log to at this point
                    GameLog($"[ERROR] Can't create file \"{ Toolkit.Privacy(fileName) }\". { Toolkit.ShortException(ex) }");
                }
            }

            internal void WriteLine(string message, LogLevel logLevel = info, bool duplicateToGameLog = false, bool noTimestamp = false)
            {
                // Don't write anything if we don't have a message
                if (string.IsNullOrEmpty(message))
                {
                    return;
                }
                
                // Date and time as prefix when indicated
                string timeStamp = noTimestamp ? "" : $"{ DateTime.Now:yyyy-MM-dd HH:mm:ss} - ";

                // Loglevel prefix for anything other than info
                string logLevelPrefix = (logLevel == info) ? "" : $"[{ Convert.ToString(logLevel).ToUpper() }] ";

                try
                {
                    // Use a lock for when the updater gets its own executable but still uses the same log file
                    lock (file)
                    {
                        file.WriteLine(timeStamp + logLevelPrefix + message);
                    }
                }
                catch
                {
                    // Log error to the game log; only place we can safely log to at this point
                    GameLog($"[ERROR] Can't write to file \"{ Toolkit.Privacy(fileName) }\".");

                    // Log to the game log instead
                    duplicateToGameLog = true;
                }

                // Duplicate message to game log if indicated, including loglevel prefix
                if (duplicateToGameLog)
                {
                    GameLog(logLevelPrefix + message);
                }

                // Log a debug message when a path is found that might contain the OS username; don't include path to avoid infinite loop
                string lowerCaseMessage = message.ToLower();

                if (lowerCaseMessage.Contains("\\appdata\\local") || lowerCaseMessage.Contains("c:\\users\\") || lowerCaseMessage.Contains("/users/"))
                {
                    Log("Path probably needs more privacy.", logLevel: debug);
                }
            }
        }


        // Log a message to the game log; only called from within the Logger class
        private static void GameLog(string message)
        {
            // Don't write anything if we don't have a message
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            
            // Log with the mod name as a prefix
            UnityEngine.Debug.Log($"{ ModSettings.internalName }: { message }");
        }


        // Log a message to the mod log, and also to the game log if indicated
        internal static void Log(string message, LogLevel logLevel = info, bool duplicateToGameLog = false)
        {
            // Don't log if it's a debug message and we're not in debugmode
            if ((logLevel == debug) && !ModSettings.DebugMode)
            {
                return;
            }

            // Initialize the file on the first message
            if (!logWritten)
            {
                log = new Filer(ModSettings.logfileFullPath, append: ModSettings.LogAppend);

                logWritten = true;

                // Log the logfile location to the game log
                GameLog($"Detailed logging for this mod can be found in \"{ Toolkit.Privacy(ModSettings.logfileFullPath) }\"");
            }

            // Write the message to file, with loglevel prefix, and duplicate to game log if indicated
            log.WriteLine(message, logLevel, duplicateToGameLog);
        }


        // Log a message to the updater log
        internal static void UpdaterLog(string message, LogLevel logLevel = info)
        {
            // Initialize the file on the first message
            if (!updaterLogWritten) 
            {
                updaterLog = new Filer(ModSettings.updaterLogfileFullPath, append: false);

                updaterLogWritten = true;

                // Log the updater logfile location to the game log
                GameLog($"Logging for the updater can be found in \"{ Toolkit.Privacy(ModSettings.updaterLogfileFullPath) }\"");
            }

            // Write the message to file, with loglevel prefix
            updaterLog.WriteLine(message, logLevel);
        }


        // Log exception to mod log or updater log; duplicates to game log if indicated; includes stack trace to help debug the problem
        internal static void Exception(Exception ex, bool hideFromGameLog = false, bool debugOnly = false)
        {
            // Don't log if it's a debug message and we're not in debugmode
            if (debugOnly && !ModSettings.DebugMode)
            {
                return;
            }

            // Log with regular or debug prefix
            string logPrefix = debugOnly ? "[DEBUG EXCEPTION]" : "[EXCEPTION]";

            // Log the exception with or without stack trace
            if (ModSettings.DebugMode)
            {
                // Debug mode: exception with full stacktrace
                Log($"{ logPrefix } { ex }");
            }
            else
            {
                // Exception with short(er) stacktrace
                Log($"{ logPrefix } { Toolkit.ShortException(ex) }\n{ ex.StackTrace }");
            }

            // Log exception to the game log if indicated
            if (!hideFromGameLog)
            {
                UnityEngine.Debug.LogException(ex);
            }                
        }
    }
}
