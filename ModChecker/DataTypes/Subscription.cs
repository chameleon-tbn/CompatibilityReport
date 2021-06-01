using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ModChecker.Util;


namespace ModChecker.DataTypes
{
    internal class Subscription
    {
        // The Steam ID, name and author
        internal ulong SteamID { get; private set; }
        internal string Name { get; private set; }
        internal string AuthorName { get; private set; }

        // Date the mod was last updated
        internal DateTime Updated { get; private set; }

        // Date the files on disk were downloaded/updated
        internal DateTime Downloaded { get; private set; }

        // URL for an archived Steam Workshop page; only for removed mods
        internal string ArchiveURL { get; private set; }

        // Source URL
        internal string SourceURL { get; private set; }

        // Gameversion compatibility
        internal Version GameVersionCompatible { get; private set; } = GameVersion.Unknown;

        // Required DLC and mods
        internal List<Enums.DLC> RequiredDLC { get; private set; } = new List<Enums.DLC>();
        internal List<ulong> RequiredMods { get; private set; } = new List<ulong>();

        // Mods that need this one; only for pure dependency mods with no functionality of their own
        internal List<ulong> NeededFor { get; private set; } = new List<ulong>();

        // Successor(s); only for mods with issues
        internal List<ulong> SucceededBy { get; private set; } = new List<ulong>();

        // Alternative mods; only for mods with issues and no successor
        internal List<ulong> Alternatives { get; private set; } = new List<ulong>();

        // Status indicators
        internal bool IsEnabled { get; private set; }
        internal bool IsLocal { get; private set; }
        internal bool IsBuiltin { get; private set; }
        internal bool IsCameraScript { get; private set; }
        internal bool IsReviewed { get; private set; }
        internal bool AuthorIsRetired { get; private set; }

        internal List<Enums.ModStatus> Statuses { get; private set; }

        // Generic note
        internal string Note { get; private set; }

        // Compatibilities
        internal Dictionary<ulong, List<Enums.CompatibilityStatus>> Compatibilities { get; private set; }

        // Notes about mod compatibility
        internal Dictionary<ulong, string> ModNotes { get; private set; }

        
        // Dictionary for all subscribed mods, keyed by Steam ID
        internal static Dictionary<ulong, Subscription> AllSubscriptions { get; private set; } = null;

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

        // fake Steam IDs to assign to local mods and unknown builtin mods
        private static ulong localModID = ModSettings.lowestLocalModID;
        private static ulong unknownBuiltinModID = ModSettings.lowestUnknownBuiltinModID;


        // Default constructor
        internal Subscription()
        {
            // Nothing to do here
        }


