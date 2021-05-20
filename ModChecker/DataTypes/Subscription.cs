using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using static ColossalFramework.Plugins.PluginManager;
using ModChecker.Util;
using static ModChecker.Util.ModSettings;


namespace ModChecker.DataTypes
{
    internal class Subscription
    {
        // The name, Steam ID and author
        internal ulong SteamID { get; private set; }
        internal string Name { get; private set; }
        internal string AuthorName { get; private set; }

        // Generic note
        internal string Note { get; private set; }

        // Status indicators
        internal bool IsEnabled { get; private set; }
        internal bool IsLocal { get; private set; }
        internal bool IsBuiltin { get; private set; }
        internal bool IsCameraScript { get; private set; }
        internal bool IsReviewed { get; private set; }
        internal bool IsRemoved { get; private set; }
        internal bool AuthorIsRetired { get; private set; }
        internal List<Enums.ModStatus> Statuses { get; private set; }

        // Source URL and archive URL
        internal string SourceURL { get; private set; }
        internal string ArchiveURL { get; private set; }                    // Only for removed mods; archive of the Steam Workshop page

        // Requirements, successors, alternatives and recommendations
        internal List<Enums.DLC> RequiredDLC { get; private set; }
        internal List<ulong> RequiredMods { get; private set; }
        internal List<ulong> NeededFor { get; private set; }                // Mods that need this one; only for pure dependency mods with no functionality of their own
        internal List<ulong> SucceededBy { get; private set; }              // Only for mods with issues
        internal List<ulong> Alternatives { get; private set; }             // Only for mods with issues and no successor
        internal List<ulong> Recommendations { get; private set; }

        // Compatibilities
        internal Dictionary<ulong, List<Enums.CompatibilityStatus>> Compatibilities { get; private set; }
        internal Dictionary<ulong, string> ModNotes { get; private set; }   // Notes about mod compatibility

        // Gameversion compatibility and last update
        internal Version GameVersionCompatible { get; private set; } = GameVersion.Unknown;
        internal DateTime? Updated { get; private set; }

        // The date/time the files on disk were downloaded/updated
        internal DateTime Downloaded { get; private set; }


        // Dictionary for all subscribed mods, keyed by Steam ID
        internal static Dictionary<ulong, Subscription> AllSubscriptions { get; private set; } = null;

        // A dictionary and two lists used for sorted reporting
        private static Dictionary<string, ulong> AllNamesAndSteamIDs;
        internal static List<ulong> AllSteamIDs { get; private set; }
        internal static List<string> AllNames { get; private set; }

        // Keep track of the number of reviewed mods
        internal static uint TotalSubscriptionsReviewed { get; private set; }

        // Keep track of local and enabled builtin mods for logging
        private static uint TotalBuiltin;
        private static uint TotalLocal;

        // fake Steam IDs to assign to local and unknown builtin mods
        private static ulong localModID = lowestLocalModID;
        private static ulong unknownBuiltinModID = lowestUnknownBuiltinModID;


        // Default constructor
        internal Subscription()
        {
            // Nothing to do here
        }


