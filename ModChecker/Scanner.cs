using System.Collections.Generic;
using System.Diagnostics;
using ModChecker.DataTypes;
using ModChecker.Util;


namespace ModChecker
{
    internal static class Scanner
    {
        // Keep track of run status
        private static readonly List<string> HasRun = new List<string> { };


        // Init: basic checks and loading the catalog; 
        // Called early on game startup, before subscriptions can be seen
        internal static void Init()
        {
            // Don't run Init more than once
            if (HasRun.Contains("Init"))
            {
                Logger.Log("Scanner Init called more than once.", Logger.debug);

                return;
            }

            // Exit if Steam Workshop is not enabled or otherwise unavailable in game
            if (!Tools.SteamWorkshopAvailable)
            {
                Logger.Log("The game can't access the Steam Workshop, and thus has no subscriptions and has nothing to check. No report was generated.\n" + 
                    "This is expected behaviour if you used the '--noWorkshop' parameter.", Logger.warning, gameLog: true);

                HasRun.Add("Can't");

                return;
            }

            // Keep track of elapsed time
            Stopwatch timer = Stopwatch.StartNew();

            // Log mod and game version
            Logger.Log($"{ ModSettings.name } version { ModSettings.version }. Game version { GameVersion.Formatted(GameVersion.Current) }. ", gameLog: true);
            
            Logger.Log($"{ GameVersion.SpecialNote }", gameLog: true);
            
            // Initialize the catalog; exit if no valid catalog can be loaded
            if (!Catalog.InitActive())
            {
                Logger.Log("Can't load bundled catalog and can't download a new catalog. No report was generated.", Logger.error, gameLog: true);

                HasRun.Add("Can't");

                return;
            }

            // Check if game version equals the version this mod (more precise: the catalog) was made for
            if (GameVersion.Current != Catalog.Active.CompatibleGameVersion)
            {
                string olderNewer = (GameVersion.Current < Catalog.Active.CompatibleGameVersion) ? "an older" : "a newer";

                Logger.Log($"This mod was updated for game version { GameVersion.Formatted(Catalog.Active.CompatibleGameVersion) }. " +
                    $"You're using { olderNewer } version of the game. Results might not be accurate.", Logger.warning, gameLog: true);
            }

            // Log catalog details and how long the init took. Not sure how useful that number is, with a lot happening in the game at the same time.
            Logger.Log($"Using catalog version { Catalog.Active.StructureVersion }.{ Catalog.Active.Version:D4}, " + 
                $"created on { Catalog.Active.UpdateDate.ToLongDateString() }. Catalog contains { Catalog.Active.CountReviewed } reviewed mods and " +
                $"{ Catalog.Active.Count - Catalog.Active.CountReviewed } other mods with basic information.", gameLog: true);

            timer.Stop();

            Logger.Log($"Scanner initialized in { timer.ElapsedMilliseconds } ms. Now waiting for all mods to load.\n", gameLog: true);

            // Indicate that Init has run
            HasRun.Add("Init");
        }


        // Start a scan
        // Called late on game startup (just before main menu) and during map loading; a report will be created both times, overriding an older report
        internal static void Scan(string scene)
        {
            // Allow scanning multiple times in the same scene, to support on-demand scanning
            if (HasRun.Contains(scene))
            {
                Logger.Log($"Scanner called more than once in scene { scene }.", Logger.debug);

                // return;
            }

            // Only scan in allowed scenes
            if (!ModSettings.ScannerScenes.Contains(scene))
            {
                Logger.Log($"Scanner called in unallowed scene { scene }. Exiting.", Logger.debug);

                return;
            }

            // Only scan if Init has completed without issues
            if (!HasRun.Contains("Init") || HasRun.Contains("Can't"))
            {
                Logger.Log("Scanner called without succesfull Init earlier. Cannot create a report.", Logger.error);

                Scanner.Close();

                return;
            }

            // if we don't have an active catalog, initialize it again; exit if we can't
            if (Catalog.Active == null)
            {
                if (!Catalog.InitActive())
                {
                    Logger.Log("Scanner called without an active catalog, and can't load bundled catalog and can't download a new catalog. No report was generated.", 
                        Logger.error, gameLog: true);

                    return;
                }
            }

            // Start timer to keep track of elapsed time
            Stopwatch timer = Stopwatch.StartNew();

            string phase = (scene == "IntroScreen") ? "game startup, before main menu" : ((scene == "Game") ? "map loading." : $"scene { scene }");
            
            Logger.Log($"Scan started during { phase }.", gameLog: true);

            // Get all subscriptions, including all builtin and local mods, with info from game and catalog; always at least one mod (this), so no need for a null-check
            Subscription.GetAll();

            // Create the report(s)
            Reporter.Create();

            // Log number of mods, runtime and report location
            timer.Stop();

            Logger.Log($"Reviewed { Subscription.TotalReviewed } of your { Subscription.AllSubscriptions.Count } mods in { timer.ElapsedMilliseconds } ms.", 
                gameLog: true);

            // Indicate that we've done the scan in this scene
            HasRun.Add(scene);

            // Close and clean up
            Scanner.Close();
        }


        // Clean up
        internal static void Close()
        {
            if (!HasRun.Contains("Init"))
            {
                Logger.Log("Scanner asked to close without an Init done.", Logger.debug);
            }

            // Clear subscriptions
            Subscription.CloseAll();

            // Clear catalog; this means we need to init the catalog again on another scan in a different scene, or for an on-demand scan
            Catalog.CloseActive();

            Logger.Log("Mod has shutdown.", gameLog: true);
        }
    }
}
