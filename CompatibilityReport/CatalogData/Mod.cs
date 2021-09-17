using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using CompatibilityReport.Util;

namespace CompatibilityReport.CatalogData
{
    [Serializable] 
    public class Mod
    {
        public ulong SteamID { get; private set; }
        public string Name { get; private set; }

        public DateTime Published { get; private set; }
        public DateTime Updated { get; private set; }

        public ulong AuthorID { get; private set; }
        public string AuthorUrl { get; private set; }

        public string SourceUrl { get; private set; }

        // Game version this mod is compatible with. 'Version' is not serializable, so a converted string is used.
        public string CompatibleGameVersionString { get; private set; }

        public List<Enums.Dlc> RequiredDlcs { get; private set; } = new List<Enums.Dlc>();

        // No mod should be in more than one of the required mods, successors, alternatives and recommendations.
        [XmlArrayItem("SteamID")] public List<ulong> RequiredMods { get; private set; } = new List<ulong>();
        [XmlArrayItem("SteamID")] public List<ulong> Successors { get; private set; } = new List<ulong>();
        [XmlArrayItem("SteamID")] public List<ulong> Alternatives { get; private set; } = new List<ulong>();
        [XmlArrayItem("SteamID")] public List<ulong> Recommendations { get; private set; } = new List<ulong>();

        public Enums.Stability Stability { get; private set; } = Enums.Stability.NotReviewed;
        public string StabilityNote { get; private set; }

        public List<Enums.Status> Statuses { get; private set; } = new List<Enums.Status>();
        public string GenericNote { get; private set; }

        public bool ExclusionForSourceUrl { get; private set; }
        public bool ExclusionForGameVersion { get; private set; }
        public bool ExclusionForNoDescription { get; private set; }
        public List<Enums.Dlc> ExclusionForRequiredDlc { get; private set; } = new List<Enums.Dlc>();
        [XmlArrayItem("SteamID")] public List<ulong> ExclusionForRequiredMods { get; private set; } = new List<ulong>();

        // Date of the last review of this mod, imported by the FileImporter, and the last automatic review for changes in mod information (WebCrawler).
        public DateTime ReviewDate { get; private set; }
        public DateTime AutoReviewDate { get; private set; }
        [XmlArrayItem("ChangeNote")] public List<string> ChangeNotes { get; private set; } = new List<string>();

        // Properties used by the Reporter for subscribed mods.
        [XmlIgnore] public bool IsDisabled { get; private set; }
        [XmlIgnore] public bool IsCameraScript { get; private set; }
        [XmlIgnore] public DateTime DownloadedTime { get; private set; }

        // Properties used by the Updater, to see if this mod was added or updated this session.
        [XmlIgnore] public bool AddedThisSession { get; private set; }
        [XmlIgnore] public bool UpdatedThisSession { get; private set; }


        /// <summary>Default constructor for deserialization.</summary>
        private Mod()
        {
            // Nothing to do here
        }


        /// <summary>Constructor for mod creation.</summary>
        public Mod(ulong steamID)
        {
            SteamID = steamID;

            AddedThisSession = true;
        }


        /// <summary>Gets the game version this mod is compatible with.</summary>
        /// <returns>The game version this mod is compatible with.</returns>
        public Version CompatibleGameVersion()
        {
            return Toolkit.ConvertToVersion(CompatibleGameVersionString);
        }


