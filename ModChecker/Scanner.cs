using System;
using System.Diagnostics;
using ModChecker.DataTypes;
using ModChecker.Util;


namespace ModChecker
{
    internal static class Scanner
    {
        // Keep track of run status
        private static bool initDone;
        private static bool scanDone;


        // Init: basic checks and loading the catalog
        // Called early during game startup, before subscriptions can be seen
        internal static void Init()
        {
            // Don't run Init more than once
            if (initDone)
            {
                Logger.Log("Scanner Init called more than once.", Logger.debug);

                return;
            }

            // Keep track of elapsed time
            Stopwatch timer = Stopwatch.StartNew();

            // Log mod and game version
            Logger.Log($"{ ModSettings.modName } version { ModSettings.fullVersion }. Game version { GameVersion.Formatted(GameVersion.Current) }. ", duplicateToGameLog: true);

            Logger.Log($"{ GameVersion.SpecialNote }", duplicateToGameLog: true);

            // Exit if Steam Workshop is not available in game
            if (!Tools.SteamWorkshopAvailable())
            {
                Logger.Log("The game can't access the Steam Workshop, and thus has no subscriptions to check. No report was generated.\n" + 
                    "This is expected behaviour if you used the '--noWorkshop' parameter.", Logger.warning, duplicateToGameLog: true);

                return;
            }

            // Initialize the catalog; exit if no valid catalog can be loaded
            if (!ActiveCatalog.Init())
            {
                Logger.Log("Can't load bundled catalog and can't download a new catalog. No report was generated.", Logger.error, duplicateToGameLog: true);

                return;
            }

            // Log catalog details
            Logger.Log($"Using catalog { ActiveCatalog.Instance.VersionString() }, created on { ActiveCatalog.Instance.UpdateDate.ToLongDateString() }. " + 
                $"Catalog contains { ActiveCatalog.Instance.ReviewCount } reviewed mods and " + 
                $"{ ActiveCatalog.Instance.Count - ActiveCatalog.Instance.ReviewCount } mods with basic information.", duplicateToGameLog: true);

            // Log if the game version differs from the version the catalog was made for
            if (GameVersion.Current != ActiveCatalog.Instance.CompatibleGameVersion)
            {
                string olderNewer = (GameVersion.Current < ActiveCatalog.Instance.CompatibleGameVersion) ? "an older" : "a newer";

                Logger.Log($"The catalog was updated for game version { GameVersion.Formatted(ActiveCatalog.Instance.CompatibleGameVersion) }. " +
                    $"You're using { olderNewer } version of the game. Results may not be accurate.", Logger.warning, duplicateToGameLog: true);
            }

            timer.Stop();

            Logger.Log($"Scanner initialized in { Tools.FormattedTime(timer.ElapsedMilliseconds, showDecimal: true) }. Now waiting for all mods to load.", 
                extraLine: true, duplicateToGameLog: true);

            // Indicate that Init has run
            initDone = true;
        }


        // Start a scan and create the report(s), but first more checks
        // Called late on game startup (scene = "IntroScreen", just before main menu), during map loading ("Game") or on-demand ("On-demand")
        // Also called whenever mod options are opened
        internal static void Scan(string scene)
        {
            // If not an on-demand scan, check if scan wasn't already done and if in the right 'scene' (IntroScreen or Game, depening on user setting)
            if (scene != "On-demand")
            {
                if (scanDone || (ModSettings.ScanBeforeMainMenu && (scene != "IntroScreen")) || (!ModSettings.ScanBeforeMainMenu && (scene != "Game")))
                {
                    return;
                }
            }

            // Only scan if Init has completed without issues
            if (!initDone)
            {
                Logger.Log("Scanner called without succesfull Init earlier. Cannot create a report.", Logger.error);

                return;
            }

            // Keep track of elapsed time
            Stopwatch timer = Stopwatch.StartNew();

            // if we don't have an active catalog, initialize it again; exit if we can't
            if (!ActiveCatalog.Init())
            {
                Logger.Log("Scanner called without an active catalog, and can't load a catalog. No report was generated.", Logger.error, duplicateToGameLog: true);

                return;
            }

            string message = (scene == "IntroScreen") ? "Scan started during game startup, before main menu." 
                          : ((scene == "Game")        ? "Scan started during map loading." 
                                                      : "On-demand scan started.");

            Logger.Log(message, duplicateToGameLog: true);

            // Get all subscriptions, including all builtin and local mods, with info from game and catalog
            ActiveSubscriptions.Get();

            // Create the report(s)
            Reporter.Create();

            // Log number of mods, runtime and report location
            timer.Stop();

            Logger.Log($"Scan complete. Reviewed { ActiveSubscriptions.TotalReviewed } of your { ActiveSubscriptions.All.Count } mods " + 
                $"in { Tools.FormattedTime(timer.ElapsedMilliseconds, showDecimal: true) }.", duplicateToGameLog: true);

            // Indicate that we've completed a scan
            scanDone = true;

            // Close and clean up
            Close();
        }


        // Clean up
        internal static void Close()
        {
            if (!initDone)
            {
                Logger.Log("Scanner asked to close without an Init done.", Logger.debug);
            }

            // Clear the subscriptions dictionaries and lists
            ActiveSubscriptions.Clear();

            // Clear active catalog; will have to reload it for an on-demand scan
            ActiveCatalog.Close();

            Logger.Log("Mod has shutdown.", duplicateToGameLog: true);
        }
    }
}
