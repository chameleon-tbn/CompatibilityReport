using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using ModChecker.Util;


namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class Mod
    {
        // Steam ID and name
        public ulong SteamID { get; private set; }

        public string Name { get; private set; }

        // Author Profile ID and Author Custom URL; only one is needed to identify the author; ID is more reliable
        public ulong AuthorID { get; private set; }

        public string AuthorURL { get; private set; }

        // Date the mod was published and last updated on the Steam Workshop
        public DateTime Published { get; private set; }

        public DateTime Updated { get; private set; }

        // An archive page of the Steam Workshop page, for mods that were removed from the Steam Workshop
        public string ArchiveURL { get; private set; }

        // Public location of the source
        public string SourceURL { get; private set; }

        // Game version this mod is compatible with; 'Version' is not serializable, so we use a string; convert to Version when needed
        public string CompatibleGameVersionString { get; private set; }

        // Required DLCs
        public List<Enums.DLC> RequiredDLC { get; private set; } = new List<Enums.DLC>();

        // Required mods for this mod (all are required); this is the only list that allows mod groups, meaning one (not all) of the mods in the group is required
        [XmlArrayItem("SteamID")] public List<ulong> RequiredMods { get; private set; } = new List<ulong>();

        // Required assets for this mod; only used to collect info we gather from the Steam Workshop, not reported right now
        [XmlArrayItem("SteamID")] public List<ulong> RequiredAssets { get; private set; } = new List<ulong>();

        // Mods this is needed for; only used when this is purely a dependency mod (like Harmony)
        [XmlArrayItem("SteamID")] public List<ulong> NeededFor { get; private set; } = new List<ulong>();

        // Successors of this mod
        [XmlArrayItem("SteamID")] public List<ulong> SucceededBy { get; private set; } = new List<ulong>();

        // Alternatives for this mod; only used if this has compatibility issues
        [XmlArrayItem("SteamID")] public List<ulong> Alternatives { get; private set; } = new List<ulong>();

        // Statuses for this mod
        public List<Enums.ModStatus> Statuses { get; private set; } = new List<Enums.ModStatus>();

        // General note about this mod; this is included in the report
        public string Note { get; private set; }

        // Date this mod was last manually and automatically reviewed for changes in information and compatibilities
        public DateTime ReviewUpdated { get; private set; }

        public DateTime AutoReviewUpdated { get; private set; }

        // Change notes, automatically filled by the updater; not displayed in report or log, but visible in the catalog
        public string ChangeNotes { get; private set; }


        // Default constructor
        public Mod()
        {
            // Nothing to do here
        }


        // Constructor with 4 parameters
        internal Mod(ulong steamID, string name, ulong authorID, string authorURL)
        {
            SteamID = steamID;

            Name = name ?? "";

            AuthorID = authorID;

            AuthorURL = authorURL ?? "";
        }


        // Update a mod with new info; all fields can be updated except Steam ID; all fields are optional, only supplied fields are updated
        internal void Update(string name = null,
                             ulong authorID = 0,
                             string authorURL = null,
                             DateTime? published = null,
                             DateTime? updated = null,
                             string archiveURL = null,
                             string sourceURL = null,
                             string compatibleGameVersionString = null,
                             List<Enums.DLC> requiredDLC = null,
                             List<ulong> requiredMods = null,
                             List<ulong> requiredAssets = null,
                             List<ulong> neededFor = null,
                             List<ulong> succeededBy = null,
                             List<ulong> alternatives = null,
                             List<Enums.ModStatus> statuses = null,
                             string note = null,
                             DateTime? reviewUpdated = null,
                             DateTime? autoReviewUpdated = null,
                             string changeNotes = null)
        {
            // Only update supplied fields, so ignore every null value; make sure strings are set to empty strings instead of null
            Name = name ?? Name ?? "";

            AuthorID = authorID == 0 ? AuthorID : authorID;

            AuthorURL = authorURL ?? AuthorURL ?? "";

            Published = published ?? Published;

            Updated = updated ?? Updated;

            // If the updated date is older than published, set it to published
            if (Updated < Published)
            {
                Updated = Published;
            }

            ArchiveURL = archiveURL ?? ArchiveURL ?? "";

            SourceURL = sourceURL ?? SourceURL ?? "";

            // If the string is null, set it to the unknown game version
            CompatibleGameVersionString = compatibleGameVersionString ?? CompatibleGameVersionString ?? GameVersion.Unknown.ToString();

            // Make sure the lists are empty lists instead of null
            RequiredDLC = requiredDLC ?? RequiredDLC ?? new List<Enums.DLC>();

            RequiredMods = requiredMods ?? RequiredMods ?? new List<ulong>();

            RequiredAssets = requiredAssets ?? RequiredAssets ?? new List<ulong>();

            NeededFor = neededFor ?? NeededFor ?? new List<ulong>();

            SucceededBy = succeededBy ?? SucceededBy ?? new List<ulong>();

            Alternatives = alternatives ?? Alternatives ?? new List<ulong>();

            Statuses = statuses ?? Statuses ?? new List<Enums.ModStatus>();

            Note = note ?? Note ?? "";

            ReviewUpdated = reviewUpdated ?? ReviewUpdated;

            AutoReviewUpdated = autoReviewUpdated ?? AutoReviewUpdated;

            // Add a change note (on a new line) instead of replacing it
            ChangeNotes += string.IsNullOrEmpty(changeNotes) ? "" : (string.IsNullOrEmpty(ChangeNotes) ? "" : "\n") + changeNotes;
        }


        // Return a max length, formatted string with the Steam ID and name
        internal string ToString(bool nameFirst = false,
                                 bool showFakeID = true,
                                 bool cutOff = true)
        {
            string id;

            if (SteamID > ModSettings.highestFakeID)
            {
                // Workshop mod
                id = $"[Steam ID { SteamID, 10 }]";
            }
            else if ((SteamID >= ModSettings.lowestLocalModID) && (SteamID <= ModSettings.highestLocalModID))
            {
                // Local mod
                id = "[local mod" + (showFakeID ? " " + SteamID.ToString() : "") + "]";
            }
            else
            {
                // Builtin mod
                id = "[builtin mod" + (showFakeID ? " " + SteamID.ToString() : "") + "]";
            }

            int maxNameLength = ModSettings.maxReportWidth - 1 - id.Length;

            // Cut off the name to max. length, if the cutOff parameter is true
            string name = (Name.Length <= maxNameLength) || !cutOff ? Name : Name.Substring(0, maxNameLength - 3) + "...";

            if (nameFirst)
            {
                return name + " " + id;
            }
            else
            {
                return id + " " + name;
            }
        }
    }
}
