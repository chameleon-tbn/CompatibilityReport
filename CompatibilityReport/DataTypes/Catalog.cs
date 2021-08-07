using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CompatibilityReport.Util;


namespace CompatibilityReport.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable][XmlRoot(ModSettings.xmlRoot)] public class Catalog
    {
        // Catalog structure version (major version); will only change on structural changes in the xml that make it incompatible with a previous structure version
        public uint StructureVersion { get; private set; }

        // Catalog version and date; version always increases and never resets, even when going to a new StructureVersion
        public uint Version { get; private set; }

        public DateTime UpdateDate { get; private set; }

        // Game version this catalog was created for; 'Version' is not serializable, so a converted string is used
        [XmlIgnore] internal Version CompatibleGameVersion { get; private set; }
        public string CompatibleGameVersionString { get; private set; }

        // A note about the catalog, displayed in the report header
        public string Note { get; private set; }

        // Intro and footer for the text report
        public string ReportIntroText { get; private set; }

        public string ReportFooterText { get; private set; }

        // The actual mod data in four lists
        public List<Mod> Mods { get; private set; } = new List<Mod>();

        public List<Compatibility> Compatibilities { get; private set; } = new List<Compatibility>();

        public List<Group> Groups { get; private set; } = new List<Group>();

        public List<Author> Authors { get; private set; } = new List<Author>();

        // Update-exclusions; these prevent certain changes by the AutoUpdater
        public List<Exclusion> Exclusions { get; private set; } = new List<Exclusion>();

        // Assets that show up as required items; listed to see the difference between a required asset and a required mod we don't know (unlisted or removed mod)
        [XmlArrayItem("SteamID")] public List<ulong> Assets { get; private set; } = new List<ulong>();


        // Dictionaries to make searching easier and faster
        [XmlIgnore] internal Dictionary<ulong, Mod> ModDictionary { get; private set; } = new Dictionary<ulong, Mod>();
        
        [XmlIgnore] internal Dictionary<ulong, Group> GroupDictionary { get; private set; } = new Dictionary<ulong, Group>();

        [XmlIgnore] internal Dictionary<ulong, Author> AuthorIDDictionary { get; private set; } = new Dictionary<ulong, Author>();

        [XmlIgnore] internal Dictionary<string, Author> AuthorURLDictionary { get; private set; } = new Dictionary<string, Author>();


        // The total number of mods in the catalog
        [XmlIgnore] internal int Count { get; private set; }
        [XmlIgnore] internal int ReviewCount { get; private set; }


        // Default constructor, used when creating an empty catalog for reading from disk
        public Catalog()
        {
            // Nothing to do here
        }


        // Constructor with 3 to 5 parameters, used when creating a new catalog
        internal Catalog(uint version,
                         DateTime updateDate,
                         string note,
                         string reportIntroText = null,
                         string reportFooterText = null)
        {
            StructureVersion = ModSettings.currentCatalogStructureVersion;

            Version = version;

            UpdateDate = updateDate;

            CompatibleGameVersion = GameVersion.Current;

            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            Note = note ?? "";

            ReportIntroText = reportIntroText ?? ModSettings.defaultIntroText;

            ReportFooterText = reportFooterText ?? ModSettings.defaultFooterText;
        }


        // Constructor with all parameters, used when converting an old catalog
        internal Catalog(uint version, DateTime updateDate, Version compatibleGameVersion, string note, string reportIntroText, string reportFooterText, 
            List<Mod> mods, List<Compatibility> modCompatibilities, List<Group> groups, List<Author> modAuthors, List<Exclusion> exclusions, List<ulong> assets)
        {
            StructureVersion = ModSettings.currentCatalogStructureVersion;

            Version = version;

            UpdateDate = updateDate;

            CompatibleGameVersion = compatibleGameVersion;
            
            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            Note = note ?? "";

            ReportIntroText = reportIntroText ?? "";

            ReportFooterText = reportFooterText ?? "";

            Mods = mods ?? new List<Mod>();

            Compatibilities = modCompatibilities ?? new List<Compatibility>();

            Groups = groups ?? new List<Group>();

            Authors = modAuthors ?? new List<Author>();

            Exclusions = exclusions ?? new List<Exclusion>();

            Assets = assets ?? new List<ulong>();
        }


        // Update the catalog; all fields are optional, only supplied fields are updated; the lists can be updated directly, version has it's own update method
        internal void Update(string note = null, 
                             Version compatibleGameVersion = null, 
                             string reportIntroText = null, 
                             string reportFooterText = null)
        {
            Note = note ?? Note;

            CompatibleGameVersion = compatibleGameVersion ?? CompatibleGameVersion;

            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            ReportIntroText = reportIntroText ?? ReportIntroText;

            ReportFooterText = reportFooterText ?? ReportFooterText;
        }


        // Catalog version string, for reporting and logging
        internal string VersionString() => $"{ StructureVersion }.{ Version:D4}";


        // Increase the version with a new update date (defaults to now); used for the Updater
        internal void NewVersion(DateTime? updated = null)
        {
            Version++;

            UpdateDate = updated ?? DateTime.Now;
        }


        // Change the compatible game version to a higher version
        internal bool UpdateGameVersion(Version newGameVersion)
        {
            // Exit on incorrect game version, or if the new game version is not higher than the current
            if (newGameVersion == null || newGameVersion <= CompatibleGameVersion)
            {
                return false;
            }

            CompatibleGameVersion = newGameVersion;

            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            return true;
        }


        // Add a new mod to the catalog
        internal Mod AddMod(ulong steamID,
                            string name = "",
                            ulong authorID = 0,
                            string authorURL = "",
                            DateTime? published = null,
                            DateTime? updated = null,
                            string archiveURL = null,
                            string sourceURL = null,
                            string compatibleGameVersionString = null,
                            List<Enums.DLC> requiredDLC = null,
                            List<ulong> requiredMods = null,
                            List<ulong> successors = null,
                            List<ulong> alternatives = null,
                            List<ulong> recommendations = null,
                            List<Enums.ModStatus> statuses = null,
                            string note = null,
                            DateTime? reviewUpdated = null,
                            DateTime? autoReviewUpdated = null,
                            string changeNoteString = null)
        {
            // Create a new mod
            Mod mod = new Mod(steamID, name, authorID, authorURL);

            if (!ModDictionary.ContainsKey(steamID))
            {
                // Add the mod to the list and dictionary
                Mods.Add(mod);

                ModDictionary.Add(steamID, mod);
            }
            else
            {
                // This mod already exists; update the existing one
                mod = ModDictionary[steamID];

                Logger.Log($"Tried to add mod [{ steamID }] while it already existed. Updating existing mod instead.", Logger.error);
            }

            // Create change notes list
            List<string> changeNotes = new List<string>();

            if (!string.IsNullOrEmpty(changeNoteString))
            {
                changeNotes.Add(changeNoteString);
            }

            // Add all info to the mod
            mod.Update(name, authorID, authorURL, published, updated, archiveURL, sourceURL, compatibleGameVersionString, requiredDLC, requiredMods,
                successors, alternatives, recommendations, statuses, note, reviewUpdated, autoReviewUpdated, changeNotes);

            // Return a reference to the new mod
            return mod;
        }


        // Add a new author to the catalog; return the author or null if they couldn't be added
        internal Author AddAuthor(ulong authorID,
                                  string authorURL,
                                  string name,
                                  DateTime lastSeen = default,
                                  bool retired = false,
                                  string changeNoteString = null)
        {
            if (AuthorIDDictionary.ContainsKey(authorID) || AuthorURLDictionary.ContainsKey(authorURL ?? "")) 
            {
                // Author already exists
                Logger.Log($"Tried to add an author with a profile ID ({ authorID }) or custom URL ({ authorURL }) that is already used. Author NOT added.", Logger.error);

                return null;
            }

            // Create change notes list
            List<string> changeNotes = new List<string>();

            if (!string.IsNullOrEmpty(changeNoteString))
            {
                changeNotes.Add(changeNoteString);
            }
            
            // Create a new author
            Author author = new Author(authorID, authorURL, name, lastSeen, retired, changeNotes);

            // Add the author to the list
            Authors.Add(author);

            // Add the new author to one or two dictionaries
            if (authorID != 0)
            {
                AuthorIDDictionary.Add(authorID, author);
            }

            if (!string.IsNullOrEmpty(authorURL))
            {
                AuthorURLDictionary.Add(authorURL, author);
            }

            // Return a reference to the new author
            return author;
        }


        // Get the group this mod is a member of
        internal Group GetGroup(ulong steamID)
        {
            return steamID == 0 ? default : Groups.FirstOrDefault(x => x.SteamIDs.Contains(steamID));
        }


        // Check if a mod is a group member
        internal bool IsGroupMember(ulong steamID)
        {
            return GetGroup(steamID) != default;
        }


        // Add a new group to the catalog; return the group, or null if the group couldn't be added; NOTE: a mod can only be in one group
        internal Group AddGroup(string name, List<ulong> steamIDs)
        {
            // Check if none of the steam IDs is already in a group, or is a group itself
            foreach (ulong steamID in steamIDs ?? new List<ulong>())
            {
                if (steamID >= ModSettings.lowestGroupID && steamID <= ModSettings.highestGroupID)
                {
                    Logger.Log($"Could not add a new group ({ name }) because { steamID } is a group and groups can't be nested.", Logger.error);

                    return null;
                }

                if (IsGroupMember(steamID))
                {
                    Logger.Log($"Could not add a new group ({ name }) because [Steam ID { steamID }] is already member of another group.", Logger.error);

                    return null;
                }
            }

            // Get a new group ID
            ulong newGroupID;

            if (GroupDictionary?.Any() != true)
            {
                // No groups yet, so use the lowest group ID
                newGroupID = ModSettings.lowestGroupID;
            }
            else
            {
                // Set it one higher than the highest used group ID
                newGroupID = GroupDictionary.Keys.Max() + 1;

                if (newGroupID > ModSettings.highestGroupID)
                {
                    Logger.Log($"Could not add a new group ({ name }) because we ran out of group IDs.", Logger.error);

                    return null;
                }
            }

            // Add the new group to the list and dictionary
            Group group = new Group(newGroupID, name, steamIDs);

            Groups.Add(group);

            GroupDictionary.Add(newGroupID, group);

            // Replace required mods in the whole catalog from a group member to the group
            foreach (ulong groupMemberID in group.SteamIDs)
            {
                // Get all mods that have this group member as required mod
                List<Mod> modList = Mods.FindAll(x => x.RequiredMods.Contains(groupMemberID));

                foreach (Mod mod in modList)
                {
                    // Replace the mod ID with the group ID
                    mod.RequiredMods.Remove(groupMemberID);

                    mod.RequiredMods.Add(newGroupID);

                    Logger.Log($"Changed required mod { ModDictionary[groupMemberID].ToString() } to { group.ToString() }, for { mod.ToString(cutOff: false) }.",
                        Logger.debug);
                }
            }

            // Return a reference to the new group
            return group;
        }


        // Add a new compatibility to the catalog; return the compatibility, or null if the compatibility couldn't be added
        internal Compatibility AddCompatibility(ulong steamID1,
                                                ulong steamID2,
                                                List<Enums.CompatibilityStatus> statuses,
                                                string note = "")
        {
            if ((steamID1 >= ModSettings.lowestGroupID && steamID1 <= ModSettings.highestGroupID) 
                || (steamID2 >= ModSettings.lowestGroupID && steamID2 <= ModSettings.highestGroupID))
            {
                Logger.Log($"Tried to add a compatibility with a group instead of a mod, which is not supported. Compatibility NOT added.", Logger.error);

                return null;
            }

            if (Compatibilities.Find(x => x.SteamID1 == steamID1 && x.SteamID2 == steamID2) != default)
            {
                // A compatibility already exists between these Steam IDs
                Logger.Log($"Tried to add a compatibility while one already exists between [Steam ID { steamID1 }] and [Steam ID { steamID2 }]. " + 
                    "Compatibility NOT added.", Logger.error);

                return null;
            }

            // Check if a mirrored compatibility already exists; this is allowed for some statuses, but not all  [Todo 0.4] Can we allow all compatibilities mirrored?
            if (Compatibilities.Find(x => x.SteamID1 == steamID2 && x.SteamID2 == steamID1 && 
                !x.Statuses.Contains(Enums.CompatibilityStatus.NewerVersion) &&
                !x.Statuses.Contains(Enums.CompatibilityStatus.FunctionalityCovered) &&
                !x.Statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToAuthor) &&
                !x.Statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToUsers) &&
                !x.Statuses.Contains(Enums.CompatibilityStatus.CompatibleAccordingToAuthor)) != null)
            {
                Logger.Log($"Tried to add a compatibility between [Steam ID { steamID1 }] and [Steam ID { steamID2 }] while a reversed one already exists . " +
                    "Compatibility NOT added.", Logger.error);

                return null;
            }

            // Add a new compatibility to the list
            Compatibility compatibility = new Compatibility(steamID1, steamID2, statuses, note);

            Compatibilities.Add(compatibility);
            
            // Return a reference to the new compatibility
            return compatibility;
        }


        // Check if an exclusion exists
        internal bool ExclusionExists(ulong steamID, Enums.ExclusionCategory category, ulong subItem = 0)
        {
            bool result = Exclusions.FirstOrDefault(x => x.SteamID == steamID && x.Category == category && x.SubItem == subItem) != default;

            // If the exclusion doesn't exist, but the exclusion is about required mods, then check exclusion for group and group member(s)
            if (!result && (category == Enums.ExclusionCategory.RequiredMod || category == Enums.ExclusionCategory.NotRequiredMod))
            {
                if  (IsGroupMember(subItem))
                {
                    // Check exclusion for the group subItem is a member of
                    result = Exclusions.FirstOrDefault(x => x.SteamID == steamID && x.Category == category && x.SubItem == GetGroup(subItem).GroupID) != default;
                }
                else if (GroupDictionary.ContainsKey(subItem))
                {
                    // SubItem is a group. Check exclusion for the group members. If any exist then the exclusion for the group will be considered to exist
                    foreach(ulong groupMember in GroupDictionary[subItem].SteamIDs)
                    {
                        result = result || Exclusions.FirstOrDefault(x => x.SteamID == steamID && x.Category == category && x.SubItem == groupMember) != default;
                    }
                }
                    
            }

            return result;
        }


        // Add a new exclusion to the catalog.
        internal bool AddExclusion(ulong steamID, Enums.ExclusionCategory category, ulong subItem = 0)
        {
            // Exit on a zero steam ID or an unknown category, or if the exclusion already exists
            if (steamID == 0 || category == Enums.ExclusionCategory.Unknown || ExclusionExists(steamID, category, subItem))
            {
                return false;
            }

            // Exit if the subitem is zero while required
            if (subItem == 0 && (category == Enums.ExclusionCategory.RequiredDLC || category == Enums.ExclusionCategory.RequiredMod || 
                category == Enums.ExclusionCategory.NotRequiredMod))
            {
                return false;
            }

            // Add exclusion
            Exclusions.Add(new Exclusion(steamID, category, subItem));

            // If the exclusion is about required mods, then we need to check for groups, and add exclusions for both group and group members
            if (category == Enums.ExclusionCategory.RequiredMod || category == Enums.ExclusionCategory.NotRequiredMod)
            {
                if (IsGroupMember(subItem))
                {
                    // SubItem is a group member. Add exclusion for the group and all members by calling AddExclusion with the group ID. This should not cause a loop.
                    AddExclusion(steamID, category, GetGroup(subItem).GroupID);
                }
                else if (GroupDictionary.ContainsKey(subItem))
                {
                    // SubItem is a group. Add exclusion for all group members. This is done manually here and not by calling AddExclusion again, to avoid a loop.
                    foreach (ulong groupMember in GroupDictionary[subItem].SteamIDs)
                    {
                        if (!ExclusionExists(steamID, category, groupMember))
                        {
                            Exclusions.Add(new Exclusion(steamID, category, groupMember));
                        }
                    }
                }
            }

            return true;
        }


        // Remove an exclusion from the catalog
        internal bool RemoveExclusion(ulong steamID, Enums.ExclusionCategory category, ulong subItem = 0)
        {
            // Get the exclusion
            Exclusion exclusion = Exclusions.FirstOrDefault(x => x.SteamID == steamID && x.Category == category && x.SubItem == subItem);

            // Exit if it doesn't exist
            if (exclusion == default)
            {
                return false;
            }

            // Remove the exclusion if it exists
            bool result = Exclusions.Remove(exclusion);

            // If the exclusion is about required mods, then we need to check for groups, and remove exclusions for both group and group members
            if (result && (category == Enums.ExclusionCategory.RequiredMod || category == Enums.ExclusionCategory.NotRequiredMod))
            {
                if (IsGroupMember(subItem))
                {
                    // SubItem is a group member. Remove exclusion for the group and all members by calling RemoveExclusion with the group ID. This should not cause a loop.
                    RemoveExclusion(steamID, category, GetGroup(subItem).GroupID);
                }
                else if (GroupDictionary.ContainsKey(subItem))
                {
                    // SubItem is a group. Remove exclusion for all group members. This is done manually here and not by calling RemoveExclusion again, to avoid a loop.
                    foreach (ulong groupMember in GroupDictionary[subItem].SteamIDs)
                    {
                        Exclusions.Remove(new Exclusion(steamID, category, groupMember));
                    }
                }
            }

            return result;
        }


        // Validate a catalog, including counting the number of mods and checking the compatible game version
        private bool Validate()
        {
            // Not valid if Version is 0 or UpdateDate is the lowest value
            if ((Version == 0) || (UpdateDate == DateTime.MinValue))
            {
                Logger.Log($"Catalog { VersionString() } has incorrect version or update date ({ Toolkit.DateString(UpdateDate) }).", 
                    Logger.error);

                return false;
            }

            // Not valid if there are no mods
            if (Mods?.Any() != true)
            {
                Logger.Log($"Catalog { VersionString() } contains no mods.", Logger.error); 
                
                return false;
            }

            // Count the number of mods in the catalog
            Count = Mods.Count;

            // Count the number of mods with a manual review in the catalog
            List<Mod> reviewedMods = Mods.FindAll(x => x.ReviewUpdated != DateTime.MinValue);

            ReviewCount = reviewedMods?.Any() == null ? 0 : reviewedMods.Count;

            // If the compatible gameversion for the catalog is unknown, try to convert the string field
            if ((CompatibleGameVersion == null) || (CompatibleGameVersion == GameVersion.Unknown))
            {
                CompatibleGameVersion = Toolkit.ConvertToGameVersion(CompatibleGameVersionString);

                if (CompatibleGameVersion == GameVersion.Unknown) 
                {
                    // Conversion failed, assume it's the mods compatible game version
                    CompatibleGameVersion = ModSettings.compatibleGameVersion;
                }
            }

            // Set the version string, so it matches (again) with the version object
            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            return true;
        }


        // Prepare a catalog for searching
        internal void CreateIndex()
        {
            // Clear the dictionaries
            ModDictionary.Clear();
            GroupDictionary.Clear();
            AuthorIDDictionary.Clear();
            AuthorURLDictionary.Clear();

            // Add mods to the dictionary
            foreach (Mod mod in Mods) 
            { 
                if (ModDictionary.ContainsKey(mod.SteamID))
                {
                    Logger.Log($"Found a duplicate mod steam ID in the catalog: { mod.SteamID }.", Logger.error);
                }
                else
                {
                    ModDictionary.Add(mod.SteamID, mod);
                }
            }

            // Add groups to the dictionary
            foreach (Group group in Groups) 
            {
                if (GroupDictionary.ContainsKey(group.GroupID))
                {
                    Logger.Log($"Found a duplicate group ID in the catalog: { group.GroupID }.", Logger.error);
                }
                else
                {
                    GroupDictionary.Add(group.GroupID, group);
                }
            }

            // Add authors to one or both of the dictionaries
            foreach (Author author in Authors) 
            { 
                if (author.ProfileID != 0)
                {
                    if (AuthorIDDictionary.ContainsKey(author.ProfileID))
                    {
                        Logger.Log($"Found a duplicate author profile ID in the catalog: { author.ProfileID }.", Logger.error);
                    }
                    else
                    {
                        AuthorIDDictionary.Add(author.ProfileID, author);
                    }                    
                }

                if (!string.IsNullOrEmpty(author.CustomURL))
                {
                    if (AuthorURLDictionary.ContainsKey(author.CustomURL))
                    {
                        Logger.Log($"Found a duplicate author custom URL in the catalog: { author.CustomURL }.", Logger.error);
                    }
                    else
                    {
                        AuthorURLDictionary.Add(author.CustomURL, author);
                    }
                }
            }
        }


        // Save a catalog to disk
        internal bool Save(string fullPath)
        {
            try
            {
                // Write serialized catalog to file
                XmlSerializer serializer = new XmlSerializer(typeof(Catalog));

                using (TextWriter writer = new StreamWriter(fullPath))
                {
                    serializer.Serialize(writer, this);
                }

                Logger.Log($"Created catalog { VersionString() } at \"{Toolkit.PrivacyPath(fullPath)}\".");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create catalog at \"{Toolkit.PrivacyPath(fullPath)}\".", Logger.error);

                Logger.Exception(ex);

                return false;
            }
        }


        // Load a catalog from disk and validate it
        internal static Catalog Load(string fullPath)
        {
            // Check if file exists
            if (!File.Exists(fullPath))
            {
                Logger.Log($"Can't load nonexistent catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);

                return null;
            }

            Catalog catalog = new Catalog();

            try
            {
                // Load catalog from disk
                XmlSerializer serializer = new XmlSerializer(typeof(Catalog));

                using (TextReader reader = new StreamReader(fullPath))
                {
                    catalog = (Catalog)serializer.Deserialize(reader);
                }

                // Validate loaded catalog
                if (catalog.Validate())
                {
                    // Valid
                    return catalog;
                }
                else
                {
                    // Invalid, don't return the catalog
                    Logger.Log($"Discarded invalid catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);

                    return null;
                }
            }
            catch (Exception ex)
            {
                // Loading failed
                if (ex.ToString().Contains("There is an error in XML document"))
                {
                    // XML error, log debug exception
                    Logger.Log($"XML error in catalog \"{ Toolkit.PrivacyPath(fullPath) }\". Catalog could not be loaded.", Logger.warning);

                    Logger.Exception(ex, debugOnly: true, duplicateToGameLog: false);
                }
                else
                {
                    // Other error, log exception
                    Logger.Log($"Can't load catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);

                    Logger.Exception(ex);
                }

                return null;
            }
        }
    }
}
