using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter
{
    public static class Report
    {
        private static bool hasRun;


        /// <summary>Starts the reporter to create a text and/or HTML report.</summary>
        public static void Create(string scene)
        {
            if (scene != "On-demand")
            {
                if (hasRun || (ModSettings.ScanBeforeMainMenu && scene != "IntroScreen") || (!ModSettings.ScanBeforeMainMenu && scene != "Game"))
                {
                    return;
                }
            }

            hasRun = true;

            Logger.Log($"{ ModSettings.ModName } version { ModSettings.FullVersion }. Game version " +
                $"{ Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }.", duplicateToGameLog: true);

            if (PlatformService.platformType != PlatformType.Steam)
            {
                Logger.Log("Your game has no access to the Steam Workshop, and this mod requires that. No report was generated.", Logger.Error, duplicateToGameLog: true);
                return;
            }
            if (PluginManager.noWorkshop)
            {
                Logger.Log("The game can't access the Steam Workshop because of the '--noWorkshop' launch option. No report was generated.",
                    Logger.Warning, duplicateToGameLog: true);

                return;
            }

            Catalog catalog = Catalog.Load();

            if (catalog == null)
            {
                Logger.Log("Can't load bundled catalog and can't download a new catalog. No report was generated.", Logger.Error, duplicateToGameLog: true);
                return;
            }

            Logger.Log(scene == "IntroScreen" ? "Reporter started during game startup." : 
                (scene == "Game" ? "Reporter started during map loading." : "Reporter started for an on-demand report."));

            if (Toolkit.CurrentGameVersion() != catalog.GameVersion())
            {
                Logger.Log($"The catalog was updated for game version { Toolkit.ConvertGameVersionToString(catalog.GameVersion()) }. You're using " +
                    $"{ (Toolkit.CurrentGameVersion() < catalog.GameVersion() ? "an older" : "a newer") } version of the game. Results may not be accurate.", 
                    Logger.Warning);
            }

            catalog.ScanSubscriptions();
            Logger.Log($"Reviewed { catalog.ReviewedSubscriptionCount } of your { catalog.SubscriptionCount() } mods.");

            if (ModSettings.HtmlReport)
            {
                // Todo 1.1 Create HTML report.
            }

            // Always create the text report if the HTML report is disabled, so at least one report is created.
            if (ModSettings.TextReport || !ModSettings.HtmlReport)
            {
                TextReport.Create(catalog);
            }

            Logger.Log("Mod has finished.", duplicateToGameLog: true);
        }
    }
}