        /// <summary>Updates one or more mod properties.</summary>
        public void Update(string name = null,
                           DateTime published = default,
                           DateTime updated = default,
                           ulong authorID = 0,
                           string authorUrl = null,
                           string sourceUrl = null,
                           string compatibleGameVersionString = null,
                           Enums.Stability stability = default,
                           string stabilityNote = null,
                           string genericNote = null,
                           DateTime reviewDate = default,
                           DateTime autoReviewDate = default)
        {
            Name = name ?? Name ?? "";

            // If the updated date is older than published, set it to published.
            Published = published == default ? Published : published;
            Updated = updated == default ? Updated : updated;
            Updated = Updated < Published ? Published : Updated;

            AuthorID = authorID == 0 ? AuthorID : authorID;
            AuthorUrl = authorUrl ?? AuthorUrl ?? "";

            SourceUrl = sourceUrl ?? SourceUrl ?? "";
            CompatibleGameVersionString = compatibleGameVersionString ?? CompatibleGameVersionString ?? Toolkit.UnknownVersion().ToString();

            Stability = stability == default ? Stability : stability;
            StabilityNote = stabilityNote ?? StabilityNote ?? "";

            GenericNote = genericNote ?? GenericNote ?? "";

            ReviewDate = reviewDate == default ? ReviewDate : reviewDate;
            AutoReviewDate = autoReviewDate == default ? AutoReviewDate : autoReviewDate;

            UpdatedThisSession = true;
        }


        /// <summary>Updates one or more exclusions.</summary>
        public void UpdateExclusions(bool? exclusionForSourceUrl = null, bool? exclusionForGameVersion = null, bool? exclusionForNoDescription = null)
        {
            ExclusionForSourceUrl = exclusionForSourceUrl ?? ExclusionForSourceUrl;
            ExclusionForGameVersion = exclusionForGameVersion ?? ExclusionForGameVersion;
            ExclusionForNoDescription = exclusionForNoDescription ?? ExclusionForNoDescription;
        }


        /// <summary>Adds an exclusion for a required DLC.</summary>
        public void AddExclusion(Enums.Dlc requiredDLC)
        {
            if (!ExclusionForRequiredDlc.Contains(requiredDLC))
            {
                ExclusionForRequiredDlc.Add(requiredDLC);
            }
        }


        /// <summary>Adds an exclusion for a required mod.</summary>
        public void AddExclusion(ulong requiredMod)
        {
            if (!ExclusionForRequiredMods.Contains(requiredMod))
            {
                ExclusionForRequiredMods.Add(requiredMod);
            }
        }


        /// <summary>Updates the subscription properties.</summary>
        public void UpdateSubscription(bool isDisabled, bool isCameraScript, DateTime downloadedTime)
        {
            IsDisabled = isDisabled;
            IsCameraScript = isCameraScript;
            DownloadedTime = downloadedTime;
        }


        /// <summary>Adds a mod change note.</summary>
        public void AddChangeNote(string changeNote)
        {
            ChangeNotes.Add(changeNote);
        }


        /// <summary>Converts the mod to a string containing the Steam ID and name.</summary>
        /// <remarks>Optionally hides fake Steam IDs, puts the name before the ID, or cuts off the string at report width.</remarks>
        /// <returns>A string representing the mod.</returns>
        // Todo 0.4 cutoff not used, but that might change on Report revision.
        public string ToString(bool hideFakeID = false, bool nameFirst = false, bool cutOff = false)
        {
            string idString;

            if (SteamID > ModSettings.HighestFakeID)
            {
                // Steam Workshop mod
                idString = $"[Steam ID { SteamID, 10 }]";
            }
            else if ((SteamID >= ModSettings.LowestLocalModID) && (SteamID <= ModSettings.HighestLocalModID))
            {
                // Local mod
                idString = $"[local mod{ (hideFakeID ? "" : $" { SteamID }") }]";
            }
            else
            {
                // Builtin mod
                idString = $"[builtin mod{ (hideFakeID ? "" : $" { SteamID }") }]";
            }

            string disabledPrefix = IsDisabled ? "[Disabled] " : "";

            int maxNameLength = ModSettings.TextReportWidth - idString.Length - 1 - disabledPrefix.Length;
            string name = (Name.Length <= maxNameLength) || !cutOff ? Name : $"{ Name.Substring(0, maxNameLength - 3) }...";

            return nameFirst ? $"{ disabledPrefix }{ name } { idString }" : $"{ disabledPrefix }{ idString } { name }";
        }
    }
}
