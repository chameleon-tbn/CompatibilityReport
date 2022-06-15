using System.IO;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Settings;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter
{
    public static class Report
    {
        private static bool hasRun;


        /// <summary>Starts the reporter to create a text and/or HTML report.</summary>
        public static void Create(string scene, bool force = false)
        {
            if (hasRun && !force)
            {
                return;
            }

            hasRun = true;

            // Remove the debug logfile from a previous session, if it exists.
            if (!GlobalConfig.Instance.AdvancedConfig.DebugMode)
            {
                Toolkit.DeleteFile(Path.Combine(ModSettings.DebugLogPath, ModSettings.LogFileName));
            }

            Logger.Log($"{ ModSettings.ModName } version { ModSettings.FullVersion }. Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }.");
            Logger.Log($"Reporter started during scene { scene }.", Logger.Debug);

            if (PlatformService.platformType != PlatformType.Steam)
            {
                Logger.Log("Your game has no access to the Steam Workshop, and this mod requires that. No report was generated.", Logger.Error);
                return;
            }
            if (PluginManager.noWorkshop)
            {
                Logger.Log("The game can't access the Steam Workshop because of the '--noWorkshop' launch option. No report was generated.", Logger.Warning);
                return;
            }

            Catalog catalog = Catalog.Load();

            if (catalog == null)
            {
                Logger.Log("Can't load bundled catalog and can't download a new catalog. No report was generated.", Logger.Error);
                return;
            }

            if (Toolkit.CurrentGameVersion() != catalog.GameVersion())
            {
                Logger.Log($"The catalog was updated for game version { Toolkit.ConvertGameVersionToString(catalog.GameVersion()) }. You're using " +
                    $"{ (Toolkit.CurrentGameVersion() < catalog.GameVersion() ? "an older" : "a newer") } version of the game. Results might not be accurate.", 
                    Logger.Warning);
            }

            catalog.ScanSubscriptions();
            Logger.Log($"Reviewed { catalog.ReviewedSubscriptionCount } of your { catalog.SubscriptionCount() } mods.");

            GeneralConfig modConfig = GlobalConfig.Instance.GeneralConfig;
            if (modConfig.HtmlReport)
            {
                HtmlReport htmlReport = new HtmlReport(catalog);
                htmlReport.Create();
            }

            // Always create the text report if the HTML report is disabled, so at least one report is created.
            if (modConfig.TextReport || !modConfig.HtmlReport)
            {
                TextReport textReport = new TextReport(catalog);
                textReport.Create();
            }
        }
    }
}