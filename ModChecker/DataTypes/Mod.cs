using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using ModChecker.Util;


namespace ModChecker.DataTypes
{
    [Serializable]
    public class Mod                                // Needs to be public for XML serialization
    {
        // Steam ID and name
        public ulong SteamID { get; private set; } = 0;

        public string Name { get; private set; }

        // Author ID for main author and additional authors; can be linked to the ModAuthors info
        public string AuthorID { get; private set; }

        // Date the mod was published and last updated
        public DateTime Published { get; private set; }

        public DateTime Updated { get; private set; }

        // Is the mod removed, and do we know an archive page of the Steam Workshop page
        public bool IsRemoved { get; private set; }

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


        // Default constructor, used when reading from disk
        public Mod()
        {
            // Nothing to do here
        }


        // Constructor with all one to three parameters, used when creating a catalog or when converting an old catalog
        public Mod(ulong steamID,
                   string name = "",
                   string authorID = "")
        {
            SteamID = steamID;

            Name = name;

            AuthorID = authorID;
        }


        // Update a mod with new info; all fields can be updated except Steam ID
        internal void Update(string name = "",
                   string authorID = "",
                   DateTime published = default,
                   DateTime updated = default,
                   bool isRemoved = false,
                   string archiveURL = "",
                   string sourceURL = "",
                   string gameVersionCompatible = "",
                   List<Enums.DLC> requiredDLC = default,
                   List<ulong> requiredMods = default,
                   List<ulong> onlyNeededFor = default,
                   List<ulong> succeededBy = default,
                   List<ulong> alternatives = default,
                   List<Enums.ModStatus> statuses = default,
                   string note = "",
                   DateTime reviewUpdated = default,
                   DateTime autoReviewUpdated = default,
                   string catalogRemark = "")
        {
            Name = name;

            AuthorID = authorID;

            Published = published;

            // If updated is unknown (default minvalue), set it to the published date
            Updated = (updated == DateTime.MinValue) ? Published : updated;

            IsRemoved = isRemoved;

            ArchiveURL = archiveURL;

            SourceURL = sourceURL;

            CompatibleGameVersionString = gameVersionCompatible;

            // Make sure the lists are empty lists instead of null
            RequiredDLC = requiredDLC ?? new List<Enums.DLC>();

            RequiredMods = requiredMods ?? new List<ulong>();

            NeededFor = onlyNeededFor ?? new List<ulong>();

            SucceededBy = succeededBy ?? new List<ulong>();

            Alternatives = alternatives ?? new List<ulong>();

            Statuses = statuses ?? new List<Enums.ModStatus>();

            Note = note;

            ReviewUpdated = reviewUpdated;

            AutoReviewUpdated = autoReviewUpdated;

            CatalogRemark = catalogRemark;
        }


        // Return a max length, formatted string with the Steam ID and name
        internal string ToString(bool nameFirst = false, bool showFakeID = true)
        {
            string id;

            if (SteamID > ModSettings.HighestFakeID)
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

            int maxNameLength = ModSettings.MaxReportWidth - 1 - id.Length;

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
