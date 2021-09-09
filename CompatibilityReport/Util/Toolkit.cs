using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Text;
using ColossalFramework.Plugins;
using ICities;
using CompatibilityReport.CatalogData;

namespace CompatibilityReport.Util
{
    public static class Toolkit
    {
        // Return a short exception message.
        public static string ShortException(Exception ex)
        {
            return $"Exception: { ex.GetType().Name }: { ex.Message }";
        }


        // Delete a file. Returns true on success or a non-existed file, false on a deletion error.
        public static bool DeleteFile(string fullPath)
        {
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Could not delete file \"{ Privacy(fullPath) }\". { ShortException(ex) }", Logger.Debug);
                    return false;
                }
            }

            return true;
        }


        // Move or rename a file.
        public static bool MoveFile(string sourceFullPath, string destinationFullPath)
        {
            try
            {
                File.Move(sourceFullPath, destinationFullPath);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not move file \"{ Privacy(sourceFullPath) }\" to \"{ Privacy(destinationFullPath) }\". { ShortException(ex) }", Logger.Debug);
                return false;
            }
        }


        // Copy a file.
        public static bool CopyFile(string sourceFullPath, string destinationFullPath)
        {
            try
            {
                File.Copy(sourceFullPath, destinationFullPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not copy file \"{ Privacy(sourceFullPath) }\" to \"{ Privacy(destinationFullPath) }\". { ShortException(ex) }", Logger.Debug);
                return false;
            }
        }


        // Save a string to a file.
        public static bool SaveToFile(string message, string fileFullPath, bool createBackup = false)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            if (createBackup)
            {
                CopyFile(fileFullPath, fileFullPath + ".old");
            }

            try
            {
                File.WriteAllText(fileFullPath, message);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not save to file \"{ Privacy(fileFullPath) }\". { ShortException(ex) }", Logger.Debug);
                return false;
            }
        }


        // Remove the Windows username from the '...\AppData\Local' path for privacy reasons.
        // Todo 0.6 Something similar needed for Mac OS X or Linux?
        public static string Privacy(string path)
        {
            int index = path.ToLower().IndexOf("\\appdata\\local");

            if (index == -1)
            {
                return path;
            }
            else
            {
                return "%LocalAppData%" + path.Substring(index + "\\appdata\\local".Length);
            }
        }


        // Return the filename (including extension) from a path.
        public static string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            int index = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/')) + 1;

            return (index == 0 || index == path.Length) ? path : path.Substring(index);
        }


        // Skip validating SSL certificates to avoid "authentication or decryption has failed" errors. Our downloads are not sensitive data and don't need security.
        // Code copied from https://github.com/bloodypenguin/ChangeLoadingImage/blob/master/ChangeLoadingImage/LoadingExtension.cs by bloodypenguin.
        private static readonly RemoteCertificateValidationCallback TLSCallback = (sender, cert, chain, sslPolicyErrors) => true;


        // Download a file. A failed download will be retried a set number of times, unless an unrecoverable TLS error is encountered.
        public static bool Download(string url, string fullPath)
        {
            bool success = false;
            int failedAttempts = 0;

            ServicePointManager.ServerCertificateValidationCallback += TLSCallback;

            while (failedAttempts <= ModSettings.DownloadRetries)
            {
                try
                {
                    using (WebClient webclient = new WebClient())
                    {
                        webclient.DownloadFile(url, fullPath);
                    }

                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.ToString().Contains("Security.Protocol.Tls.TlsException: The authentication or decryption has failed"))
                    {
                        // TLS 1.2+ is not supported by .Net Framework 3.5.
                        Logger.Log($"Download failed because the webserver only supports TLS 1.2 or higher: { url }", Logger.Debug);
                        break;
                    }

                    failedAttempts++;

                    Logger.Log($"Download of \"{ url }\" failed { failedAttempts } time{ (failedAttempts > 1 ? "s" : "") }" + 
                        (failedAttempts <= ModSettings.DownloadRetries ? ", will retry. " : ". Download abandoned. ") + 
                        (ex.Message.Contains("(502) Bad Gateway") ? "Exception: 502 Bad Gateway" : $"{ ShortException(ex) }"), Logger.Debug);
                }
            }

            ServicePointManager.ServerCertificateValidationCallback -= TLSCallback;

            return success;
        }


        // Return the Steam Workshop URL for a mod, or an empty string for a local or builtin mod.
        public static string GetWorkshopUrl(ulong steamID)
        {
            return (steamID > ModSettings.HighestFakeID) ? $"https://steamcommunity.com/sharedfiles/filedetails/?id={ steamID }" : "";
        }


        // Return the Steam Workshop URL for an author.
        public static string GetAuthorWorkshopUrl(ulong steamID, string customURL)
        {
            if (steamID != 0 && steamID != ModSettings.FakeAuthorIDforColossalOrder)
            {
                return $"https://steamcommunity.com/profiles/{ steamID }/myworkshopfiles/?appid=255710&requiredtags[]=Mod";
            }
            else if (!string.IsNullOrEmpty(customURL))
            {
                return $"https://steamcommunity.com/id/{ customURL }/myworkshopfiles/?appid=255710&requiredtags[]=Mod";
            }
            else
            {
                return "";
            }
        }


        // Get the name of a mod. Some mods run code in their IUserMod.Name property or in their constructors, which can cause exceptions. This method handles those.
        // Code is based on https://github.com/CitiesSkylinesMods/AutoRepair/blob/master/AutoRepair/AutoRepair/Descriptors/Subscription.cs by aubergine10.
        public static string GetPluginName(PluginManager.PluginInfo plugin)
        {
            string name = "";

            try
            {
                if (plugin == null)
                {
                    Logger.Log("GetPluginName: plugin is null.", Logger.Debug);
                }
                else if (plugin.userModInstance != null)
                {
                    name = ((IUserMod)plugin.userModInstance).Name;
                }
                else if (string.IsNullOrEmpty(plugin.name))
                {
                    Logger.Log("Can't retrieve plugin name. Both userModInstance and plugin.name are null/empty.", Logger.Debug);
                }
                else
                {
                    name = $"{ plugin.name }";
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Can't retrieve plugin name.", Logger.Debug);
                Logger.Exception(ex, hideFromGameLog: true, debugOnly: true);

                name = "";
            }

            return name;
        }


        // Converts the date and time on Steam Workshop pages to a DateTime.
        public static DateTime ConvertWorkshopDateTime(string dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString))
            {
                return default;
            }

            // Format is either like "12 Mar, 2019 @ 6:11am", or "24 May @ 11:27pm" for current year. Insert the year when it's missing.
            if (!dateTimeString.Contains(", 20"))
            {
                int position = dateTimeString.IndexOf('@') - 1;
                position = (position > 0) ? position : 0;

                dateTimeString = dateTimeString.Insert(position, $", { DateTime.Now.Year }");
            }

            try
            {
                return DateTime.ParseExact(dateTimeString, "d MMM, yyyy @ h:mmtt", new CultureInfo("en-GB"));
            }
            catch
            {
                Logger.Log($"Failed to convert workshop datetime: { dateTimeString }.", Logger.Debug);
                return default;
            }
        }


        // Convert a string to a DateTime.
        public static DateTime ConvertDate(string dateString)
        {
            try
            {
                return DateTime.ParseExact(dateString, "yyyy-MM-dd", new CultureInfo("en-GB"));
            }
            catch
            {
                return default;
            }
        }


        // Return a formatted date string.
        public static string DateString(DateTime date)
        {
            return $"{ date:yyyy-MM-dd}";
        }


        // Return a formatted time string, in seconds or minutes or both.
        public static string TimeString(double milliseconds, bool alwaysShowSeconds = false)
        {
            if (milliseconds < 200)
            {
                return $"{ milliseconds } ms";
            }

            int seconds = (int)Math.Floor(milliseconds / 1000);

            // Decide on when to show minutes, seconds and decimals for seconds.
            bool showMinutes = seconds >= 120;
            bool showSeconds = seconds < 120 || alwaysShowSeconds;
            bool showDecimal = seconds < 10;

            return (showSeconds ? (showDecimal ? $"{ Math.Floor(milliseconds / 100) / 10:F1}" : $"{ seconds }") + " seconds" : "") +
                   (showSeconds && showMinutes ? " (" : "") +
                   (showMinutes ? $"{ Math.Floor((double)seconds / 60):0}:{ seconds % 60:00} minutes" : "") +
                   (showSeconds && showMinutes ? ")" : "");
        }
        
        
        // Convert a string to a Version. This works for both "1.13.3.9" and "1.13.3-f9" formats.
        public static Version ConvertToGameVersion(string versionString)
        {
            try
            {
                string[] elements = versionString.Split(new char[] { '.', '-', 'f' }, StringSplitOptions.RemoveEmptyEntries);

                return new Version(Convert.ToInt32(elements[0]), Convert.ToInt32(elements[1]), Convert.ToInt32(elements[2]), Convert.ToInt32(elements[3]));
            }
            catch
            {
                return UnknownVersion();
            }
        }


        // Convert a game version to a formatted string, in the format of "1.13.3-f9".
        public static string ConvertGameVersionToString(Version version)
        {
            try
            {
                return $"{ version.ToString(3) }-f{ version.Revision }";
            }
            catch
            {
                return version.ToString();
            }
        }


        // Return the unknown version. This is what a null or empty string converts to.
        public static Version UnknownVersion()
        {
            return new Version(0, 0);
        }


        // Return the current game version.
        public static Version CurrentGameVersion()
        {
            return new Version((int)BuildConfig.APPLICATION_VERSION_A, (int)BuildConfig.APPLICATION_VERSION_B,
                (int)BuildConfig.APPLICATION_VERSION_C, (int)BuildConfig.APPLICATION_BUILD_NUMBER);
        }


        // Return the current major game version.
        public static Version CurrentMajorGameVersion()
        {
            return new Version(CurrentGameVersion().Major, CurrentGameVersion().Minor);
        }


        // Convert a string to enum.
        public static T ConvertToEnum<T>(string enumString)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), enumString, ignoreCase: true);
            }
            catch
            {
                return default;
            }
        }


        // Convert a DLC enum to a formatted string.
        public static string ConvertDlcToString(Enums.Dlc dlc)
        {
            return dlc.ToString().Replace("__", ": ").Replace('_', ' ');
        }


        // Convert a string to ulong, for Steam IDs.
        public static ulong ConvertToUlong(string numericString)
        {
            try
            {
                return Convert.ToUInt64(numericString);
            }
            catch
            {
                return 0;
            }
        }


        // Convert a list of strings to a list of ulongs, for Steam IDs.
        public static List<ulong> ConvertToUlong(List<string> numericStrings)
        {
            List<ulong> ulongList = new List<ulong>();

            foreach(string numericString in numericStrings)
            {
                ulongList.Add(ConvertToUlong(numericString));
            }

            return ulongList;
        }


        // Clean certain html codes from a string.
        // Todo 0.4 Is this really needed?
        public static string CleanHtml(string htmlText)
        {
            return string.IsNullOrEmpty(htmlText) ? "" : htmlText.Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        }


        // Get the substring between two search-strings.
        public static string MidString(string original, string leftBoundary, string rightBoundary)
        {
            int indexLeft = original.IndexOf(leftBoundary);

            if (indexLeft == -1)
            {
                return "";
            }

            indexLeft += leftBoundary.Length;
            int indexRight = original.IndexOf(rightBoundary, indexLeft);

            if (indexRight <= indexLeft)
            {
                return "";
            }

            return original.Substring(indexLeft, indexRight - indexLeft);
        }


        // Return a word-wrapped string, with optional indent strings for every line after the first.
        public static string WordWrap(string unwrapped, int maxWidth = 0, string indent = "", string indentAfterNewLine = null)
        {
            if (unwrapped == null || unwrapped.Length <= maxWidth)
            {
                return unwrapped ?? "";
            }

            if (unwrapped.Contains("\n"))
            {
                // Make sure a line end has a space in front of it for easier splitting later.
                unwrapped = unwrapped.Replace("\n", " \n").Replace("  \n", " \n");

                indentAfterNewLine = indentAfterNewLine ?? indent;
            }

            maxWidth = maxWidth == 0 ? ModSettings.TextReportWidth : maxWidth;

            string[] words = unwrapped.Split(' ');
            StringBuilder wrapped = new StringBuilder();
            string line = "";

            foreach (string word in words)
            {
                if (string.IsNullOrEmpty(word))
                {
                    line += " ";
                }
                else if (word[0] == '\n')
                {
                    wrapped.AppendLine(line);
                    line = word.Replace("\n", indentAfterNewLine) + " ";
                }
                else if (line.Length + word.Length >= maxWidth)
                {
                    wrapped.AppendLine(line);
                    line = indent + word + " ";
                }
                else
                {
                    line += word + " ";
                }
            }

            wrapped.Append(line);

            return wrapped.ToString();
        }
    }
}
