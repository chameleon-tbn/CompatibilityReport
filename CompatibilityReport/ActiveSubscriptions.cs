using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Plugins;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;


namespace CompatibilityReport
{
    internal static class ActiveSubscriptions
    {
        // Dictionary for all subscribed mods, keyed by Steam ID
        internal static Dictionary<ulong, Subscription> All { get; private set; } = null;

        // Dictionary for all mod names with their Steam IDs; names are not unique, so one name could refer to multiple Steam IDs; used for reporting sorted by name
        internal static Dictionary<string, List<ulong>> AllNamesAndIDs { get; private set; } = null;

        // Lists with all SteamIDs and Names for sorted reporting
        internal static List<ulong> AllSteamIDs { get; private set; }
        internal static List<string> AllNames { get; private set; }

        // Keep track of the number of local, builtin and reviewed mods for logging and reporting
        internal static uint TotalReviewed { get; private set; }

        // Keep track of local and enabled builtin mods for logging
        private static uint TotalBuiltin;

        private static uint TotalLocal;


        // Gather all subscribed and local mods, except disabled builtin mods
        internal static void Get()
        {
            // Don't do this again if already done
            if (All != null)
            {
                return;
            }

            // Initiate the dictionary and lists and reset the counters
            All = new Dictionary<ulong, Subscription>();
            AllNamesAndIDs = new Dictionary<string, List<ulong>>();
            AllSteamIDs = new List<ulong>();
            AllNames = new List<string>();

            TotalBuiltin = 0;
            TotalLocal = 0;
            TotalReviewed = 0;

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
                    All.Add(subscription.SteamID, subscription);

                    // Add Steam ID to the list
                    AllSteamIDs.Add(subscription.SteamID);

                    // Multiple mods can have the same name, but names should only appear once in the list and dictionary
                    if (AllNames.Contains(subscription.Name))
                    {
                        // Name found earlier; only add the Steam ID to the dictionary (more precise: to the list inside the dictionary)
                        AllNamesAndIDs[subscription.Name].Add(subscription.SteamID);

                        // Sort the list inside the dictionary
                        AllNamesAndIDs[subscription.Name].Sort();
                    }
                    else
                    {
                        // Name not found yet; add it to the list and dictionary
                        AllNames.Add(subscription.Name);

                        AllNamesAndIDs.Add(subscription.Name, new List<ulong> { subscription.SteamID });
                    }

                    // Keep track of builtin and local mods added; builtin mods are also local, but should not be counted twice
                    if (subscription.IsBuiltin)
                    {
                        TotalBuiltin++;
                    }
                    else if (subscription.IsLocal)
                    {
                        TotalLocal++;
                    }

                    // Keep track of the number of reviewed subscriptions
                    if (subscription.IsReviewed)
                    {
                        TotalReviewed++;
                    }
                }
            }

            Logger.Log($"{ All.Count } mods ready for review, including { TotalBuiltin } builtin and { TotalLocal } local mods.");

            // Sort the lists
            AllSteamIDs.Sort();
            AllNames.Sort();
        }


        // Clear all subscription info from memory
        internal static void Clear()
        {
            // Nullify the dictionaries and lists
            All = null;
            AllNamesAndIDs = null;
            AllSteamIDs = null;
            AllNames = null;

            // Reset the number of builtin, local and reviewed mods
            TotalReviewed = 0;
            TotalBuiltin = 0;
            TotalLocal = 0;

            Logger.Log("Subscription dictionary closed.");
        }
    }
}
