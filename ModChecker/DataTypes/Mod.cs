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

        public List<string> OtherAuthors { get; private set; } = new List<string>();

        // Version of the mod, if it is indicated in the name or on the Steam Workshop page
        public string Version { get; private set; }

        // Date the mod was published and last updated
        public DateTime? Published { get; private set; }

        public DateTime? Updated { get; private set; }

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

        // Recommendations from the mod author
        [XmlArrayItem("SteamID")] public List<ulong> Recommendations { get; private set; } = new List<ulong>();

        // General note about this mod
        public string Note { get; private set; }

        // Statuses for this mod
        public List<Enums.ModStatus> Statuses { get; private set; } = new List<Enums.ModStatus>();

        // Date this mod was last reviewed for changes in information and compatibility
        public DateTime? ReviewUpdated { get; private set; }

        // Remark for ourselves, not displayed in report or log (but publicly viewable in the catalog)
        public string CatalogRemark { get; private set; }


        // Default constructor, used when reading from disk
        public Mod()
        {
            // Nothing to do here
        }


        // Constructor with all one to all parameters, used when creating/updating a catalog or when converting an old catalog
        public Mod(ulong steamID,
                   string name = "",
                   string authorID = "",
                   List<string> otherAuthors = null,
                   string version = "",
                   DateTime? published = null,
                   DateTime? updated = null,
                   bool removed = false,
                   string archiveURL = "",
                   string sourceURL = "",
                   string gameVersionCompatible = "",
                   List<Enums.DLC> dlcRequired = null,
                   List<ulong> modsRequired = null,
                   List<ulong> modsRecommended = null,
                   List<ulong> onlyNeededFor = null,
                   string note = "",
                   List<Enums.ModStatus> statuses = null,
                   DateTime? reviewUpdated = null)
        {
            SteamID = steamID;

            Name = name;

            AuthorID = authorID;

            OtherAuthors = otherAuthors;

            Version = version;

            Published = published;

            Updated = updated;

            IsRemoved = removed;

            ArchiveURL = archiveURL;

            SourceURL = sourceURL;

            CompatibleGameVersionString = gameVersionCompatible;

            RequiredDLC = dlcRequired;

            RequiredMods = modsRequired;

            Recommendations = modsRecommended;

            NeededFor = onlyNeededFor;

            Note = note;

            Statuses = statuses;

            ReviewUpdated = reviewUpdated;
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
