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

        // Author ID for main author and additional authors; can be linked to the ModAuthors info
        public string AuthorID { get; private set; }

        // Date the mod was published and last updated
        public DateTime Published { get; private set; }

        public DateTime Updated { get; private set; }

        // Is the mod removed, and do we know an archive page of the Steam Workshop page
        public string ArchiveURL { get; private set; }

        // Public location of the source
        public string SourceURL { get; private set; }

        // Game version this mod is compatible with; 'Version' is not serializable, so we use a string; convert to Version when needed
        public string CompatibleGameVersionString { get; private set; }

        // Required DLCs
        public List<Enums.DLC> RequiredDLC { get; private set; } = new List<Enums.DLC>();

        // Required mods for this mod; this is the only list that allows mod groups, meaning one (not all) of the mods in that group is required
        [XmlArrayItem("SteamID")] public List<ulong> RequiredMods { get; private set; } = new List<ulong>();

        // Mods this is needed for; only used when this is purely a dependency mod
        [XmlArrayItem("SteamID")] public List<ulong> NeededFor { get; private set; } = new List<ulong>();

        // Successors of this mod
        [XmlArrayItem("SteamID")] public List<ulong> SucceededBy { get; private set; } = new List<ulong>();

        // Alternatives for this mod; only used if this has compatibility issues
        [XmlArrayItem("SteamID")] public List<ulong> Alternatives { get; private set; } = new List<ulong>();

        // Statuses for this mod
        public List<Enums.ModStatus> Statuses { get; private set; } = new List<Enums.ModStatus>();

        // General note about this mod
        public string Note { get; private set; }

        // Date this mod was last manually and automatically reviewed for changes in information and compatibility
        public DateTime ReviewUpdated { get; private set; }

        public DateTime AutoReviewUpdated { get; private set; }

        // Remark for ourselves, not displayed in report or log (but publicly viewable in the catalog)
        public string CatalogRemark { get; private set; }


        // Default constructor
        public Mod()
        {
            // Nothing to do here
        }


        // Constructor with 3 parameters
        internal Mod(ulong steamID,
                     string name,
                     string authorID)
        {
            SteamID = steamID;

            Name = name ?? "";

            AuthorID = authorID ?? "";
        }


        // Update a mod with new info; all fields can be updated except Steam ID; all fields are optional, only supplied fields are updated
        internal void Update(string name = null,
                             string authorID = null,
                             DateTime? published = null,
                             DateTime? updated = null,
                             string archiveURL = null,
                             string sourceURL = null,
                             string compatibleGameVersionString = null,
                             List<Enums.DLC> requiredDLC = null,
                             List<ulong> requiredMods = null,
                             List<ulong> onlyNeededFor = null,
                             List<ulong> succeededBy = null,
                             List<ulong> alternatives = null,
                             List<Enums.ModStatus> statuses = null,
                             string note = null,
                             DateTime? reviewUpdated = null,
                             DateTime? autoReviewUpdated = null,
                             string catalogRemark = null)
        {
            // Only update supplied fields, so ignore every null value; make sure strings are set to empty string instead of null
            Name = name ?? Name ?? "";

            AuthorID = authorID ?? AuthorID ?? "";

            Published = published ?? Published;

            Updated = updated ?? Updated;

            // If Updated is unknown (default minvalue), set it to the Published date
            if (Updated == DateTime.MinValue)
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

            NeededFor = onlyNeededFor ?? NeededFor ?? new List<ulong>();

            SucceededBy = succeededBy ?? SucceededBy ?? new List<ulong>();

            Alternatives = alternatives ?? Alternatives ?? new List<ulong>();

            Statuses = statuses ?? Statuses ?? new List<Enums.ModStatus>();

            Note = note ?? Note ?? "";

            ReviewUpdated = reviewUpdated ?? ReviewUpdated;

            AutoReviewUpdated = autoReviewUpdated ?? AutoReviewUpdated;

            CatalogRemark = catalogRemark ?? CatalogRemark ?? "";
        }


        // Return a max length, formatted string with the Steam ID and name
        internal string ToString(bool nameFirst = false,
                                 bool showFakeID = true)
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

            string name = (Name.Length <= maxNameLength) ? Name : Name.Substring(0, maxNameLength - 3) + "...";

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
