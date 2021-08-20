using System;
using System.Collections.Generic;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using CompatibilityReport.Util;


namespace CompatibilityReport.DataTypes
{
    internal class Subscription
    {
        // The Steam ID, name and author
        internal ulong SteamID { get; private set; }
        internal string Name { get; private set; }
        internal string AuthorName { get; private set; }

        // Date the mod was last updated
        internal DateTime Updated { get; private set; }

        // Date the files on disk were last downloaded
        internal DateTime Downloaded { get; private set; }

        // URL for an archived Steam Workshop page; only for mods no longer on the Steam Workshop
        internal string ArchiveURL { get; private set; }

        // Source URL
        internal string SourceURL { get; private set; }

        // Gameversion compatibility
        internal Version GameVersionCompatible { get; private set; } = GameVersion.Unknown;

        // Required DLC and mods
        internal List<Enums.DLC> RequiredDLC { get; private set; } = new List<Enums.DLC>();
        internal List<ulong> RequiredMods { get; private set; } = new List<ulong>();

        // Successor(s); only for mods with issues
        internal List<ulong> Successors { get; private set; } = new List<ulong>();

        // Alternative mods; only for mods with issues and no successor
        internal List<ulong> Alternatives { get; private set; } = new List<ulong>();

        // Mod stability
        internal Enums.ModStability Stability { get; private set; }
        internal string StabilityNote { get; private set; }

        // Status indicators
        internal bool IsEnabled { get; private set; }
        internal bool IsLocal { get; private set; }
        internal bool IsBuiltin { get; private set; }
        internal bool IsCameraScript { get; private set; }
        internal bool IsReviewed { get; private set; }
        internal bool AuthorIsRetired { get; private set; }

        internal List<Enums.ModStatus> Statuses { get; private set; } = new List<Enums.ModStatus>();

        // Generic note
        internal string GenericNote { get; private set; }

        // Compatibilities
        internal Dictionary<ulong, List<Enums.CompatibilityStatus>> Compatibilities { get; private set; } = new Dictionary<ulong, List<Enums.CompatibilityStatus>>();

        // Notes about mod compatibilities
        internal Dictionary<ulong, string> ModNotes { get; private set; } = new Dictionary<ulong, string>();


        // fake Steam IDs to assign to local mods and unknown builtin mods
        private static ulong localModID = ModSettings.lowestLocalModID;
        private static ulong unknownBuiltinModID = ModSettings.lowestUnknownBuiltinModID;


        // Default constructor
        internal Subscription()
        {
            // Nothing to do here
        }


        // Constructor with plugin parameter; get all information from plugin and catalog [Todo 0.4] This constructor is large, is that problematic?
        internal Subscription(PluginManager.PluginInfo plugin)
        {
            // Make sure we got a real plugin
            if (plugin == null)
            {
                Logger.Log("Found a 'null' plugin in the subscriptions list.", Logger.error);

                return;
            }

            // Get information from plugin
            Name = Toolkit.GetPluginName(plugin);
            IsEnabled = plugin.isEnabled;
            IsLocal = plugin.publishedFileID == PublishedFileId.invalid;
            IsBuiltin = plugin.isBuiltin;
            IsCameraScript = plugin.isCameraScript;

            // Get the time this mod was downloaded/updated; [Todo 0.4] How reliable is this? Is ToLocalTime needed? Check how Loading Order Mod does this
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

                    Logger.Log($"Unknown builtin mod found: \"{ Name }\". { ModSettings.pleaseReportText }", Logger.error);
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
                Logger.Log($"Skipped builtin mod that is not enabled: { this.ToString() }");

                return;
            }
            
            // Exit here if the mod is not in the catalog
            if (!ActiveCatalog.Instance.ModDictionary.ContainsKey(SteamID))
            {
                // Mod not found in catalog
                if (!IsLocal || IsBuiltin)
                {
                    // Log that we didn't find this one in the catalog; probably an unlisted or removed mod
                    Logger.Log($"Mod found in game but not in the catalog: { this.ToString() }. { ModSettings.pleaseReportText }", Logger.error);
                }
                else
                {
                    // Local mod, log the subscription name
                    Logger.Log($"Mod found: { this.ToString() }");
                }

                // Nothing more to do; exit
                return;
            }
            
            // Get the mod from the catalog
            Mod catalogMod = ActiveCatalog.Instance.ModDictionary[SteamID];

            // Change name from subscription, as seen in Content Manager and Options, to catalog, as seen in the Workshop. They often differ slightly, sometimes a lot.
            Name = catalogMod.Name;

            // Log the found mod
            Logger.Log($"Mod found: { catalogMod.ToString(cutOff: false) }");

            // Check if the mod was manually reviewed
            IsReviewed = catalogMod.ReviewDate != default;

            // Get information from the catalog
            Updated = catalogMod.Updated;
            ArchiveURL = catalogMod.ArchiveURL;
            SourceURL = catalogMod.SourceURL;
            GameVersionCompatible = Toolkit.ConvertToGameVersion(catalogMod.CompatibleGameVersionString);