        // Constructor with plugin parameter; get all information from plugin and catalog
        internal Subscription(PluginInfo plugin)
        {
            // Just make sure we got a real plugin
            if (plugin == null)
            {
                Logger.Log("Found a 'null' plugin in the subscriptions list. Skipping.", Logger.error);

                return;
            }

            // Get information from plugin
            Name = Tools.GetPluginName(plugin);
            IsEnabled = plugin.isEnabled;
            IsLocal = plugin.publishedFileID == PublishedFileId.invalid;
            IsBuiltin = plugin.isBuiltin;
            IsCameraScript = plugin.isCameraScript;

            // Get the time this mod was downloaded/updated
            Downloaded = PackageEntry.GetLocalModTimeUpdated(plugin.modPath).ToLocalTime();         // Unfinished: how reliable is this?

            // Get the Steam ID or assign a fake ID
            if (!IsLocal)
            {
                // Steam Workshop mod
                SteamID = plugin.publishedFileID.AsUInt64;

                // Check for overlap between fake and real Steam IDs
                if (SteamID <= HighestFakeID)
                {
                    Logger.Log($"Steam ID { SteamID } is lower than the internal IDs used for local mods. This could cause issues. { PleaseReportText }",
                        Logger.error);
                }
            }
            else if (IsBuiltin)
            {
                // Builtin mod
                AuthorName = "Colossal Order";

                if (BuiltinMods.ContainsKey(Name))
                {
                    // Known builtin mod; these always get the same fake Steam ID from the BuiltinMods dictionary, so they can be used in mod compatibility
                    SteamID = BuiltinMods[Name];                    
                }
                else
                {
                    // Unknown builtin mod. This is either a mistake or BuiltinMods should be updated. Assign fake Steam ID, or 0 if we ran out of fake IDs.
                    SteamID = (unknownBuiltinModID <= highestUnknownBuiltinModID) ? unknownBuiltinModID : 0;

                    // Increase the fake Steam ID for the next mod
                    unknownBuiltinModID++;

                    Logger.Log($"Unknown builtin mod found: \"{ Name }\". This is probably a mistake. { PleaseReportText }", Logger.error);
                }
            }
            else
            {
                // Local mod. Assign fake Steam ID, or 0 if we ran out of fake IDs.
                SteamID = (localModID <= highestLocalModID) ? localModID : 0;

                // Increase the fake Steam ID for the next mod
                localModID++;
            }

            if (IsBuiltin && !IsEnabled)
            {
                // Exit on disabled builtin mods; they will not be included in the report and should not be counted in TotalReviewed below                
                Logger.Log($"Skipped builtin mod that is not enabled: { this.ToString() }.");

                return;
            }
                
            // Don't log disabled builtin mods, because they will get a 'skipped' log message at GetAll()
            Logger.Log($"Mod found: { this.ToString() }");
            
            // Find the mod in the active catalog
            if (!Catalog.Active.ModDictionary.ContainsKey(SteamID))
            {
                // Mod not found in catalog; set to not reviewed
                IsReviewed = false;

                // Debug log, unless it's a local non-builtin mod
                if (!IsLocal || IsBuiltin)
                {
                    Logger.Log($"Mod not found in catalog: { this.ToString() }.", Logger.debug);
                }                

                // Nothing more to do; exit
                return;
            }

            // Mod is in the catalog
            Mod mod = Catalog.Active.ModDictionary[SteamID];

            // Check if the mod was reviewed and increase the number if so
            IsReviewed = mod.ReviewUpdated != null;

            if (IsReviewed)
            {
                TotalSubscriptionsReviewed++;
            }

            // Check for mod rename
            if (Name != mod.Name)
            {
                Logger.Log($"Mod { SteamID } was renamed from \"{ mod.Name }\" to \"{ Name }\".", Logger.debug);
            }

            // Get information from the catalog
            Note = mod.Note;
            IsRemoved = mod.IsRemoved;
            SourceURL = mod.SourceURL;
            ArchiveURL = mod.ArchiveURL;
            GameVersionCompatible = Tools.ConvertToGameVersion(mod.CompatibleGameVersionString);
            Updated = mod.Updated;
            Statuses = mod.Statuses;

            RequiredDLC = mod.RequiredDLC;
            RequiredMods = mod.RequiredMods;
            NeededFor = mod.NeededFor;
            SucceededBy = mod.SucceededBy;
            Alternatives = mod.Alternatives;
            Recommendations = mod.Recommendations;

            // Get the author name for Steam Workshop mods
            if (!string.IsNullOrEmpty(mod.AuthorTag) && !IsLocal)
            {
                // Find the author in the active catalog for Steam Workshop mods
                if (Catalog.Active.AuthorDictionary.ContainsKey(mod.AuthorTag))
                {
                    // Get the author info from the catalog
                    AuthorName = Catalog.Active.AuthorDictionary[mod.AuthorTag].Name;

                    AuthorIsRetired = Catalog.Active.AuthorDictionary[mod.AuthorTag].Retired;
                }
                else
                {
                    // Can't find the author name, so use the author tag
                    AuthorName = mod.AuthorTag;

                    Logger.Log($"Author not found in catalog: { AuthorName }", Logger.debug);
                }
            }

            // Get all compatibilities for this mod
            Compatibilities = new Dictionary<ulong, List<Enums.CompatibilityStatus>>();
            ModNotes = new Dictionary<ulong, string>();

            AddCompatibility(Catalog.Active.ModCompatibilities.FindAll(c => c.SteamID1 == SteamID), firstID: true);
            AddCompatibility(Catalog.Active.ModCompatibilities.FindAll(c => c.SteamID2 == SteamID), firstID: false);

            // Get all compatibilities for all mod groups that this mod is a member of
            List<ModGroup> modGroups = Catalog.Active.ModGroups.FindAll(g => g.SteamIDs.Contains(SteamID));

            if (modGroups != null)
            {
                foreach (ModGroup group in modGroups)
                {
                    AddCompatibility(Catalog.Active.ModCompatibilities.FindAll(c => c.SteamID1 == group.GroupID), firstID: true);
                    AddCompatibility(Catalog.Active.ModCompatibilities.FindAll(c => c.SteamID2 == group.GroupID), firstID: false);
                }
            }
        }


