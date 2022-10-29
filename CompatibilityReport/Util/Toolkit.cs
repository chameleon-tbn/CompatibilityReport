using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Text;
using ColossalFramework.Plugins;
using ICities;
using CompatibilityReport.CatalogData;
using UnityEngine;
using UnityEngine.Networking;
using GlobalConfig = CompatibilityReport.Settings.GlobalConfig;

namespace CompatibilityReport.Util
{
    public static class Toolkit
    {
        private static string modPath = "";

        public static string GetModPath() {
            if (string.IsNullOrEmpty(modPath))
            {
                Assembly thisAssembly = Assembly.GetExecutingAssembly();
                var plugins = PluginManager.instance.GetPluginsInfo();
                foreach (PluginManager.PluginInfo pluginInfo in plugins)
                {
                    try
                    {
                        foreach (Assembly assembly in pluginInfo.GetAssemblies())
                        {
                            if (assembly == thisAssembly)
                            {
                                modPath = pluginInfo.modPath;
                                return modPath;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Exception(e);
                    }
                }

            }
            return modPath;
        }
        
        public static bool IsMainMenuScene(string scene)
        {
            return scene == "Startup" || scene == "MainMenu" || scene == "IntroScreen" || scene == "IntroScreen2";
        } 
        
        /// <summary>Creates a short exception message.</summary>
        /// <returns>Exception message string.</returns>
        public static string ShortException(Exception ex)
        {
            return $"Exception: { ex.GetType().Name }";
        }


        /// <summary>Creates a directory.</summary>
        /// <remarks>This does not log any errors, because it is used by the Logger itself.</remarks>
        /// <returns>True if succesful or the directory already exist, false on a creation error.</returns>
        public static bool CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(ModSettings.DebugLogPath);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>Deletes a file.</summary> 
        /// <remarks>This does not log any errors, because it is used by the Logger itself.</remarks>
        /// <returns>True if succesful or the file didn't exist, false on a deletion error.</returns>
        public static bool DeleteFile(string fullPath)
        {
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>Moves or renames a file.</summary>
        /// <remarks>If the destination already exists, it will be overwritten. This does not log any errors.</remarks>
        /// <returns>True if succesful, false on errors.</returns>
        public static bool MoveFile(string sourceFullPath, string destinationFullPath)
        {
            if (!File.Exists(sourceFullPath))
            {
                return false;
            }

            DeleteFile(destinationFullPath);

            try
            {
                File.Move(sourceFullPath, destinationFullPath);
                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>Copies a file.</summary>
        /// <remarks>If the destination already exists, it will be overwritten. This does not log any errors, because it is used by the Logger itself.</remarks>
        /// <returns>True if succesful, false on errors.</returns>
        public static bool CopyFile(string sourceFullPath, string destinationFullPath)
        {
            if (!File.Exists(sourceFullPath))
            {
                return false;
            }

            try
            {
                File.Copy(sourceFullPath, destinationFullPath, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>Saves a string to a file.</summary>
        /// <remarks>Overwrites the file by default. Optionally create a backup of the old file or appends instead of overwriting.</remarks>
        /// <returns>True if succesful, false on errors.</returns>
        public static bool SaveToFile(string message, string fileFullPath, bool append = false, bool createBackup = false)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            if (createBackup)
            {
                CopyFile(fileFullPath, $"{ fileFullPath }.old");
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
                Logger.Log($"Could not save to file \"{ Privacy(fileFullPath) }\". { ShortException(ex) }", Logger.Debug);
                return false;
            }
        }


        /// <summary>Removes the Windows username from the '...\AppData\Local' path for privacy reasons.</summary>
        /// <remarks>This will also remove inconsistency between slashes and backslashes in the path.</remarks>
        /// <returns>A path string with a little more privacy.</returns>
        // Todo 1.x Something similar for MacOS or Linux?
        public static string Privacy(string path)
        {
            path = path.Replace('/', Path.DirectorySeparatorChar);

            int index = path.ToLower().IndexOf("\\appdata\\local");
            return (index == -1) ? path : $"%LocalAppData%{ path.Substring(index + "\\appdata\\local".Length) }";
        }


        // Skip validating SSL certificates to avoid "authentication or decryption has failed" errors. Our downloads are not sensitive data and don't need security.
        // Code copied from https://github.com/bloodypenguin/ChangeLoadingImage/blob/master/ChangeLoadingImage/LoadingExtension.cs by bloodypenguin.
        private static readonly RemoteCertificateValidationCallback TLSCallback = (sender, cert, chain, sslPolicyErrors) => true;

        /// <summary>Download a file to a location on disk.</summary>
        /// <remarks>A failed download will be retried a set number of times, unless an unrecoverable TLS error is encountered.</remarks>
        /// <returns>True if succesful, false on errors.</returns>
        public static bool Download(string url, string fullPath)
        {
            bool success = false;
            int failedAttempts = 0;

            ServicePointManager.ServerCertificateValidationCallback += TLSCallback;

            int downloadRetries = GlobalConfig.Instance.AdvancedConfig.DownloadRetries;
            while (failedAttempts <= downloadRetries)
            {
                try
                {
                    using (WebClient webClient = new WebClient())
                    {
                        webClient.Proxy = null;
                        webClient.DownloadFile(url, fullPath);
                    }

                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Log("Exception on download: \n" + ex);
                    if (ex.ToString().Contains("Security.Protocol.Tls.TlsException: The authentication or decryption has failed"))
                    {
                        // TLS 1.2+ is not supported by .Net Framework 3.5.
                        Logger.Log($"Download failed because the webserver only supports TLS 1.2 or higher: { url }", Logger.Debug);
                        break;
                    }

                    failedAttempts++;

                    Logger.Log($"Download of \"{ url }\" failed { failedAttempts } time{ (failedAttempts > 1 ? "s" : "") }" + 
                        (failedAttempts <= downloadRetries ? ", will retry. " : ". Download abandoned. ") + 
                        (ex.Message.Contains("(502) Bad Gateway") ? "Exception: 502 Bad Gateway" : $"{ ShortException(ex) }"), Logger.Debug);
                }
            }

            ServicePointManager.ServerCertificateValidationCallback -= TLSCallback;
            return success;
        }
        
#if CATALOG_DOWNLOAD
        /// <summary>Uploads a file to a location on disk.</summary>
        /// <remarks>A failed download will be retried a set number of times, unless an unrecoverable TLS error is encountered.</remarks>
        /// <returns>True if succesful, false on errors.</returns>
        public static bool Upload(string url, string fullPath, string username, string password)
        {
            bool success = false;
            int failedAttempts = 0;

            ServicePointManager.ServerCertificateValidationCallback += TLSCallback;

            int uploadRetries = GlobalConfig.Instance.AdvancedConfig.UploadRetries;
            while (failedAttempts <= uploadRetries)
            {
                try
                {
                    using (WebClient webClient = new WebClient())
                    {
                        webClient.Proxy = null;
                        webClient.Credentials = new NetworkCredential(username, password);
                        var response = webClient.UploadFile(url, fullPath);
                        Logger.Log($"Upload response: {Encoding.ASCII.GetString(response)}");
                    }

                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Log("Exception on upload: \n" + ex);
                    if (ex.ToString().Contains("Security.Protocol.Tls.TlsException: The authentication or decryption has failed"))
                    {
                        // TLS 1.2+ is not supported by .Net Framework 3.5.
                        Logger.Log($"Upload failed because the webserver only supports TLS 1.2 or higher: { url }", Logger.Debug);
                        break;
                    }

                    failedAttempts++;

                    Logger.Log($"Upload of \"{ url }\" failed { failedAttempts } time{ (failedAttempts > 1 ? "s" : "") }" + 
                        (failedAttempts <= uploadRetries ? ", will retry. " : ". Upload abandoned. ") + 
                        (ex.Message.Contains("(502) Bad Gateway") ? "Exception: 502 Bad Gateway" : $"{ ShortException(ex) }"), Logger.Debug);
                }
            }

            ServicePointManager.ServerCertificateValidationCallback -= TLSCallback;
            return success;
        }
#endif       
        
        /// <summary>
        /// Reads page and invokes onComplete action when finished or failed
        /// </summary>
        /// <returns>Enumerator that can be used for creating non-blocking process, invokec onComplete with pari of {status, page (if success otherwise null)}</returns>
        public static IEnumerator DownloadPageText(string url, Action<KeyValuePair<bool, string>> onComplete)
        {
            int failedAttempts = 0;

            int downloadRetries = GlobalConfig.Instance.AdvancedConfig.DownloadRetries;
            while (failedAttempts <= downloadRetries)
            {
                using (UnityWebRequest uwr = UnityWebRequest.Get(url))
                {
                    yield return uwr.Send();

                    // wait until done (either with or without errors)
                    while (!uwr.isDone)
                    {
                        yield return null;
                    }
                    
                    if (uwr.isError)
                    {
                        Logger.Log($"Download of \"{url}\" failed {failedAttempts} time{(failedAttempts > 1 ? "s" : "")}" +
                            (failedAttempts <= downloadRetries ? ", will retry.  " : ". Download abandoned. ") + $"{uwr.error}");
                        failedAttempts++;
                    }
                    else
                    {
                        onComplete(new KeyValuePair<bool, string>(true, uwr.downloadHandler.text));
                        yield break;
                    }
                }
            }
            yield return null;
            onComplete(new KeyValuePair<bool, string>(false, null));
        } 

        /// <summary>Get the Steam Workshop URL for a mod.</summary>
        /// <remarks>It does not check if the given Steam ID is an existing Steam Workshop mod.</remarks>
        /// <returns>A string with the URL, or an empty string for a local or built-in mod.</returns>
        public static string GetWorkshopUrl(ulong steamID)
        {
            return (steamID > ModSettings.HighestFakeID) ? $"https://steamcommunity.com/sharedfiles/filedetails/?id={ steamID }" : "";
        }

        public static string GetDirectoryUrl(string path)
        {
            return  $"file:///{path}";
        }


        /// <summary>Get the Steam Workshop URL for an author.</summary>
        /// <remarks>It does not check if the given Steam ID or Custom URL is an existing Steam Workshop author.</remarks>
        /// <returns>A string with the URL, or an empty string for the fake Colossal Order author or if both parameters are zero/empty.</returns>
        public static string GetAuthorWorkshopUrl(ulong steamID, string customUrl)
        {
            if (steamID != 0 && steamID != ModSettings.FakeAuthorIDforColossalOrder)
            {
                return $"https://steamcommunity.com/profiles/{ steamID }/myworkshopfiles/?appid=255710&requiredtags[]=Mod";
            }
            else if (!string.IsNullOrEmpty(customUrl))
            {
                return $"https://steamcommunity.com/id/{ customUrl }/myworkshopfiles/?appid=255710&requiredtags[]=Mod";
            }
            
            return "";
        }


        /// <summary>Get the name of a mod.</summary>
        /// <remarks>Some mods run code in their IUserMod.Name property or in their constructors, which can cause exceptions. This method handles those.</remarks>
        /// <returns>A string with the mod name, or an empty string if we cannot get the name.</returns>
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
                else if (!string.IsNullOrEmpty(plugin.name))
                {
                    name = $"{ plugin.name }";
                }
                else
                {
                    Logger.Log("Can't retrieve plugin name. Both plugin.userModInstance.Name and plugin.name are null/empty.", Logger.Debug);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Can't retrieve plugin name.", Logger.Debug);
                Logger.Exception(ex, Logger.Debug);

                name = "";
            }

            return name;
        }


        /// <summary>Determines the kind of change between an old and a new value.</summary>
        /// <returns>The string "added", "removed" or "changed".</returns>
        public static string GetChange(object oldValue, object newValue)
        {
            return oldValue == default ? "added" : newValue == default ? "removed" : "changed";
        }


        /// <summary>Determines the kind of change between an old and a new string.</summary>
        /// <returns>The string "added", "removed" or "changed".</returns>
        public static string GetChange(string oldValue, string newValue)
        {
            return string.IsNullOrEmpty(oldValue) ? "added" : string.IsNullOrEmpty(newValue) ? "removed" : "changed";
        }


        /// <summary>Determines the kind of change between an old and a new stability.</summary>
        /// <returns>The string "added", "removed" or "changed".</returns>
        public static string GetChange(Enums.Stability oldValue, Enums.Stability newValue)
        {
            return oldValue <= Enums.Stability.NotReviewed ? "added" : newValue <= Enums.Stability.NotReviewed ? "removed" : "changed";
        }


        /// <summary>Determines the kind of change between an old and a new Version.</summary>
        /// <returns>The string "added", "removed" or "changed".</returns>
        public static string GetChange(Version oldValue, Version newValue)
        {
            return oldValue == default || oldValue == UnknownVersion() ? "added" :
                newValue == default || newValue == UnknownVersion() ? "removed" : "changed";
        }
        
        
        /// <summary>Converts a date/time string as seen on Steam Workshop pages.</summary>
        /// <remarks>The date/time string needs to be in a format similar to '10 Mar, 2015 @ 11:22am', or '10 Mar @ 3:45pm' for current year.</remarks>
        /// <returns>The date and time as a DateTime in the UTC timezone, or the default lowest DateTime value if conversion failed.</returns>
        public static DateTime ConvertWorkshopDateTime(string dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString))
            {
                return default;
            }

            // Insert the year if it's missing.
            if (!dateTimeString.Contains(", 20"))
            {
                int position = dateTimeString.IndexOf('@') - 1;
                position = (position > 0) ? position : 0;

                dateTimeString = dateTimeString.Insert(position, $", { DateTime.Now.Year }");
            }

            try
            {
                return DateTime.ParseExact($"{ dateTimeString } { ModSettings.DefaultSteamTimezone }", "d MMM, yyyy @ h:mmtt zzz", CultureInfo.InvariantCulture).ToUniversalTime();
            }
            catch
            {
                Logger.Log($"Failed to convert Steam Workshop datetime: { dateTimeString }.", Logger.Debug);
                return default;
            }
        }


        /// <summary>Converts a date from a string to a DateTime.</summary>
        /// <remarks>The date needs to be in the format 'yyyy-mm-dd'. Time will be set at noon UTC.</remarks>
        /// <returns>The date as a DateTime in the UTC timezone, or the default lowest DateTime value if conversion failed.</returns>
        public static DateTime ConvertDate(string dateString)
        {
            try
            {
                return DateTime.ParseExact($"{ dateString } 12:00 +00:00", "yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture).ToUniversalTime();
            }
            catch
            {
                return default;
            }
        }


        /// <summary>Converts a date to a string.</summary>
        /// <returns>A string with the date in the format 'yyyy-mm-dd'.</returns>
        public static string DateString(DateTime date)
        {
            return $"{ date:yyyy-MM-dd}";
        }


        /// <summary>Converts an elapsed time to a string.</summary>
        /// <remarks>This will return the time in milliseconds if less than 200ms, in seconds with one decimal if less than 10s, in seconds without decimals 
        ///          if less than 120s, or in minutes (mm:ss). Optionally will show the total number of seconds next to the minutes.</remarks>
        /// <returns>A string with the elapsed time in milliseconds, seconds and/or minutes.</returns>
        public static string TimeString(double milliseconds, bool alwaysShowSeconds = false)
        {
            if (milliseconds < 200)
            {
                return $"{ milliseconds:0} ms";
            }

            double seconds = Math.Round(milliseconds / 1000);

            // Decide on when to show minutes, seconds and decimals for seconds.
            bool showMinutes = seconds >= 120;
            bool showSeconds = seconds < 120 || alwaysShowSeconds;
            bool showDecimal = milliseconds < 9950;

            return (showSeconds ? (showDecimal ? $"{ milliseconds / 1000:F1} seconds" : $"{ seconds:0} seconds") : "") +
                   (showSeconds && showMinutes ? " (" : "") +
                   (showMinutes ? $"{ Math.Floor(seconds / 60):0}:{ seconds % 60:00} minutes" : "") +
                   (showSeconds && showMinutes ? ")" : "");
        }


        /// <summary>Cleans a DateTime value by removing the milliseconds and converting it to UTC timezone.</summary>
        /// <returns>A DateTime without milliseconds, in the UTC timezone.</returns>
        public static DateTime CleanDateTime(DateTime dirtyDateTime)
        {
            return new DateTime(dirtyDateTime.Year, dirtyDateTime.Month, dirtyDateTime.Day, dirtyDateTime.Hour, dirtyDateTime.Minute, dirtyDateTime.Second,
                dirtyDateTime.Kind).ToUniversalTime();
        }


        /// <summary>Converts a string to a Version.</summary>
        /// <remarks>This works for both 1.13.3.9 and 1.13.3-f9 formats.</remarks>
        /// <returns>A Version representing the string value, or Version 0.0 if conversion failed.</returns>
        public static Version ConvertToVersion(string versionString)
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


        /// <summary>Converts a Version to a game version string.</summary>
        /// <returns>A string with the version in a format like 1.13.3-f9, or in dotted format like 1.13.3.9 if conversions failed.</returns>
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


        /// <summary>Get the version 0.0, in this mod also known as the unknown version.</summary>
        /// <remarks>This is what null, an empty string or an unrecognized version string converts to, including any version with less than 4 elements.</remarks>
        /// <returns>Version 0.0</returns>
        public static Version UnknownVersion()
        {
            return new Version(0, 0);
        }


        /// <summary>Get the current game version.</summary>
        /// <returns>The current game version.</returns>
        public static Version CurrentGameVersion()
        {
            return ConvertToVersion(BuildConfig.applicationVersion);
        }


        /// <summary>Get the current major game version.</summary>
        /// <returns>The current major game version, like 1.13.</returns>
        public static Version CurrentMajorGameVersion()
        {
            return new Version(CurrentGameVersion().Major, CurrentGameVersion().Minor);
        }


        /// <summary>Converts a string to an enum.</summary>
        /// <remarks>T is the enum type. The string is not case-sensitive.</remarks>
        /// <returns>The enum representation of the string value, or the default value of the enum type if conversion failed.</returns>
        public static T ConvertToEnum<T>(string enumString)
        {
            try
            {
                T enumValue = (T)Enum.Parse(typeof(T), enumString, ignoreCase: true);

                return Enum.IsDefined(typeof(T), enumValue) ? enumValue : default;
            }
            catch
            {
                return default;
            }
        }


        /// <summary>Converts a DLC enum to a string.</summary>
        /// <remarks>The name of the DLC enum is returned, with double underscores replaced by colon+space, and single underscores replaced by a space.</remarks>
        /// <returns>A string with the DLC name.</returns>
        public static string ConvertDlcToString(Enums.Dlc dlc)
        {
            return dlc.ToString().Replace("__", ": ").Replace('_', ' ');
        }


        /// <summary>Converts a string to an ulong, for a Steam ID.</summary>
        /// <returns>An ulong representing the string value, or zero if conversion failed.</returns>
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


        /// <summary>Converts a list of strings to a list of ulongs, for Steam IDs.</summary>
        /// <returns>A list of ulongs, with a value of zero for strings that couldn't be converted.</returns>
        public static List<ulong> ConvertToUlong(List<string> numericStrings)
        {
            List<ulong> ulongList = new List<ulong>();

            foreach (string numericString in numericStrings)
            {
                ulongList.Add(ConvertToUlong(numericString));
            }

            return ulongList;
        }


        /// <summary>Cleans certain HTML codes from a string, by converting them to the correct text characters.</summary>
        /// <remarks>This converts the following characters: &quot;, &lt;, &gt; and &amp;.</remarks>
        /// <returns>The string with HTML codes converted to their text characters.</returns>
        public static string CleanHtml(string htmlText)
        {
            return string.IsNullOrEmpty(htmlText) ? "" : htmlText.Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        }


        /// <summary>Gets the substring between two 'boundary' substrings in a string.</summary>
        /// <returns>The found substring, or an empty string if either of the 'boundary' substrings is not found.</returns>
        public static string MidString(string original, string leftBoundary, string rightBoundary)
        {
            int indexLeft = original.IndexOf(leftBoundary);

            if (indexLeft == -1)
            {
                return "";
            }

            indexLeft += leftBoundary.Length;
            int indexRight = original.IndexOf(rightBoundary, indexLeft);

            return (indexRight <= indexLeft) ? "" : original.Substring(indexLeft, indexRight - indexLeft);
        }


        /// <summary>Word-wraps a text.</summary>
        /// <remarks>By default the report width is used and no indenting. An indent string can be supplied for use on every line after the first.
        ///          Pptionally a different indent string can be supplied for use after newline characters ('\n') found in the text, for instance for bullets.</remarks>
        /// <returns>The word-wrapped, and optionally indented, string.</returns>
        public static string WordWrap(string unwrapped, int maxWidth = 0, string indent = "", string indentAfterNewLine = null)
        {
            if (string.IsNullOrEmpty(unwrapped))
            {
                return "";
            }

            if (unwrapped.Contains("\n"))
            {
                // Make sure a line end has a space in front of it for easier splitting later.
                unwrapped = unwrapped.Replace("\n", " \n").Replace("  \n", " \n");

                indentAfterNewLine = indentAfterNewLine ?? indent;
            }
            else if (unwrapped.Length <= maxWidth)
            {
                return unwrapped;
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
                    line = $"{ word.Replace("\n", indentAfterNewLine) } ";
                }
                else if (line.Length + word.Length >= maxWidth)
                {
                    wrapped.AppendLine(line);
                    line = $"{ indent }{ word } ";
                }
                else
                {
                    line += $"{ word } ";
                }
            }

            wrapped.Append(line);

            return wrapped.ToString();
        }


        /// <summary>Cuts off a text at a given width.</summary>
        /// <returns>The cut off string.</returns>
        public static string CutOff(string text, int width)
        {
            text = text.Contains("\n") ? text.Substring(0, text.IndexOf("\n")): text;

            return text.Length <= width ? text : $"{ text.Substring(0, width - 3) }...";
        }
#if CATALOG_DOWNLOAD
        public static bool IsOlderThanFrequencyOption(DateTime lastWriteTimeUtc, int downloadFrequency)
        {
            switch (downloadFrequency)
            {
                case 0: 
                    return lastWriteTimeUtc.Date <= DateTime.Today.Subtract(TimeSpan.FromHours(12));//once every 12h
                case 1:
                    return lastWriteTimeUtc.Date <= DateTime.Today.Subtract(TimeSpan.FromDays(7));//once a week
                case 2:
                    return false;//never
                default:
                    Logger.Log($"Not implemented frequency option: {downloadFrequency}", Logger.LogLevel.Warning);
                    return false;
            }
        }
#endif
    }
}