        // Constructor with plugin parameter; get all information from plugin and catalog
        internal Subscription(PluginManager.PluginInfo plugin)
        {
            // Make sure we got a real plugin
            if (plugin == null)
            {
                Logger.Log("Found a 'null' plugin in the subscriptions list.", Logger.error);

                return;
            }

            // Get information from plugin
            Name = Tools.GetPluginName(plugin);
            IsEnabled = plugin.isEnabled;
            IsLocal = plugin.publishedFileID == PublishedFileId.invalid;
            IsBuiltin = plugin.isBuiltin;
            IsCameraScript = plugin.isCameraScript;

            // [Todo 0.5] How reliable is this?
            // Get the time this mod was downloaded/updated
            Downloaded = PackageEntry.GetLocalModTimeUpdated(plugin.modPath).ToLocalTime();

            // Get the Steam ID or assign a fake ID
            if (!IsLocal)
            {
                // Steam Workshop mod
                SteamID = plugin.publishedFileID.AsUInt64;

                // Check for overlap between fake and real Steam IDs
                if (SteamID <= ModSettings.highestFakeID)
                {
                    Logger.Log($"Steam ID { SteamID } is lower than the internal IDs used for local mods. This could cause issues. { ModSettings.pleaseReportText }",
                        Logger.error);
                }
            }
            else if (IsBuiltin)
            {
                // Builtin mod
                AuthorName = "Colossal Order";

                if (ModSettings.BuiltinMods.ContainsKey(Name))
                {
                    // Known builtin mod; these always get the same fake Steam ID from the BuiltinMods dictionary, so they can be used in mod compatibility
                    SteamID = ModSettings.BuiltinMods[Name];                    
                }
                else
                {
                    // Unknown builtin mod. This is either a mistake or BuiltinMods should be updated. Assign fake Steam ID, or 0 if we ran out of fake IDs.
                    SteamID = (unknownBuiltinModID <= ModSettings.highestUnknownBuiltinModID) ? unknownBuiltinModID : 0;

                    // Increase the fake Steam ID for the next mod
                    unknownBuiltinModID++;

                    Logger.Log($"Unknown builtin mod found: \"{ Name }\". This is probably a mistake. { ModSettings.pleaseReportText }", Logger.error);
                }
            }
            else
            {
                // Local mod. Assign fake Steam ID, or 0 if we ran out of fake IDs.
                SteamID = (localModID <= ModSettings.highestLocalModID) ? localModID : 0;

                // Increase the fake Steam ID for the next mod
                localModID++;
            }

            if (IsBuiltin && !IsEnabled)
            {
                // Exit on disabled builtin mods; they will not be included in the report and should not be counted in TotalReviewed below                
                Logger.Log($"Skipped builtin mod that is not enabled: { this.ToString() }.");

                return;
            }
                
            // Exit here if the mod is not in the catalog
            if (!Catalog.Active.ModDictionary.ContainsKey(SteamID))
            {
                // Mod not found in catalog; set to not reviewed
                IsReviewed = false;

                // Log that we didn't find this one in the catalog, unless it's a local (non-builtin) mod
                if (!IsLocal || IsBuiltin)
                {
                    Logger.Log($"Mod found in game but not in the catalog: { this.ToString() }.");
                }
                else
                {
                    Logger.Log($"Mod found: { this.ToString() }");
                }

                // Nothing more to do; exit
                return;
            }
            else
            {
                Logger.Log($"Mod found: { this.ToString() }");
            }

            // Get the mod from the catalog
            Mod catalogMod = Catalog.Active.ModDictionary[SteamID];

            // Check if the mod was (manually) reviewed and increase the number if so
            if (catalogMod.ReviewUpdated != DateTime.MinValue)
            {
                IsReviewed = true;

                TotalReviewed++;
            }

            // [Todo 0.4] Only for debugging, can be removed later
            // Check for mod rename
            if (Name != catalogMod.Name)
            {
                Logger.Log($"Mod { SteamID } was renamed from \"{ catalogMod.Name }\" to \"{ Name }\".", Logger.debug);
            }

            // Get information from the catalog
            Updated = catalogMod.Updated;
            ArchiveURL = catalogMod.ArchiveURL;
            SourceURL = catalogMod.SourceURL;
            GameVersionCompatible = Tools.ConvertToGameVersion(catalogMod.CompatibleGameVersionString);

            RequiredDLC = catalogMod.RequiredDLC;
            RequiredMods = catalogMod.RequiredMods;
            NeededFor = catalogMod.NeededFor;
            SucceededBy = catalogMod.SucceededBy;
            Alternatives = catalogMod.Alternatives;

            Statuses = catalogMod.Statuses;
            Note = catalogMod.Note;

            // Get the author name and retirement status; only for Steam Workshop mods
            if (!string.IsNullOrEmpty(catalogMod.AuthorID) && !IsLocal)
            {
                // Find the author in the active catalog
                if (Catalog.Active.AuthorDictionary.ContainsKey(catalogMod.AuthorID))
                {
                    // Get the author info from the catalog
                    AuthorName = Catalog.Active.AuthorDictionary[catalogMod.AuthorID].Name;

                    AuthorIsRetired = Catalog.Active.AuthorDictionary[catalogMod.AuthorID].Retired;
                }
                else
                {
                    // Can't find the author name, so use the author ID
                    AuthorName = catalogMod.AuthorID;

                    Logger.Log($"Author not found in catalog: { AuthorName }", Logger.debug);
                }
            }

            // Prepare to get compatibilities, including corresponding notes
            Compatibilities = new Dictionary<ulong, List<Enums.CompatibilityStatus>>();
            ModNotes = new Dictionary<ulong, string>();

            // Get all compatibilities (including notes) where this mod is either the first or the second ID
            AddCompatibility(Catalog.Active.ModCompatibilities.FindAll(c => c.SteamID1 == SteamID), firstID: true);
            AddCompatibility(Catalog.Active.ModCompatibilities.FindAll(c => c.SteamID2 == SteamID), firstID: false);

            // Get all mod groups that this mod is a member of
            List<ModGroup> modGroups = Catalog.Active.ModGroups.FindAll(g => g.SteamIDs.Contains(SteamID));

            if (modGroups != null)
            {
                // go through all found mod groups
                foreach (ModGroup group in modGroups)
                {
                    // Get all compatibilities (including notes) where the mod group is either the first or the second ID
                    AddCompatibility(Catalog.Active.ModCompatibilities.FindAll(c => c.SteamID1 == group.GroupID), firstID: true);
                    AddCompatibility(Catalog.Active.ModCompatibilities.FindAll(c => c.SteamID2 == group.GroupID), firstID: false);
                }
            }
        }