        // Add compatibilities to the list of compatibilities with the correct statuses; also add the corresponding note
        private void AddCompatibility(List<ModCompatibility> compatibilities, bool firstID)
        {
            if (compatibilities == null)
            {
                return;
            }

            foreach (ModCompatibility compatibility in compatibilities)
            {
                // Statuses cannot be null or empty (will contain Unknown instead), so no null-check needed
                List<Enums.CompatibilityStatus> statuses = compatibility.Statuses;

                if (statuses.Contains(Enums.CompatibilityStatus.Unknown))
                {
                    // Unknown status indicates an empty status list; exit
                    return;
                }

                // Revert some of the statuses if the subscription was the second ID in the compatibility object
                if (!firstID)
                {
                    if (statuses.Contains(Enums.CompatibilityStatus.NewerVersionOfTheSameMod))
                    {
                        statuses.Remove(Enums.CompatibilityStatus.NewerVersionOfTheSameMod);
                        statuses.Add(Enums.CompatibilityStatus.OlderVersionOfTheSameMod);
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.OlderVersionOfTheSameMod))
                    {
                        statuses.Remove(Enums.CompatibilityStatus.OlderVersionOfTheSameMod);
                        statuses.Add(Enums.CompatibilityStatus.NewerVersionOfTheSameMod);
                    }

                    if (statuses.Contains(Enums.CompatibilityStatus.FunctionalityCoveredByThisMod))
                    {
                        statuses.Remove(Enums.CompatibilityStatus.FunctionalityCoveredByThisMod);
                        statuses.Add(Enums.CompatibilityStatus.FunctionalityCoveredByOtherMod);
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.FunctionalityCoveredByOtherMod))
                    {
                        statuses.Remove(Enums.CompatibilityStatus.FunctionalityCoveredByOtherMod);
                        statuses.Add(Enums.CompatibilityStatus.FunctionalityCoveredByThisMod);
                    }

                    if (statuses.Contains(Enums.CompatibilityStatus.RequiresSpecificConfigForThisMod))
                    {
                        statuses.Remove(Enums.CompatibilityStatus.RequiresSpecificConfigForThisMod);
                        statuses.Add(Enums.CompatibilityStatus.RequiresSpecificConfigForOtherMod);
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.RequiresSpecificConfigForOtherMod))
                    {
                        statuses.Remove(Enums.CompatibilityStatus.RequiresSpecificConfigForOtherMod);
                        statuses.Add(Enums.CompatibilityStatus.RequiresSpecificConfigForThisMod);
                    }
                }

