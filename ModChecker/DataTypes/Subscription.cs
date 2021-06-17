using System;
using System.Collections.Generic;
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

        internal List<Enums.ModStatus> Statuses { get; private set; } = new List<Enums.ModStatus>();

        // Generic note
        internal string Note { get; private set; }

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


        // Constructor with plugin parameter; get all information from plugin and catalog [Todo 0.5] This constructor is large, is that problematic?
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

            // Get the time this mod was downloaded/updated; [Todo 0.5] How reliable is this? Is ToLocalTime needed?
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
                // Mod not found in catalog; set to not reviewed
                IsReviewed = false;
                
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

            // Log the catalog name which is seen in the Steam Workshop; subscription name might differ and is seen in game in the Content Manager and Options
            Logger.Log($"Mod found: { catalogMod.ToString() }");

            if (catalogMod.Name.ToLower() != Name.ToLower())
            {
                // Log both names as input for manual changes in the catalog
                Logger.Log($"{ catalogMod.ToString() }: Steam Workshop name differs from subscription name ({ Name })", Logger.debug);
            }

            // Check if the mod was manually reviewed
            IsReviewed = catalogMod.ReviewUpdated != DateTime.MinValue;

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

            // Get all compatibilities (including notes) where this mod is either the first or the second ID
            AddCompatibility(ActiveCatalog.Instance.Compatibilities.FindAll(x => x.SteamID1 == SteamID), firstID: true);
            AddCompatibility(ActiveCatalog.Instance.Compatibilities.FindAll(x => x.SteamID2 == SteamID), firstID: false);

            // Get all mod groups that this mod is a member of
            List<ModGroup> modGroups = ActiveCatalog.Instance.ModGroups.FindAll(x => x.SteamIDs.Contains(SteamID));

            if (modGroups != null)
            {
                // go through all found mod groups
                foreach (ModGroup group in modGroups)
                {
                    // Get all compatibilities (including notes) where the mod group is either the first or the second ID
                    AddCompatibility(ActiveCatalog.Instance.Compatibilities.FindAll(x => x.SteamID1 == group.GroupID), firstID: true);
                    AddCompatibility(ActiveCatalog.Instance.Compatibilities.FindAll(x => x.SteamID2 == group.GroupID), firstID: false);
                }
            }
        }


        // Add compatibilities to the list of compatibilities with the correct statuses; also add the corresponding note
        private void AddCompatibility(List<Compatibility> compatibilities, bool firstID)
        {
            if (compatibilities == null)
            {
                return;
            }

            // Add the compatibilities one by one
            foreach (Compatibility compatibility in compatibilities)
            {
                if (compatibility.Statuses.Contains(Enums.CompatibilityStatus.Unknown))
                {
                    // Unknown status indicates an empty status list; ignore this and continue with next compatibility
                    continue;
                }

                // firstID indicates which one is the subscription, but we want the ID of the other mod or group
                if (firstID)
                {
                    // Add the compatibility status to the list, with the Steam ID for the other mod or group
                    Compatibilities.Add(compatibility.SteamID2, compatibility.Statuses);

                    // Add the mod note for this compatibility to the list
                    ModNotes.Add(compatibility.SteamID2, compatibility.NoteAboutMod2);
                }
                else
                {
                    // We need to 'flip' some statuses without affecting the catalog, so gather the statuses in a new list first
                    List<Enums.CompatibilityStatus> statuses = new List<Enums.CompatibilityStatus>();

                    // Add each status to this new list, flipping some
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
