using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Text;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ICities;
using ModChecker.DataTypes;


namespace ModChecker.Util
{
    internal static class Toolkit
    {
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


        // Rename a file
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
        internal static bool CopyFile(string sourceFullPath,
                                      string destinationFullPath,
                                      bool overwrite = true)
        {
            try
            {
                File.Copy(sourceFullPath, destinationFullPath, overwrite);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not copy file \"{ PrivacyPath(sourceFullPath) }\" to \"{ destinationFullPath }\". Exception: { ex.GetType().Name } { ex.Message }", 
                    Logger.error);

                return false;
            }
        }


        // Save string to file
        internal static bool SaveToFile(string message,
                                        string fileFullPath,
                                        bool append = false)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            try
            {
                if (append)
                {
                    File.AppendAllText(fileFullPath, message);
                }
                else
                {
                    File.WriteAllText(fileFullPath, message);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not save text to file \"{ PrivacyPath(fileFullPath) }\". Exception: { ex.GetType().Name } { ex.Message }", Logger.error);

                return false;
            }
        }


        // Remove the Windows username from the '...\AppData\Local' path for privacy reasons; [Todo 0.5] Something similar needed for Mac OS X or Linux?
        internal static string PrivacyPath(string path)
        {
            // Get position of \appdata\local in the path
            int index = path.ToLower().IndexOf("\\appdata\\local");
            int indexPlus = index + "\\appdata\\local".Length;

            if (index == -1)
            {
                // Not found, return original path
                return path;
            }
            else
            {
                // Replace everything up to and including \appdata\local with %LocalAppData%; path will still work in Windows and is now more privacy-proof
                return "%LocalAppData%" + path.Substring(indexPlus);
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

            return index == 0 || index == path.Length ? path : path.Substring(index + 1, path.Length - index - 1);
        }


        // ValidationCallback gets rid of "The authentication or decryption has failed" errors when downloading
        // This allows to download from sites that still support TLS 1.1 or worse, but not from sites that only support TLS 1.2+
        // Code copied from https://github.com/bloodypenguin/ChangeLoadingImage/blob/master/ChangeLoadingImage/LoadingExtension.cs by bloodypenguin
        private static readonly RemoteCertificateValidationCallback TLSCallback = (sender, cert, chain, sslPolicyErrors) => true;


        // Download a file, return the exception for custom logging
        internal static Exception Download(string url,
                                           string fullPath,
                                           uint retriesOnError = ModSettings.downloadRetries)
        {
            Exception exception = null;

            uint failedAttempts = 0;

            // Activate TLS callback
            ServicePointManager.ServerCertificateValidationCallback += TLSCallback;

            // Download with retries
            while (failedAttempts <= retriesOnError)
            {
                using (WebClient webclient = new WebClient())
                {
                    try
                    {
                        webclient.DownloadFile(url, fullPath);

                        // Reset the exception value to return
                        exception = null;

                        // No (more) retries needed
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Download failed, increase try count
                        failedAttempts++;

                        Logger.Log($"Download of \"{ url }\" failed { failedAttempts } time{ (failedAttempts > 1 ? "s" : "") }" + 
                            (failedAttempts <= retriesOnError ? ", will retry. " : ". Skipped. ") + 
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
        internal static bool SteamWorkshopAvailable()
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
        internal static string GetAuthorWorkshop(ulong profileID, string customURL, bool modsOnly = false)
        {
            if (profileID != 0)
            {
                return $"https://steamcommunity.com/profiles/{ profileID }/myworkshopfiles/?appid=255710" + (modsOnly ? "&requiredtags[]=Mod" : "");
            }
            else if (!string.IsNullOrEmpty(customURL))
            {
                return $"https://steamcommunity.com/id/{ customURL }/myworkshopfiles/?appid=255710" + (modsOnly ? "&requiredtags[]=Mod" : "");
            }
            else
            {
                return "";
            }
        }


        // Return Steam Store url for a DLC
        internal static string GetDLCStorePage(uint appID)
        {
            return $"https://store.steampowered.com/app/{ appID }";
        }


        // Get the name of a mod, as safely as possible.
        // Some mods run code in their IUserMod.Name property, or run code in their static or instance constructors, which can cause exceptions - this method handles those.
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
                    Logger.Log("GetPluginName: both userModInstance and plugin.name are null/empty.", Logger.debug);
                }
                else
                {
                    name = $"{ plugin.name }";
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Can't retrieve plugin name.", Logger.debug);

                Logger.Exception(ex, debugOnly: true, duplicateToGameLog: false);

                name = "";
            }

            return name;
        }


        // Converts the string date/time on Steam Workshop pages and return it as a proper datetime
        internal static DateTime ConvertWorkshopDateTime(string dateTimeString)
        {
            // Only convert if we really have a string; MinValue is the DateTime equivalent of null
            if (string.IsNullOrEmpty(dateTimeString))
            {
                return DateTime.MinValue;
            }

            DateTime convertedDate;

            // Date format on the workshop is either like "12 Mar, 2019 @ 6:11am", or "24 May @ 11:27pm" for current year
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
                convertedDate = DateTime.MinValue;

                Logger.Log($"Failed to convert workshop datetime: { dateTimeString }.", Logger.warning);
            }

            return convertedDate;            
        }


        // Return a formatted date string
        internal static string DateString(DateTime date) => $"{ date:yyyy-MM-dd}";


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
                return GameVersion.Unknown;
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
                return default(T);
            }
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


        // Clean a html string from html codes
        internal static string CleanString(string text) => text == null ? "" : text.Replace("&amp;", "&").Replace("&quot;", "\"");
        

        // Get the substring between two search-string in a string
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


        // Return a formatted elapsed time string, in seconds or minutes or both
        internal static string ElapsedTime(double milliseconds,
                                           bool alwaysShowSeconds = false)
        {
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
