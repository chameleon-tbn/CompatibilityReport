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

            Logger.Log($"{ ModSettings.modName } version { ModSettings.fullVersion }. Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion) }. ",
                duplicateToGameLog: true);

            if (!Toolkit.IsSteamWorkshopAvailable())
            {
                Logger.Log("The game can't access the Steam Workshop, and thus has no subscriptions to check. No report was generated. " +
                    "This is expected behaviour if you used the '--noWorkshop' parameter.", Logger.warning, duplicateToGameLog: true);

                return;
            }

            // Load the catalog
            Catalog ActiveCatalog = Catalog.InitActive();

            if (ActiveCatalog == null)
            {
                Logger.Log("Can't load bundled catalog and can't download a new catalog. No report was generated.", Logger.error, duplicateToGameLog: true);

                return;
            }

            Logger.Log(scene == "IntroScreen" ? "Reporter started during game startup." : 
                (scene == "Game" ? "Reporter started during map loading." : "Reporter started for an on-demand report."));

            if (Toolkit.CurrentGameVersion != ActiveCatalog.CompatibleGameVersion)
            {
                Logger.Log($"The catalog was updated for game version { Toolkit.ConvertGameVersionToString(ActiveCatalog.CompatibleGameVersion) }. " +
                    $"You're using { (Toolkit.CurrentGameVersion < ActiveCatalog.CompatibleGameVersion ? "an older" : "a newer") } version of the game. " +
                    "Results may not be accurate.", Logger.warning, duplicateToGameLog: true);
            }

            // Get all subscriptions, including all builtin and local mods, with info from game and catalog
            ActiveSubscriptions.Get();

            Logger.Log($"Reviewed { ActiveSubscriptions.TotalReviewed } of your { ActiveSubscriptions.All.Count } mods ");

            // Create the HTML report if selected in settings
            if (ModSettings.HtmlReport)
            {
                reportCreated = HtmlReport.Create(ActiveCatalog);
            }

            // Create the text report if selected in settings, or if somehow no report was selected in options
            if (ModSettings.TextReport || !ModSettings.HtmlReport)
            {
                reportCreated = reportCreated || TextReport.Create(ActiveCatalog);
            }

            // Clean up memory
            ActiveSubscriptions.Clear();

            Catalog.CloseActive();

            Logger.Log("Mod has shutdown.", duplicateToGameLog: true);
        }
    }
}