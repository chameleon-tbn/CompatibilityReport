using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;


namespace CompatibilityReport.Reporter
{
    internal static class Report
    {
        // List and dictionary with SteamIDs and Names of all subscribed mods, used for sorted reporting
        internal static List<ulong> AllSubscriptionSteamIDs { get; private set; }

        internal static Dictionary<string, List<ulong>> AllSubscriptionNamesAndIDs { get; private set; }

        // Dictionary of all compatibilities for subscribed mods
        internal static Dictionary<ulong, List<Compatibility>> SubscribedCompatibilities { get; private set; }

        // Keep track of the number of reviewed mods for logging and reporting
        internal static uint TotalReviewedSubscriptions { get; private set; }

        
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
            GetSubscriptions(ActiveCatalog);

            Logger.Log($"Reviewed { TotalReviewedSubscriptions } of your { AllSubscriptionSteamIDs.Count } mods.");

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

            // Free some memory
            AllSubscriptionSteamIDs = null;

            AllSubscriptionNamesAndIDs = null;

            SubscribedCompatibilities = null;

            Catalog.CloseActive();

            Logger.Log("Mod has shutdown.", duplicateToGameLog: true);
        }


        // Get all subscribed and local mods and merge the found info into the catalog. Local mods are temporarily added to the catalog in memory.
        private static void GetSubscriptions(Catalog ActiveCatalog)
        {
            // Get a list of all subscribed, local and builtin mods, including camera scripts
            List<PluginManager.PluginInfo> plugins = new List<PluginManager.PluginInfo>();

            PluginManager manager = Singleton<PluginManager>.instance;

            plugins.AddRange(manager.GetPluginsInfo());

            plugins.AddRange(manager.GetCameraPluginInfos());

            Logger.Log($"Game reports { plugins.Count } mods.");

            // Keep a list of the Steam IDs and names of all found mods, and keep track of the number of reviewed mods
            AllSubscriptionSteamIDs = new List<ulong>();

            AllSubscriptionNamesAndIDs = new Dictionary<string, List<ulong>>();

            SubscribedCompatibilities = new Dictionary<ulong, List<Compatibility>>();

            TotalReviewedSubscriptions = 0;

            // Fake Steam ID for local mods
            ulong nextLocalModID = ModSettings.lowestLocalModID;

            foreach (PluginManager.PluginInfo plugin in plugins)
            {
                Mod subscribedMod;

                bool foundInCatalog;

                if (plugin.publishedFileID != PublishedFileId.invalid)
                {
                    // Steam Workshop mod
                    ulong steamID = plugin.publishedFileID.AsUInt64;

                    foundInCatalog = ActiveCatalog.ModDictionary.ContainsKey(steamID);

                    subscribedMod = ActiveCatalog.GetOrAddMod(steamID);
                }
                else if (plugin.isBuiltin)
                {
                    // Builtin mod
                    string modName = Toolkit.GetPluginName(plugin);

                    if (!plugin.isEnabled)
                    {
                        Logger.Log($"Skipped disabled builtin mod: { modName }");

                        continue;
                    }

                    if (ModSettings.BuiltinMods.ContainsKey(modName))
                    {
                        ulong fakeSteamID = ModSettings.BuiltinMods[modName];

                        foundInCatalog = ActiveCatalog.ModDictionary.ContainsKey(fakeSteamID);

                        subscribedMod = ActiveCatalog.GetOrAddMod(fakeSteamID);
                    }
                    else
                    {
                        Logger.Log($"Skipped an unknown builtin mod: { modName }. { ModSettings.pleaseReportText }", Logger.error);

                        continue;
                    }
                }
                else
                {
                    // Local mod
                    if (nextLocalModID > ModSettings.highestLocalModID)
                    {
                        Logger.Log($"Skipped a local mod because we ran out of fake IDs: { Toolkit.GetPluginName(plugin) }. { ModSettings.pleaseReportText }",
                            Logger.error);

                        continue;
                    }

                    // Add the mod to the catalog. Matching local mods to catalog mods is a future idea not accounted for here.
                    subscribedMod = ActiveCatalog.GetOrAddMod(nextLocalModID);

                    nextLocalModID++;

                    foundInCatalog = false;
                }

                // Update the name for local mods and Steam mods that weren't found in the catalog
                if (string.IsNullOrEmpty(subscribedMod.Name))
                {
                    subscribedMod.Update(name: Toolkit.GetPluginName(plugin));
                }

                Logger.Log($"Mod found{ (foundInCatalog ? "" : " in game but not in the catalog") }: { subscribedMod.ToString() }");

                // Update the catalog mod with specific subscription info   [Todo 0.4] How reliable is downloadTime? Is ToLocalTime needed? Check how Loading Order Mod does this
                subscribedMod.UpdateSubscription(isDisabled: !plugin.isEnabled, plugin.isCameraScript, 
                    downloadedTime: PackageEntry.GetLocalModTimeUpdated(plugin.modPath).ToLocalTime());

                if (subscribedMod.ReviewDate != default)
                {
                    TotalReviewedSubscriptions++;
                }

                // Add the Steam ID and name to the list and dictionary used for reporting
                AllSubscriptionSteamIDs.Add(subscribedMod.SteamID);

                if (!AllSubscriptionNamesAndIDs.ContainsKey(subscribedMod.Name))
                {
                    // Name not found yet, add the name and Steam ID
                    AllSubscriptionNamesAndIDs.Add(subscribedMod.Name, new List<ulong> { subscribedMod.SteamID });
                }
                else
                {
                    // Identical name found earlier for another mod; add the Steam ID to the list of Steam IDs for this name
                    AllSubscriptionNamesAndIDs[subscribedMod.Name].Add(subscribedMod.SteamID);
                }

                // Add an empty entry to the compatibilities dictionary
                SubscribedCompatibilities.Add(subscribedMod.SteamID, new List<Compatibility>());
            }

            // Fill the dictionary with all compatibilities where both Steam IDs are subscribed
            foreach(Compatibility catalogCompatibility in ActiveCatalog.Compatibilities)
            {
                if (AllSubscriptionSteamIDs.Contains(catalogCompatibility.FirstModID) && AllSubscriptionSteamIDs.Contains(catalogCompatibility.SecondModID))
                {
                    SubscribedCompatibilities[catalogCompatibility.FirstModID].Add(catalogCompatibility);

                    SubscribedCompatibilities[catalogCompatibility.SecondModID].Add(catalogCompatibility);
                }
            }
        }
    }
}