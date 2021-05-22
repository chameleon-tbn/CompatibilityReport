using System;
using System.IO;
using System.Net;
using System.Net.Security;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using static ColossalFramework.Plugins.PluginManager;
using ICities;
using ModChecker.DataTypes;


namespace ModChecker.Util
{
    internal static class Tools
    {
        // ValidationCallback to get rid of "The authentication or decryption has failed." errors when downloading
        // This allows to download from sites that still support TLS 1.1 or worse, but not from sites that only support TLS 1.2+
        // Code copied from https://github.com/bloodypenguin/ChangeLoadingImage/blob/master/ChangeLoadingImage/LoadingExtension.cs by bloodypenguin
        private static readonly RemoteCertificateValidationCallback TLSCallback = (sender, cert, chain, sslPolicyErrors) => true;


        // Delete a file
        internal static bool DeleteFile(string fullPath)
        {
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Could not delete file \"{ fullPath }\".", Logger.error);

                    Logger.Exception(ex);
                }
            }

            return false;
        }


        // Download a file, return the exception for custom logging
        internal static Exception Download(string url, string fullPath, uint retriesOnError = ModSettings.downloadRetries)
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

                        exception = null;

                        // No (more) retries needed
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Download failed, increase try count
                        failedAttempts++;

                        Logger.Log($"Download of \"{ url }\" failed. { (failedAttempts <= retriesOnError ? "Retrying. " : "") }Exception: { ex.GetType().Name }", 
                            Logger.debug);

                        exception = ex;
                    }
                }
            }

            // Deactivate TLS callback
            ServicePointManager.ServerCertificateValidationCallback -= TLSCallback;

            return exception;
        }


        // Is the Steam Workshop available in game?
        internal static bool SteamWorkshopAvailable { get; private set; } = (PlatformService.platformType == PlatformType.Steam && !PluginManager.noWorkshop);


        // Return Steam Workshop url for a mod
        internal static string GetWorkshopURL(ulong steamID)
        {
            // No URL for fake Steam IDs
            if (steamID > ModSettings.HighestFakeID)
            {
                return $"https://steamcommunity.com/sharedfiles/filedetails/?id={ steamID }";
            }
            else
            {
                return "";
            }
        }


        // Return Steam Workshop url for an author
        internal static string GetAuthorWorkshop(string authorID, bool isProfile)
        {
            if (isProfile)
            {
                return $"https://steamcommunity.com/profiles/{ authorID }/myworkshopfiles/?appid=255710";
            }
            else
            {
                return $"https://steamcommunity.com/id/{ authorID }/myworkshopfiles/?appid=255710";
            }
            
        }


        // Get the name of a mod, as safely as possible.
        // Some mods run code in their IUserMod.Name property, or run code in their static or instance constructors, which can cause exceptions - this method handles those.
        // Code based on https://github.com/CitiesSkylinesMods/AutoRepair/blob/master/AutoRepair/AutoRepair/Descriptors/Subscription.cs by aubergine10
        internal static string GetPluginName(PluginInfo plugin)
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
                    name = $"({plugin.name})";
                }
            }
            catch (Exception ex)
            {
                Logger.Log("GetPluginName: can't retrieve plugin name.", Logger.debug);

                Logger.Exception(ex, debugOnly: true, duplicateToGameLog: false);

                name = "";
            }

            return name;
        }


        // Remove the Windows username from the '...\AppData\Local' path for privacy reasons
        // Unfinished: For Mac OS X, maybe /Users/<username>/Library/ to ~/Library/ ???
        internal static string PrivacyPath(string path)
        {
            // Get position of \appdata\local in the path
            int index = path.ToLower().IndexOf("\\appdata\\local");
            int indexPlus = index + "\\appdata\\local".Length;

            if (index == -1)
            {
                // Return original path if \appdata\local was not found
                return path;
            }
            else
            {
                // Replace everything up to and including \appdata\local with %LocalAppData%; path will still work in Windows and is now more privacy-proof
                return "%LocalAppData%" + path.Substring(indexPlus);
            }
        }


        // Convert a string to a version type
        internal static Version ConvertToGameVersion(string versionString)
        {
            Version version;

            try
            {
                string[] versionArray = versionString.Split('.');

                version = new Version(
                    Convert.ToInt32(versionArray[0]),
                    Convert.ToInt32(versionArray[1]),
                    Convert.ToInt32(versionArray[2]),
                    Convert.ToInt32(versionArray[3]));
            }
            catch
            {
                // Conversion failed
                version = GameVersion.Unknown;
            }

            return version;
        }


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
    }
}
