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

        // The log, report and unknown-mods objects
        private static readonly MyFile log = new MyFile(ModSettings.LogfileFullPath, timeStamp: true, append: ModSettings.LogAppend);
        private static readonly MyFile report = new MyFile(ModSettings.ReportTextFullPath, timeStamp: false, append: false);

        // Keep track of the first message send to the game log
        private static bool firstGameLogMessage = true;

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

            internal void WriteLine(string message)
            {
                // Date and time as prefix when indicated
                string prefix = (useTimeStamps) ? DateTime.Now.ToString() + " - " : "";

                try
                {
                    lock (file)
                    {
                        file.WriteLine(prefix + message);
                    }
                }
                catch
                {
                    // Log error to the game log; only place we can safely log to at this point
                    Game($"[ERROR] Can't write to file \"{ Tools.PrivacyPath(fileName) }\".");
                }
            }
        }


        // Log a message to the mod log, and also to the game log and report if indicated
        internal static void Log(string message, LogLevel logLevel = info, bool gameLog = false)
        {
            // Don't log if we don't have a message or if it's a debug message and we're not in debugmode
            if (string.IsNullOrEmpty(message) || ((logLevel == debug) && !ModSettings.DebugMode))
            {
                return;
            }

            // Log a debug message when '\appdata\local' path found, which contains the Windows username
            if (message.IndexOf("\\appdata\\local") > 0)
            {
                Log("Path needs more privacy.", logLevel: debug);
            }

            // Loglevel prefix for anything other than info
            string logPrefix = (logLevel == info) ? "" : $"[{ Convert.ToString(logLevel).ToUpper() }] ";

            // Write the message with prefix
            log.WriteLine(logPrefix + message);

            // Duplicate message to game log if indicated, including loglevel prefix
            if (gameLog)
            {
                Game(logPrefix + message);
            }
        }


        // Log a message to the game log; only called from within the Logger class
        private static void Game(string message)
        {
            // Log with the mod name as a prefix
            UnityEngine.Debug.Log($"{ ModSettings.internalName }: { message }");

            // Tell the log location name after the first message to the game log
            if (firstGameLogMessage)
            {
                UnityEngine.Debug.Log($"{ ModSettings.internalName }: Detailed logging for this mod can be found in \"{ Tools.PrivacyPath(ModSettings.LogfileFullPath) }\"");

                firstGameLogMessage = false;
            }
        }


        // Log exception to mod log and if indicated to game log, including stack trace to help debug the problem
        internal static void Exception(Exception ex, bool debugOnly = false, bool gameLog = true, bool stackTrace = true)
        {
            // Only write to log files when DebugModeOnly is not requested or we are in debug mode
            if (!debugOnly || ModSettings.DebugMode)
            {
                // Log with regular or debug prefix
                string logPrefix = (debugOnly ? "[DEBUG EXCEPTION]" : "[EXCEPTION]");

                if (ModSettings.DebugMode)
                {
                    // Exception with full stacktrace
                    Log($"{ logPrefix } { ex }");
                }
                else if (stackTrace)
                {
                    // Exception with short stacktrace; if missing vital information, then retry in debug mode
                    Log($"{ logPrefix } { ex.GetType().Name }: { ex.Message }\n" +
                        $"{ ex.StackTrace }");
                }
                else
                {
                    // Exception without stacktrace
                    Log($"{ logPrefix } { ex.GetType().Name }: { ex.Message }");
                }                

                // Log exception to game log if indicated
                if (gameLog)
                {
                    UnityEngine.Debug.LogException(ex);
                }                
            }
        }


        // Log a message to the report
        internal static void Report(string message, LogLevel logLevel = info)
        {
            // Don't report if we don't have a message
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            // Report with loglevel prefix for anything other than info
            string logPrefix = (logLevel == info) ? "" : $"{ logLevel }: ";

            report.WriteLine(logPrefix + message);
        }
    }
}
