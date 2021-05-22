using System;
using System.IO;
using static ModChecker.DataTypes.Enums;


/// This class is based on the Logger class from Enhanced District Services by Tim / chronofanz:
/// https://github.com/chronofanz/EnhancedDistrictServices/blob/master/Source/Logger.cs


namespace ModChecker.Util
{
    internal static class Logger
    {
        // Loglevel constants to make the Log calls more readable
        internal const LogLevel info    = LogLevel.Info;
        internal const LogLevel warning = LogLevel.Warning;
        internal const LogLevel error   = LogLevel.Error;
        internal const LogLevel debug   = LogLevel.Debug;

        // The log, updater log and report objects; log is initialized immediately
        private static readonly MyFile log = new MyFile(ModSettings.LogfileFullPath, timeStamp: true, append: ModSettings.LogAppend);
        private static MyFile updaterLog;
        private static MyFile report;

        // Keep track if we already logged the logfile location to the game log
        private static bool loggedLogfileLocationToGameLog = false;

        // Here the actual file writing happens
        private class MyFile : UnityEngine.MonoBehaviour
        {
            private readonly StreamWriter file = null;
            private readonly bool useTimeStamps = false;
            private readonly string fileName = "";

            // Constructor
            internal MyFile(string fileFullPath, bool timeStamp, bool append)
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
                    else
                    {
                        long fileSize = new FileInfo(fileName).Length;

                        if (fileSize == 0)
                        {
                            // File was created last session but nothing was written
                            Log($"Empty file was found: \"{ Tools.PrivacyPath(fileName) }\". This is not necessarily an error.", LogLevel.Debug);

                            // Overwrite empty file by creating a new one
                            file = File.CreateText(fileName);
                        }
                        else if (!append || (fileSize > ModSettings.LogMaxSize))
                        {
                            // If overwrite is chosen or the filesize exceeds the maximum, make a backup of the old file
                            try
                            {
                                File.Copy(fileName, fileName + ".old", overwrite: true);
                            }
                            catch
                            {
                                Game($"[ERROR] Can't create backup file \"{ Tools.PrivacyPath(fileName) }.old\".");
                            }

                            // Overwrite old file by creating a new one
                            file = File.CreateText(fileName);

                            // Indicate were the old info went if append was chosen but the file exceeded max. size
                            if (append)
                            {
                                WriteLine($"Older info moved to \"{ Tools.PrivacyPath(fileName) }.old\".");

                                WriteLine(ModSettings.sessionSeparator);
                            }
                        }
                        else
                        {
                            // Append to existing file
                            file = File.AppendText(fileName);

                            // Write a separator to indicate a new session
                            WriteLine(ModSettings.sessionSeparator);
                        }
                    }

                    // Auto flush, so we don't have to manually flush and close later
                    file.AutoFlush = true;

                    // Change useTimeStamps here and not earlier, so the session separator is written without timestamp
                    useTimeStamps = timeStamp;
                }
                catch
                {
                    // Log error to the game log; only place we can safely log to at this point
                    Game($"[ERROR] Can't create file \"{ Tools.PrivacyPath(fileName) }\".");
                }
            }

            internal void WriteLine(string message, LogLevel logLevel = info, bool gameLog = false)
            {
                // Don't write anything if we don't have a message
                if (string.IsNullOrEmpty(message))
                {
                    return;
                }
                
                // Date and time as prefix when indicated
                string prefix = useTimeStamps ? DateTime.Now.ToString() + " - " : "";

                // Loglevel prefix for anything other than info
                string logPrefix = (logLevel == info) ? "" : $"[{ Convert.ToString(logLevel).ToUpper() }] ";

                try
                {
                    lock (file)
                    {
                        file.WriteLine(prefix + logPrefix + message);
                    }
                }
                catch
                {
                    // Log error to the game log; only place we can safely log to at this point
                    Game($"[ERROR] Can't write to file \"{ Tools.PrivacyPath(fileName) }\".");
                }

                // Duplicate message to game log if indicated, including loglevel prefix
                if (gameLog)
                {
                    Game(logPrefix + message);
                }
            }
        }


        // Initialize the updater logfile
        internal static void InitUpdaterLog()
        {
            updaterLog = new MyFile(ModSettings.UpdaterLogfileFullPath, timeStamp: true, append: ModSettings.LogAppend);
        }


        // Initialize the report
        internal static void InitReport()
        {
            report = new MyFile(ModSettings.ReportTextFullPath, timeStamp: false, append: false);
        }


        // Log a message to the game log; only called from within the Logger class
        private static void Game(string message)
        {
            // Log with the mod name as a prefix
            UnityEngine.Debug.Log($"{ ModSettings.internalName }: { message }");

            // Log the logfile location after the first message to the game log
            if (!loggedLogfileLocationToGameLog)
            {
                UnityEngine.Debug.Log($"{ ModSettings.internalName }: Detailed logging for this mod can be found in \"{ Tools.PrivacyPath(ModSettings.LogfileFullPath) }\"");

                loggedLogfileLocationToGameLog = true;
            }
        }


        // Log a message to the mod log, and also to the game log if indicated
        internal static void Log(string message, LogLevel logLevel = info, bool gameLog = false)
        {
            // Don't log if we it's a debug message and we're not in debugmode
            if ((logLevel == debug) && !ModSettings.DebugMode)
            {
                return;
            }

            // Log a debug message when '\appdata\local' path found, which contains the Windows username
            if (message.IndexOf("\\appdata\\local") > 0)
            {
                Log("Path needs more privacy.", logLevel: debug);
            }

            // Write the message to file, with loglevel prefix, and duplicate to game log if indicated
            log.WriteLine(message, logLevel, gameLog);
        }


        // Log a message to the updater log
        internal static void UpdaterLog(string message, LogLevel logLevel = info, bool regularLog = false)
        {
            // Write the message to file, with loglevel prefix
            updaterLog.WriteLine(message, logLevel);

            // Duplicate the message to the regular log if indicated
            if (regularLog)
            {
                Log(message, logLevel);
            }
        }


        // Log a message to the report
        internal static void Report(string message)
        {
            // Write the message to file
            report.WriteLine(message);
        }


        // Log exception to mod log and if indicated to game log, including stack trace to help debug the problem
        internal static void Exception(Exception ex, bool debugOnly = false, bool toUpdaterLog = false, bool duplicateToGameLog = true, bool stackTrace = true)
        {
            // Only write to log files when DebugModeOnly is not requested or we are in debug mode
            if (!debugOnly || ModSettings.DebugMode)
            {
                string message;

                // Log with regular or debug prefix
                string logPrefix = (debugOnly ? "[DEBUG EXCEPTION]" : "[EXCEPTION]");

                if (ModSettings.DebugMode)
                {
                    // Exception with full stacktrace
                    message = $"{ logPrefix } { ex }";
                }
                else if (stackTrace)
                {
                    // Exception with short stacktrace; if missing vital information, then retry in debug mode
                    message = $"{ logPrefix } { ex.GetType().Name }: { ex.Message }\n" +
                        $"{ ex.StackTrace }";
                }
                else
                {
                    // Exception without stacktrace
                    message = $"{logPrefix} {ex.GetType().Name}: {ex.Message}";
                }                

                if (toUpdaterLog)
                {
                    UpdaterLog(message);
                }
                else
                {
                    Log(message);
                }

                // Log exception to game log if indicated
                if (duplicateToGameLog)
                {
                    UnityEngine.Debug.LogException(ex);
                }                
            }
        }
    }
}