                // firstID indicates which one is the subscription, but we want the other mods ID in the dictionaries
                if (firstID)
                {
                    // Add the compatibility status to the list, with the Steam ID for the other mod or group
                    Compatibilities.Add(compatibility.SteamID2, statuses);

                    // Add the mod note for this compatibility to the list
                    ModNotes.Add(compatibility.SteamID2, compatibility.NoteMod2);
                }
                else
                {
                    // Add the compatibility status to the list, with the Steam ID for the other mod or group
                    Compatibilities.Add(compatibility.SteamID1, statuses);

                    // Add the mod note for this compatibility to the list
                    ModNotes.Add(compatibility.SteamID1, compatibility.NoteMod1);
                }
            }
        }


        // Return a max sized, formatted string with the Steam ID and name
        internal string ToString(bool nameFirst = false, bool showFakeID = true, bool showDisabled = false)
        {
            string id;

            if (IsBuiltin)
            {
                id = "[builtin mod" + (showFakeID ? " " + SteamID.ToString() : "") + "]";
            }
            else if (IsLocal)
            {
                id = "[local mod" + (showFakeID ? " " + SteamID.ToString() : "") + "]";
            }
            else
            {
                id = $"[Steam ID { SteamID, 10 }]";
            }

            string disabledPrefix = (showDisabled && !IsEnabled) ? "[Disabled] " : "";

            int maxNameLength = MaxReportWidth - 1 - id.Length - disabledPrefix.Length;

            string name = (Name.Length <= maxNameLength) ? Name : Name.Substring(0, maxNameLength - 3) + "...";

            if (nameFirst)
            {
                return disabledPrefix + name + " " + id;
            }
            else
            {
                return disabledPrefix + id + " " + name;
            }
        }


        // Gather all subscribed and local mods, except disabled builtin mods
        internal static void GetAll()
        {
            // Don't do this again if already done
            if (AllSubscriptions != null)
            {
                Logger.Log("Subscription.GetAll called more than once.", Logger.warning);

                return;
            }

            // Initiate the dictionary and reset the numbers for builtin, local and reviewed mods
            AllSubscriptions = new Dictionary<ulong, Subscription>();

            AllNamesAndSteamIDs = new Dictionary<string, ulong>();
            AllSteamIDs = new List<ulong>();
            AllNames = new List<string>();

            TotalBuiltin = 0;
            TotalLocal = 0;
            TotalSubscriptionsReviewed = 0;
            
            // Get all subscribed and local mods in a list
            List<PluginInfo> plugins = new List<PluginInfo>();

            PluginManager manager = Singleton<PluginManager>.instance;

            plugins.AddRange(manager.GetPluginsInfo());       // normal mods
            plugins.AddRange(manager.GetCameraPluginInfos()); // camera scripts

            Logger.Log($"Game reports { plugins.Count } mods.");

            // Add subscriptions to the dictionary; at least one plugin should be found (this mod), so no need to check for null or Count == 0
            foreach (PluginInfo plugin in plugins)
            {
                try
                {
                    // Get the info for this subscription from plugin and catalog; also assigns correct fake Steam IDs to local and builtin mods
                    Subscription subscription = new Subscription(plugin);

                    if (subscription.SteamID > 0)
                    {
                        // Skip disabled builtin mods, since they shouldn't influence anything; disabled local mods are still added
                        if (subscription.IsBuiltin && !subscription.IsEnabled)
                        {
                            continue;       // To next plugin in foreach
                        }

                        // Add Steam Workshop mod to the dictionaries and lists
                        AllSubscriptions.Add(subscription.SteamID, subscription);

                        AllNamesAndSteamIDs.Add(subscription.Name, subscription.SteamID);
                        AllSteamIDs.Add(subscription.SteamID);
                        AllNames.Add(subscription.Name);

                        // Keep track of builtin and local mods added; builtin mods are also local, but should not be counted twice
                        if (subscription.IsBuiltin)
                        {
                            TotalBuiltin++;
                        }
                        else if (subscription.IsLocal)
                        {
                            TotalLocal++;
                        }
                    }
                    else
                    {
                        // Ran out of fake IDs for this mod. It can't be added.
                        string builtinOrLocal = (subscription.IsBuiltin) ? "builtin" : "local";

                        Logger.Log($"Ran out of internal IDs for { builtinOrLocal } mods. Some mods were not added to the subscription list. " +
                            PleaseReportText, Logger.error);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Can't add mod to subscription list: { Tools.GetPluginName(plugin) }.", Logger.error);

                    Logger.Exception(ex, gameLog: false);
                }
            }

            Logger.Log($"{ AllSubscriptions.Count } mods ready for review, including { TotalBuiltin } builtin and { TotalLocal } local mods, " + 
                "but not including builtin mods that are disabled.");

            // Sort the lists
            AllSteamIDs.Sort();
            AllNames.Sort();
        }


        // Remove the subscription dictionary from memory
        internal static void CloseAll()
        {
            if (AllSubscriptions == null)
            {
                Logger.Log("Asked to close the subscription dictionary which was empty already.", Logger.debug);
            }

            // Nullify the dictionaries and list
            AllSubscriptions = null;
            AllNamesAndSteamIDs = null;
            AllSteamIDs = null;
            AllNames = null;

            // Reset the number of builtin, local and reviewed mods
            TotalBuiltin = 0;
            TotalLocal = 0;
            TotalSubscriptionsReviewed = 0;

            // To avoid duplicates in unforeseen situations, don't reset the next fake IDs to use
            // localModID = lowestLocalModID;
            // unknownBuiltinModID = lowestUnknownBuiltinModID;

            Logger.Log("Subscription dictionary closed.");
        }


        // Returns the Steam ID for a given mod name; only works for subscriptions
        internal static ulong NameToSteamID(string name)
        {
            if (AllNamesAndSteamIDs.ContainsKey(name))
            {
                return AllNamesAndSteamIDs[name];
            }
            else
            {
                return 0;
            }
        }


        // Returns the mod name for a given Steam ID; only works for subscriptions
        internal static string SteamIDToName(ulong steamID)
        {
            if (AllNamesAndSteamIDs.ContainsValue(steamID))
            {
                foreach (string name in AllNamesAndSteamIDs.Keys)
                {
                    if (AllNamesAndSteamIDs[name] == steamID)
                    {
                        return name;
                    }
                }
            }

            return "";
        }
    }
}