        // Add compatibilities to the list of compatibilities with the correct statuses; also add the corresponding note
        private void AddCompatibility(List<Compatibility> compatibilities,
                                      bool firstID)
        {
            if (compatibilities == null)
            {
                return;
            }

            foreach (Compatibility compatibility in compatibilities)
            {
                if (compatibility.Statuses.Contains(Enums.CompatibilityStatus.Unknown))
                {
                    // Unknown status indicates an empty status list; ignore this and continue with next compatibility
                    continue;
                }

                // firstID indicates which one is the subscription, but we want the other mods ID in the dictionaries
                if (firstID)
                {
                    // Add the compatibility status to the list, with the Steam ID for the other mod or group
                    Compatibilities.Add(compatibility.SteamID2, compatibility.Statuses);

                    // Add the mod note for this compatibility to the list
                    ModNotes.Add(compatibility.SteamID2, compatibility.NoteAboutMod2);
                }
                else
                {
                    // Gather the statuses in a new list so we can flip some without altering the catalog
                    List<Enums.CompatibilityStatus> statuses = new List<Enums.CompatibilityStatus>();

                    // Add each status, flipping some
                    foreach (Enums.CompatibilityStatus status in compatibility.Statuses)
                    {
                        switch (status)
                        {
                            case Enums.CompatibilityStatus.NewerVersionOfTheSame:
                                statuses.Add(Enums.CompatibilityStatus.OlderVersionOfTheSame);
                                break;

                            case Enums.CompatibilityStatus.OlderVersionOfTheSame:
                                statuses.Add(Enums.CompatibilityStatus.NewerVersionOfTheSame);
                                break;

                            case Enums.CompatibilityStatus.FunctionalityCoveredByThis:
                                statuses.Add(Enums.CompatibilityStatus.FunctionalityCoveredByOther);
                                break;

                            case Enums.CompatibilityStatus.FunctionalityCoveredByOther:
                                statuses.Add(Enums.CompatibilityStatus.FunctionalityCoveredByThis);
                                break;

                            case Enums.CompatibilityStatus.RequiresSpecificConfigForThis:
                                statuses.Add(Enums.CompatibilityStatus.RequiresSpecificConfigForOther);
                                break;

                            case Enums.CompatibilityStatus.RequiresSpecificConfigForOther:
                                statuses.Add(Enums.CompatibilityStatus.RequiresSpecificConfigForThis);
                                break;

                            default:
                                // Add statuses we don't need to flip
                                statuses.Add(status);
                                break;
                        }

                        // Add the compatibility status to the list, with the Steam ID for the other mod or group
                        Compatibilities.Add(compatibility.SteamID1, statuses);

                        // Add the mod note for this compatibility to the list
                        ModNotes.Add(compatibility.SteamID1, compatibility.NoteAboutMod1);
                    }
                }
            }
        }


        // Return a max sized, formatted string with the Steam ID and name
        internal string ToString(bool nameFirst = false,
                                 bool showFakeID = true,
                                 bool showDisabled = false)
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

            int maxNameLength = ModSettings.maxReportWidth - 1 - id.Length - disabledPrefix.Length;

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


        // [Todo 0.2] Move to its own class
        // Gather all subscribed and local mods, except disabled builtin mods
        internal static void GetAll()
        {
            // Don't do this again if already done
            if (AllSubscriptions != null)
            {
                Logger.Log("Subscription.GetAll called more than once.", Logger.warning);

                return;
            }

            // Initiate the dictionary and lists and reset the counters
            AllSubscriptions = new Dictionary<ulong, Subscription>();
            AllNamesAndIDs = new Dictionary<string, List<ulong>>();
            AllSteamIDs = new List<ulong>();
            AllNames = new List<string>();

            TotalBuiltin = 0;
            TotalLocal = 0;
            TotalReviewed = 0;
            
            // Get all subscribed and local mods
            List<PluginManager.PluginInfo> plugins = new List<PluginManager.PluginInfo>();

            PluginManager manager = Singleton<PluginManager>.instance;

            plugins.AddRange(manager.GetPluginsInfo());       // normal mods
            plugins.AddRange(manager.GetCameraPluginInfos()); // camera scripts

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
                }
            }

            Logger.Log($"{ AllSubscriptions.Count } mods ready for review, including { TotalBuiltin } builtin and { TotalLocal } local mods, " + 
                "but not including builtin mods that are disabled.");

            // Sort the lists
            AllSteamIDs.Sort();
            AllNames.Sort();
        }


        // Clear all subscription info from memory
        internal static void CloseAll()
        {
            // Nullify the dictionaries and lists
            AllSubscriptions = null;
            AllNamesAndIDs = null;
            AllSteamIDs = null;
            AllNames = null;

            // Reset the number of builtin, local and reviewed mods
            TotalReviewed = 0;
            TotalBuiltin = 0;
            TotalLocal = 0;

            // Reset the fake mod IDs
            localModID = ModSettings.lowestLocalModID;
            unknownBuiltinModID = ModSettings.lowestUnknownBuiltinModID;

            Logger.Log("Subscription dictionary closed.");
        }
    }
}
