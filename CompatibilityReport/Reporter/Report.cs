using System.Collections.Generic;
using ColossalFramework;
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
            GetSubscriptions();

            Logger.Log($"Reviewed { TotalReviewedSubscriptions } of your { AllSubscriptions.Count } mods ");

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
            AllSubscriptions = null;

            AllSubscriptionNamesAndIDs = null;

            AllSubscriptionSteamIDs = null;

            AllSubscriptionNames = null;

            TotalReviewedSubscriptions = 0;

            Catalog.CloseActive();

            Logger.Log("Mod has shutdown.", duplicateToGameLog: true);
        }






        // Dictionary for all subscribed mods, keyed by Steam ID
        internal static Dictionary<ulong, Subscription> AllSubscriptions { get; private set; } = null;

        // Dictionary for all mod names with their Steam IDs; names are not unique, so one name could refer to multiple Steam IDs; used for reporting sorted by name
        internal static Dictionary<string, List<ulong>> AllSubscriptionNamesAndIDs { get; private set; } = null;

        // Lists with all SteamIDs and Names for sorted reporting
        internal static List<ulong> AllSubscriptionSteamIDs { get; private set; }
        internal static List<string> AllSubscriptionNames { get; private set; }

        // Keep track of the number of local, builtin and reviewed mods for logging and reporting
        internal static uint TotalReviewedSubscriptions { get; private set; }


        // Gather all subscribed and local mods, except disabled builtin mods
        private static void GetSubscriptions()
        {
            // Don't do this again if already done
            if (AllSubscriptions != null)
            {
                return;
            }

            // Initiate the dictionary and lists and reset the counters
            AllSubscriptions = new Dictionary<ulong, Subscription>();
            AllSubscriptionNamesAndIDs = new Dictionary<string, List<ulong>>();
            AllSubscriptionSteamIDs = new List<ulong>();
            AllSubscriptionNames = new List<string>();

            // Keep track of local and enabled builtin mods for logging
            uint TotalBuiltinSubscriptions = 0;
            uint TotalLocalSubscriptions = 0;
            TotalReviewedSubscriptions = 0;

            // Get all subscribed and local mods
            List<PluginManager.PluginInfo> plugins = new List<PluginManager.PluginInfo>();

            PluginManager manager = Singleton<PluginManager>.instance;

            // Get all regular mods and cinematic camera scripts
            plugins.AddRange(manager.GetPluginsInfo());
            plugins.AddRange(manager.GetCameraPluginInfos());

            Logger.Log($"Game reports { plugins.Count } mods.");

            // Add subscriptions to the dictionary; at least one plugin should be found (this mod), so no null check needed
            foreach (PluginManager.PluginInfo plugin in plugins)
            {
                // Get the info for this subscription from plugin and catalog; also assigns correct fake Steam IDs to local and builtin mods
                Subscription subscription = new Subscription(plugin);

                if (subscription.SteamID == 0)
                {
                    // Ran out of fake IDs for this mod. It can't be added.
                    string builtinOrLocal = (subscription.IsBuiltin) ? "builtin" : "local";

                    Logger.Log($"Ran out of internal IDs for { builtinOrLocal } mods. Some mods were not added to the subscription list. " +
                        ModSettings.pleaseReportText, Logger.error);
                }
                else
                {
                    // Skip disabled builtin mods, since they shouldn't influence anything; disabled local mods are still added
                    if (subscription.IsBuiltin && !subscription.IsEnabled)
                    {
                        continue;       // To next plugin in foreach
                    }

                    // Add Steam Workshop mod to the dictionary
                    AllSubscriptions.Add(subscription.SteamID, subscription);

                    // Add Steam ID to the list
                    AllSubscriptionSteamIDs.Add(subscription.SteamID);

                    // Multiple mods can have the same name, but names should only appear once in the list and dictionary
                    if (AllSubscriptionNames.Contains(subscription.Name))
                    {
                        // Name found earlier; only add the Steam ID to the dictionary (more precise: to the list inside the dictionary)
                        AllSubscriptionNamesAndIDs[subscription.Name].Add(subscription.SteamID);

                        // Sort the list inside the dictionary
                        AllSubscriptionNamesAndIDs[subscription.Name].Sort();
                    }
                    else
                    {
                        // Name not found yet; add it to the list and dictionary
                        AllSubscriptionNames.Add(subscription.Name);

                        AllSubscriptionNamesAndIDs.Add(subscription.Name, new List<ulong> { subscription.SteamID });
                    }

                    // Keep track of builtin and local mods added; builtin mods are also local, but should not be counted twice
                    if (subscription.IsBuiltin)
                    {
                        TotalBuiltinSubscriptions++;
                    }
                    else if (subscription.IsLocal)
                    {
                        TotalLocalSubscriptions++;
                    }

                    // Keep track of the number of reviewed subscriptions
                    if (subscription.IsReviewed)
                    {
                        TotalReviewedSubscriptions++;
                    }
                }
            }

            Logger.Log($"{ AllSubscriptions.Count } mods ready for review, including { TotalBuiltinSubscriptions } builtin and { TotalLocalSubscriptions } local mods.");

            // Sort the lists
            AllSubscriptionSteamIDs.Sort();
            AllSubscriptionNames.Sort();
        }
    }
}