            RequiredDLC = catalogMod.RequiredDLC;
            RequiredMods = catalogMod.RequiredMods;
            Successors = catalogMod.Successors;
            Alternatives = catalogMod.Alternatives;

            Stability = catalogMod.Stability;
            StabilityNote = catalogMod.StabilityNote;
            Statuses = catalogMod.Statuses;
            GenericNote = catalogMod.GenericNote;

            // Get the author name and retirement status for Steam Workshop mods
            if (!IsLocal)
            {
                Author catalogAuthor = null;

                // Find the author in the active catalog
                if (catalogMod.AuthorID != 0)
                {
                    catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(catalogMod.AuthorID) ?
                        ActiveCatalog.Instance.AuthorIDDictionary[catalogMod.AuthorID] : null;
                }
                else if (!string.IsNullOrEmpty(catalogMod.AuthorURL))
                {
                    catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(catalogMod.AuthorURL) ?
                        ActiveCatalog.Instance.AuthorURLDictionary[catalogMod.AuthorURL] : null;
                }

                if (catalogAuthor != null)
                {
                    // Get the author info from the catalog
                    AuthorName = catalogAuthor.Name;
                    AuthorIsRetired = catalogAuthor.Retired;
                }
                else
                {
                    // Can't find the author in the catalog, so use the author ID or author URL
                    AuthorName = catalogMod.AuthorID != 0 ? catalogMod.AuthorID.ToString() : catalogMod.AuthorURL ?? "";

                    Logger.Log($"Author not found in catalog: { AuthorName }", Logger.debug);
                }
            }

            // Get all compatibilities (including note) where this mod is either the first or the second ID
            AddCompatibility(ActiveCatalog.Instance.Compatibilities.FindAll(x => x.SteamID1 == SteamID), firstID: true);

            AddCompatibility(ActiveCatalog.Instance.Compatibilities.FindAll(x => x.SteamID2 == SteamID), firstID: false);

            // Get the group that this mod is a member of
            Group group = ActiveCatalog.Instance.GetGroup(SteamID);

            if (group != default)
            {
                // Get all compatibilities (including note) where the group is either the first or the second ID
                AddCompatibility(ActiveCatalog.Instance.Compatibilities.FindAll(x => x.SteamID1 == group.GroupID), firstID: true);

                AddCompatibility(ActiveCatalog.Instance.Compatibilities.FindAll(x => x.SteamID2 == group.GroupID), firstID: false);
            }
        }


        // Add compatibilities to the list of compatibilities with the correct statuses; also add the corresponding note    [Todo 0.3] needs work
        private void AddCompatibility(List<Compatibility> newCompatibilities, bool firstID)
        {
            if (newCompatibilities == null)
            {
                return;
            }

            // Add the compatibilities one by one
            foreach (Compatibility newCompatibility in newCompatibilities)
            {
                if (newCompatibility.Status == default)
                {
                    // Default status indicates an empty status list; ignore this and continue with next compatibility
                    continue;
                }

                // firstID indicates which one is the subscription, but we want the ID of the other mod or group
                if (firstID)
                {
                    // Add the compatibility status to the list, with the Steam ID for the other mod or group
                    if (Compatibilities.ContainsKey(newCompatibility.SteamID2))
                    {
                        Compatibilities[newCompatibility.SteamID2].Add(newCompatibility.Status);
                    }
                    else
                    {
                        Compatibilities.Add(newCompatibility.SteamID2, new List<Enums.CompatibilityStatus> { newCompatibility.Status });
                    }

                    // Add the compatibility note to the list
                    ModNotes.Add(newCompatibility.SteamID2, newCompatibility.Note);
                }
                else
                {
                    // We need to 'mirror' some statuses without affecting the catalog
                    Enums.CompatibilityStatus newStatus = newCompatibility.Status;

                    if (newCompatibility.Status == Enums.CompatibilityStatus.NewerVersion)
                    {
                        newStatus = Enums.CompatibilityStatus.OlderVersion;
                    }
                    else if (newCompatibility.Status == Enums.CompatibilityStatus.FunctionalityCovered)
                    {
                        newStatus = Enums.CompatibilityStatus.FunctionalityCoveredByOther;
                    }

                    // Add the compatibility status to the list, with the Steam ID for the other mod or group
                    if (Compatibilities.ContainsKey(newCompatibility.SteamID1))
                    {
                        Compatibilities[newCompatibility.SteamID1].Add(newStatus);
                    }
                    else
                    {
                        Compatibilities.Add(newCompatibility.SteamID1, new List<Enums.CompatibilityStatus> { newStatus });
                    }

                    // The compatibility note should only be displayed for the first mod in the compatibility, so skip it here  [Todo 0.3] Not really true, fix this
                }
            }
        }


        // Return a max length, formatted string with the Steam ID and name
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
    }
}
