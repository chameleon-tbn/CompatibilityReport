using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using ModChecker.Util;


namespace ModChecker.DataTypes
{
    [Serializable]
    public class Mod                                // Needs to be public for XML serialization
    {
        public ulong SteamID { get; private set; } = 0;

        public string Name { get; private set; }

        public string AuthorTag { get; private set; }

        public List<string> OtherAuthors { get; private set; } = new List<string>();                            // Can be none

        public string Version { get; private set; }

        public DateTime? Updated { get; private set; }

        public DateTime? Published { get; private set; }

        public bool IsRemoved { get; private set; }                                                             // Removed from the Steam Workshop (or set private)

        public string ArchiveURL { get; private set; }                                                          // Archive of the Steam Workshop page

        public string SourceURL { get; private set; }

        public Version GameVersionCompatible { get; private set; } = GameVersion.Unknown;       // Unfinished, does not get serialized; needs string conversion

        public List<Enums.DLC> RequiredDLC { get; private set; } = new List<Enums.DLC>();                       // Can be none

        [XmlArrayItem("SteamID")] public List<ulong> RequiredMods { get; private set; } = new List<ulong>();    // Can be none; can contain groups

        [XmlArrayItem("SteamID")] public List<ulong> NeededFor { get; private set; } = new List<ulong>();       // Can be none; no groups; used if it's only a dependency mod

        [XmlArrayItem("SteamID")] public List<ulong> SucceededBy { get; private set; } = new List<ulong>();     // Can be none; no groups

        [XmlArrayItem("SteamID")] public List<ulong> Alternatives { get; private set; } = new List<ulong>();    // Can be none; no groups; only if it has (comp.) issues

        [XmlArrayItem("SteamID")] public List<ulong> Recommendations { get; private set; } = new List<ulong>(); // Can be none; no groups; rec. by the mod author

        public string Note { get; private set; }

        public List<Enums.ModStatus> Statuses { get; private set; } = new List<Enums.ModStatus>();              // Can be none

        public DateTime? ReviewUpdated { get; private set; }                                                    // Date this was last checked for compatibilities and changes

        public string CatalogRemark { get; private set; }                                                       // Only used for myself for remarks about the mod info


        // Default constructor
        public Mod()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        public Mod(ulong steamID, string name = "", string authorTag = "", List<string> otherAuthors = null, string version = "", DateTime? updated = null,
            DateTime? published = null, bool removed = false, string archiveURL = "", string sourceURL = "", Version gameVersionCompatible = null, 
            List<Enums.DLC> dlcRequired = null, List<ulong> modsRequired = null, List<ulong> modsRecommended = null, List<ulong> onlyNeededFor = null, 
            string note = "", List<Enums.ModStatus> statuses = null, DateTime? reviewUpdated = null)
        {
            SteamID = steamID;

            Name = name;

            AuthorTag = authorTag;

            OtherAuthors = otherAuthors;

            Version = version;

            Updated = updated;

            Published = published;

            IsRemoved = removed;

            ArchiveURL = archiveURL;

            SourceURL = sourceURL;

            GameVersionCompatible = gameVersionCompatible;

            RequiredDLC = dlcRequired;

            RequiredMods = modsRequired;

            Recommendations = modsRecommended;

            NeededFor = onlyNeededFor;

            Note = note;

            Statuses = statuses;

            ReviewUpdated = reviewUpdated;
        }


        // Return a max sized, formatted string with the Steam ID and name
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
