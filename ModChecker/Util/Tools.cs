using System;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using static ColossalFramework.Plugins.PluginManager;
using ICities;
using ModChecker.DataTypes;


namespace ModChecker.Util
{
    internal static class Tools
    {
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
        internal static string GetAuthorWorkshop(string authorTag)
        {
            return $"https://steamcommunity.com/profiles/{ authorTag }/myworkshopfiles/?appid=255710";
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

                Logger.Exception(ex, debugOnly: true, gameLog: false);

                name = "";
            }

            return name;
        }


        // Remove the Windows username from the '...\AppData\Local' path for privacy reasons
        // Unfinished: Mac OS X, might be /Users/<username>/Library/ to ~/Library/
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
    }
}
