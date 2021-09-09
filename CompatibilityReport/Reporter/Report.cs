using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;


namespace CompatibilityReport.Reporter
{
    internal static class Report
    {
        private static bool reportCreated;


        // Start the reporter
        internal static void Create(string scene)
        {
            // If not an on-demand report, check if report wasn't already created and if we're in the right 'scene': IntroScreen or Game, depening on user setting
            if (scene != "On-demand")
            {
                if (reportCreated || (ModSettings.ScanBeforeMainMenu && scene != "IntroScreen") || (!ModSettings.ScanBeforeMainMenu && scene != "Game"))
                {
                    return;
                }
            }

            Logger.Log($"{ ModSettings.ModName } version { ModSettings.FullVersion }. Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }. ",
                duplicateToGameLog: true);

            if (PlatformService.platformType != PlatformType.Steam)
            {
                Logger.Log("Your game has no access to the Steam Workshop, and this mod requires that. No report was generated.", 
                    Logger.error, duplicateToGameLog: true);

                return;
            }
            if (PluginManager.noWorkshop)
            {
                Logger.Log("The game can't access the Steam Workshop because of the '--noWorkshop' launch option. No report was generated.",
                    Logger.error, duplicateToGameLog: true);

                return;
            }

            // Load the catalog
            Catalog catalog = Catalog.Load();

            if (catalog == null)
            {
                Logger.Log("Can't load bundled catalog and can't download a new catalog. No report was generated.", Logger.error, duplicateToGameLog: true);

                return;
            }

            Logger.Log(scene == "IntroScreen" ? "Reporter started during game startup." : 
                (scene == "Game" ? "Reporter started during map loading." : "Reporter started for an on-demand report."));

            if (Toolkit.CurrentGameVersion() != catalog.GameVersion())
            {
                Logger.Log($"The catalog was updated for game version { Toolkit.ConvertGameVersionToString(catalog.GameVersion()) }. " +
                    $"You're using { (Toolkit.CurrentGameVersion() < catalog.GameVersion() ? "an older" : "a newer") } version of the game. " +
                    "Results may not be accurate.", Logger.warning, duplicateToGameLog: true);
            }

            // Get all subscription info into the catalog
            catalog.GetSubscriptions();

            Logger.Log($"Reviewed { catalog.ReviewedSubscriptionCount } of your { catalog.SubscriptionIDIndex.Count } mods.");

            // Create the HTML report if selected in settings
            if (ModSettings.HtmlReport)
            {
                reportCreated = HtmlReport.Create(catalog);
            }

            // Create the text report if selected in settings, or if somehow no report was selected in options
            if (ModSettings.TextReport || !ModSettings.HtmlReport)
            {
                reportCreated = reportCreated || TextReport.Create(catalog);
            }

            Logger.Log("Mod has shutdown.", duplicateToGameLog: true);
        }
    }
}