using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Text;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ICities;
using CompatibilityReport.CatalogData;


namespace CompatibilityReport.Util
{
    internal static class Toolkit
    {
        // Current full and major game version
        internal static readonly Version CurrentGameVersion = new Version(
                (int)BuildConfig.APPLICATION_VERSION_A,
                (int)BuildConfig.APPLICATION_VERSION_B,
                (int)BuildConfig.APPLICATION_VERSION_C,
                (int)BuildConfig.APPLICATION_BUILD_NUMBER);

        internal static readonly Version CurrentGameMajorVersion = new Version(CurrentGameVersion.Major, CurrentGameVersion.Minor);

        // Unknown version; a null field written to the catalog comes back like this
        internal static readonly Version UnknownVersion = new Version(0, 0);


        // Delete a file
        internal static bool DeleteFile(string fullPath)
        {
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Could not delete file \"{ PrivacyPath(fullPath) }\". Exception: { ex.GetType().Name } { ex.Message }", Logger.warning);

                    return false;
                }
            }

            // return true if file was deleted or didn't exist
            return true;
        }


        // Move or rename a file
        internal static bool MoveFile(string sourceFullPath, string destinationFullPath)
        {
            try
            {
                File.Move(sourceFullPath, destinationFullPath);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not move file \"{ PrivacyPath(sourceFullPath) }\" to \"{ PrivacyPath(destinationFullPath) }\". " + 
                    $"Exception: { ex.GetType().Name } { ex.Message }", Logger.error);

                return false;
            }
        }


        // Copy a file
        internal static bool CopyFile(string sourceFullPath, string destinationFullPath)
        {
            try
            {
                File.Copy(sourceFullPath, destinationFullPath, overwrite: true);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not copy file \"{ PrivacyPath(sourceFullPath) }\" to \"{ destinationFullPath }\". Exception: { ex.GetType().Name } { ex.Message }", 
                    Logger.error);

                return false;
            }
        }


        // Save a string to a file
        internal static bool SaveToFile(string message, string fileFullPath, bool createBackup = false)
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
                Logger.Log($"Could not save to file \"{ PrivacyPath(fileFullPath) }\". Exception: { ex.GetType().Name } { ex.Message }", Logger.error);

                return false;
            }
        }


        // Remove the Windows username from the '...\AppData\Local' path for privacy reasons; [Todo 0.6] Something similar needed for Mac OS X or Linux?
        internal static string PrivacyPath(string path)
        {
            int index = path.ToLower().IndexOf("\\appdata\\local");

            if (index == -1)
            {
                // Not found, return original path
                return path;
            }
            else
            {
                index += "\\appdata\\local".Length;

                // Replace everything up to and including \appdata\local with %LocalAppData%; path will still work in Windows and is now a bit more privacy-proof
                return "%LocalAppData%" + path.Substring(index);
            }
        }


        // Return the filename (with extension) from a path
        internal static string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            int index = path.LastIndexOf('\\');

            return index < 1 || index == path.Length ? path : path.Substring(index + 1, path.Length - index - 1);
        }


        // ValidationCallback gets rid of "The authentication or decryption has failed" errors when downloading, allowing sites that still support TLS 1.1 or worse
        // Code copied from https://github.com/bloodypenguin/ChangeLoadingImage/blob/master/ChangeLoadingImage/LoadingExtension.cs by bloodypenguin
        private static readonly RemoteCertificateValidationCallback TLSCallback = (sender, cert, chain, sslPolicyErrors) => true;


        // Download a file, return the exception for custom logging
        internal static Exception Download(string url, string fullPath)
        {
            Exception exception = null;

            int failedAttempts = 0;

            // Activate TLS callback
            ServicePointManager.ServerCertificateValidationCallback += TLSCallback;

            // Download with retries
            while (failedAttempts <= ModSettings.downloadRetries)
            {
                using (WebClient webclient = new WebClient())
                {
                    try
                    {
                        webclient.DownloadFile(url, fullPath);

                        // Reset the exception value
                        exception = null;

                        // No (more) retries needed
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Download failed, increase try count
                        failedAttempts++;

                        Logger.Log($"Download of \"{ url }\" failed { failedAttempts } time{ (failedAttempts > 1 ? "s" : "") }" + 
                            (failedAttempts <= ModSettings.downloadRetries ? ", will retry. " : ". Skipped. ") + 
                            (ex.Message.Contains("(502) Bad Gateway") ? "Error: 502 Bad Gateway" : $"Exception: { ex.GetType().Name } { ex.Message }"), Logger.debug);

                        exception = ex;
                    }
                }
            }

            // Deactivate TLS callback
            ServicePointManager.ServerCertificateValidationCallback -= TLSCallback;

            return exception;
        }


        // Is the Steam Workshop available in game?
        internal static bool IsSteamWorkshopAvailable()
        {
            return (PlatformService.platformType == PlatformType.Steam) && !PluginManager.noWorkshop;
        }


        // Return Steam Workshop url for a mod
        internal static string GetWorkshopURL(ulong steamID)
        {
            // No URL for fake Steam IDs
            if (steamID > ModSettings.highestFakeID)
            {
                return $"https://steamcommunity.com/sharedfiles/filedetails/?id={ steamID }";
            }
            else
            {
                return "";
            }
        }


        // Return Steam Workshop url for an author
        internal static string GetAuthorWorkshop(ulong steamID, string customURL)
        {
            if (steamID != 0 && steamID != ModSettings.fakeAuthorIDforColossalOrder)
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


        // Get the name of a mod, as safely as possible.
        // Some mods run code in their IUserMod.Name property, or run code in their constructors, which can cause exceptions - this method handles those.
        // Code based on https://github.com/CitiesSkylinesMods/AutoRepair/blob/master/AutoRepair/AutoRepair/Descriptors/Subscription.cs by aubergine10
        internal static string GetPluginName(PluginManager.PluginInfo plugin)
        {
            string name = "";

            try
            {
                if (plugin == null)
                {
                    Logger.Log("GetPluginName: plugin is null.", Logger.debug);
                }
                else if (plugin.userModInstance != null)
                {
                    name = ((IUserMod)plugin.userModInstance).Name;
                }
                else if (string.IsNullOrEmpty(plugin.name))
                {
                    Logger.Log("Can't retrieve plugin name. Both userModInstance and plugin.name are null/empty.", Logger.warning);
                }
                else
                {
                    name = $"{ plugin.name }";
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Can't retrieve plugin name.", Logger.warning);

                Logger.Exception(ex, hideFromGameLog: true);

                name = "";
            }

            return name;
        }


        // Converts the string date/time on Steam Workshop pages to a DateTime
        internal static DateTime ConvertWorkshopDateTime(string dateTimeString)
        {
            // Only convert if we really have a string
            if (string.IsNullOrEmpty(dateTimeString))
            {
                return default;
            }

            DateTime convertedDate;

            // Date format on the workshop is either like "12 Mar, 2019 @ 6:11am", or like "24 May @ 11:27pm" for current year
            if (!dateTimeString.Contains(", 20"))
            {
                // Date without year; insert the current year
                int position = dateTimeString.IndexOf('@') - 1;

                dateTimeString = dateTimeString.Insert(position, $", { DateTime.Now.Year }");
            }

            // Date format should now always be like "24 May, 2021 @ 11:27pm"
            try
            {
                convertedDate = DateTime.ParseExact(dateTimeString, "d MMM, yyyy @ h:mmtt", new CultureInfo("en-GB"));
            }
            catch
            {
                // Couldn't convert; probably got a faulty string
                convertedDate = default;

                Logger.Log($"Failed to convert workshop datetime: { dateTimeString }.", Logger.warning);
            }

            return convertedDate;            
        }


        // Return a formatted date string
        internal static string DateString(DateTime date)
        {
            return $"{ date:yyyy-MM-dd}";
        }


        // Convert a string to a datetime
        internal static DateTime ConvertDate(string dateString)
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


        // Convert a string to a version type
        internal static Version ConvertToGameVersion(string versionString)
        {
            try
            {
                // Try to get only the numbers for either "1.13.3.9" or "1.13.3-f9" version format
                string[] elements = versionString.Split(new char[] { '.', '-', 'f' }, StringSplitOptions.RemoveEmptyEntries);

                return new Version(Convert.ToInt32(elements[0]), Convert.ToInt32(elements[1]), Convert.ToInt32(elements[2]), Convert.ToInt32(elements[3]));
            }
            catch
            {
                // Conversion failed
                return UnknownVersion;
            }
        }


        // Convert a game version to a formatted string, in the commonly used format as shown on the Main Menu and the Paradox Launcher
        internal static string ConvertGameVersionToString(Version version)
        {
            try
            {
                // This will throw an exception on a short Version like (0, 0)
                return $"{ version.ToString(3) }-f{ version.Revision }";
            }
            catch
            {
                return version.ToString();
            }
        }
        
        
        // Convert a string to enum
        internal static T ConvertToEnum<T>(string enumString)
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


        // Convert a DLC enum to a formatted string
        internal static string ConvertDLCtoString(Enums.Dlc dlc)
        {
            return dlc.ToString().Replace("__", ": ").Replace('_', ' ');
        }


        // Convert a string to ulong, for Steam IDs
        internal static ulong ConvertToUlong(string numericString)
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


        // Convert a list of strings to a list of ulongs, for Steam IDs
        internal static List<ulong> ConvertToUlong(List<string> numericStrings)
        {
            List<ulong> ulongList = new List<ulong>();

            foreach(string numericString in numericStrings)
            {
                ulongList.Add(ConvertToUlong(numericString));
            }

            return ulongList;
        }


        // Clean a html string from certain html codes      [Todo 0.4] Is this really needed?
        internal static string CleanHtml(string text)
        {
            return string.IsNullOrEmpty(text) ? "" : text.Replace("&quot;", "\"").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        }
        

        // Get the substring between two search-strings
        internal static string MidString(string original, string leftBoundary, string rightBoundary)
        {
            // Get the position of the left boundary string
            int indexLeft = original.IndexOf(leftBoundary);

            if (indexLeft < 1)
            {
                // Left boundary string not found
                return "";
            }

            // Increase the left boundary index to the end of the left boundary string
            indexLeft += leftBoundary.Length;

            // Get the position of the right boundary string
            int indexRight = original.IndexOf(rightBoundary, indexLeft);

            if (indexRight < indexLeft)
            {
                // Right boundary string not found
                return "";
            }

            return original.Substring(indexLeft, indexRight - indexLeft);
        }


        // Return a word-wrapped string, with optional indent string"s for every line after the first
        internal static string WordWrap(string unwrapped, int maxWidth = ModSettings.ReportWidth, string indent = "", string indentAfterNewLine = null)
        {
            if (unwrapped == null || unwrapped.Length <= maxWidth)
            {
                return unwrapped ?? "";
            }

            if (unwrapped.Contains("\n"))
            {
                // Make sure a line end has a space in front of it for easier splitting later
                unwrapped = unwrapped.Replace("\n", " \n").Replace("  \n", " \n");

                // If no special indent string was supplied, assume the regular indent string for these lines
                indentAfterNewLine = indentAfterNewLine ?? indent;
            }

            StringBuilder wrapped = new StringBuilder();

            string line = "";

            string[] words = unwrapped.Split(' ');

            foreach (string word in words)
            {
                if (string.IsNullOrEmpty(word))
                {
                    // Write a space if we get an empty word, which could happen if a string has multiple concurrent spaces
                    line += " ";
                }
                else if (word[0] == '\n')
                {
                    // Start on a new line if we encounter a new line character
                    wrapped.AppendLine(line);

                    // Get rid of the new line character from the word and insert an indent string
                    line = word.Replace("\n", indentAfterNewLine) + " ";
                }
                else if (line.Length + word.Length >= maxWidth)
                {
                    // Start on a new line if we would go over the max width
                    wrapped.AppendLine(line);

                    line = indent + word + " ";
                }
                else
                {
                    line += word + " ";
                }
            }

            // Add the last line
            wrapped.Append(line);

            return wrapped.ToString();
        }


        // Return a formatted elapsed time string, in seconds or minutes or both
        internal static string ElapsedTime(double milliseconds, bool alwaysShowSeconds = false)
        {
            // Return the time in milliseconds if it's less than 0.2 seconds
            if (milliseconds < 200)
            {
                return $"{ milliseconds } ms";
            }

            int seconds = (int)Math.Floor(milliseconds / 1000);

            // Number of seconds when to switch from showing seconds to minutes
            long threshold = 120;

            // Decide on when to show seconds, decimals (for seconds only) and minutes
            bool showMinutes = seconds > threshold;
            bool showSeconds = seconds <= threshold || alwaysShowSeconds;
            bool showDecimal = seconds < 10;
            
            return (showSeconds ? (showDecimal ? $"{ Math.Floor(milliseconds / 100) / 10:F1}" : $"{ seconds }") + " seconds" : "") + 
                   (showSeconds && showMinutes ? " (" : "") + 
                   (showMinutes ? $"{ Math.Floor((double)seconds / 60):0}:{ seconds % 60:00} minutes" : "") + 
                   (showSeconds && showMinutes ? ")" : "");
        }
    }
}
