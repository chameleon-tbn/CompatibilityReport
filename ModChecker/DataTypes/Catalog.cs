using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using ModChecker.Util;


namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [XmlRoot(ModSettings.xmlRoot)] public class Catalog
    {
        // Catalog structure version (major version); will only change on structural changes in the xml that make it incompatible with a previous structure version
        public uint StructureVersion { get; private set; }

        // Catalog version and date; version always increases and never resets, even when going to a new StructureVersion
        public uint Version { get; private set; }

        public DateTime UpdateDate { get; private set; }

        // Game version this catalog was created for; 'Version' is not serializable, so a converted string is used
        [XmlIgnore] public Version CompatibleGameVersion { get; private set; }
        public string CompatibleGameVersionString { get; private set; }

        // A note about the catalog, displayed in the report header
        public string Note { get; private set; }

        // Intro and footer for the text report
        public string ReportIntroText { get; private set; }

        public string ReportFooterText { get; private set; }

        // The actual mod data in four lists
        public List<Mod> Mods { get; private set; } = new List<Mod>();

        public List<Compatibility> Compatibilities { get; private set; } = new List<Compatibility>();

        public List<ModGroup> ModGroups { get; private set; } = new List<ModGroup>();

        public List<Author> Authors { get; private set; } = new List<Author>();

        // Update exclusions; these prevent certain changes by the auto updater
        public List<Exclusion> Exclusions { get; private set; } = new List<Exclusion>();


        // Dictionaries to make searching easier and faster
        [XmlIgnore] public Dictionary<ulong, Mod> ModDictionary { get; private set; } = new Dictionary<ulong, Mod>();
        
        [XmlIgnore] public Dictionary<ulong, ModGroup> ModGroupDictionary { get; private set; } = new Dictionary<ulong, ModGroup>();

        [XmlIgnore] public Dictionary<ulong, Author> AuthorIDDictionary { get; private set; } = new Dictionary<ulong, Author>();

        [XmlIgnore] public Dictionary<string, Author> AuthorURLDictionary { get; private set; } = new Dictionary<string, Author>();


        // The total number of mods in the catalog
        [XmlIgnore] public int Count { get; private set; }
        [XmlIgnore] public int ReviewCount { get; private set; }


        // Default constructor, used when creating an empty catalog for reading from disk
        public Catalog()
        {
            // Nothing to do here
        }


        // Constructor with 3 to 5 parameters, used when creating a new catalog
        public Catalog(uint version,
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
        public Catalog(uint version, DateTime updateDate, Version compatibleGameVersion, string note, string reportIntroText, string reportFooterText, 
            List<Mod> mods, List<Compatibility> modCompatibilities, List<ModGroup> modGroups, List<Author> modAuthors, List<Exclusion> updateExclusions)
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

            ModGroups = modGroups ?? new List<ModGroup>();

            Authors = modAuthors ?? new List<Author>();

            Exclusions = updateExclusions ?? new List<Exclusion>();
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
        public string VersionString() => $"{ StructureVersion }.{ Version:D4}";


        // Increase the version with a new update date (defaults to now); used for the Updater
        internal void NewVersion(DateTime? updated = null)
        {
            Version++;

            UpdateDate = updated ?? DateTime.Now;
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

            // Add all info to the mod
            mod.Update(name, authorID, authorURL, published, updated, archiveURL, sourceURL, compatibleGameVersionString, requiredDLC, requiredMods,
                requiredAssets, neededFor, succeededBy, alternatives, statuses, note, reviewUpdated, autoReviewUpdated, changeNotes);

            // Return a reference to the new mod
            return mod;
        }


        // Add a new author to the catalog; return the author or null if they couldn't be added
        internal Author AddAuthor(ulong authorID,
                                  string authorURL,
                                  string name,
                                  DateTime lastSeen = default,
                                  bool retired = false,
                                  string changeNotes = "")
        {
            if (AuthorIDDictionary.ContainsKey(authorID) || AuthorURLDictionary.ContainsKey(authorURL ?? "")) 
            {
                // Author already exists
                Logger.Log($"Tried to add an author with a profile ID ({ authorID }) or custom URL ({ authorURL }) that is already used. Author NOT added.", Logger.error);

                return null;
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


        // Add a new mod group to the catalog; return the group, or null if the group couldn't be added; NOTE: a mod can only be in one group
        internal ModGroup AddModGroup(string name, List<ulong> steamIDs)
        {
            // Check if none of the steam IDs is already in a group, or is a group itself
            foreach (ulong steamID in steamIDs ?? new List<ulong>())
            {
                if (steamID >= ModSettings.lowestModGroupID && steamID <= ModSettings.highestModGroupID)
                {
                    Logger.Log($"Could not add a new group ({ name }) because { steamID } is a group and groups can't be nested.", Logger.error);

                    return null;
                }

                if (ModGroups.Find(x => x.SteamIDs.Contains(steamID)) != null)
                {
                    Logger.Log($"Could not add a new group ({ name }) because [Steam ID { steamID }] is already member of another group.", Logger.error);

                    return null;
                }
            }

            // Get a new group ID
            ulong newGroupID;

            if (ModGroupDictionary?.Any() != true)
            {
                // No groups yet, so use the lowest group ID
                newGroupID = ModSettings.lowestModGroupID;
            }
            else
            {
                // Set it one higher than the highest used group ID
                newGroupID = ModGroupDictionary.Keys.Max() + 1;

                if (newGroupID > ModSettings.highestModGroupID)
                {
                    Logger.Log($"Could not add a new group ({ name }) because we ran out of mod group IDs.", Logger.error);

                    return null;
                }
            }

            // Add the new group to the list and dictionary
            ModGroup modGroup = new ModGroup(newGroupID, name, steamIDs);

            ModGroups.Add(modGroup);

            ModGroupDictionary.Add(newGroupID, modGroup);

            // Change required mods in the whole catalog from a group member to the group
            foreach (ulong groupMemberID in modGroup.SteamIDs)
            {
                // Get all mods that have this group member as required mod
                List<Mod> modList = Mods.FindAll(x => x.RequiredMods.Contains(groupMemberID)) ?? new List<Mod>();

                foreach (Mod mod in modList)
                {
                    // Replace the mod ID with the group ID
                    mod.RequiredMods.Remove(groupMemberID);

                    mod.RequiredMods.Add(newGroupID);

                    Logger.Log($"Changed required mod { ModDictionary[groupMemberID].ToString() } to { modGroup.ToString() }, for { mod.ToString(cutOff: false) }.",
                        Logger.debug);
                }
            }

            // Return a reference to the new group
            return modGroup;
        }


        // Add a new compatibility to the catalog; return the compatibility, or null if the compatibility couldn't be added
        internal Compatibility AddCompatibility(ulong steamID1,
                                                ulong steamID2,
                                                List<Enums.CompatibilityStatus> statuses,
                                                string note1 = "",
                                                string note2 = "")
        {
            if ((steamID1 >= ModSettings.lowestModGroupID && steamID1 <= ModSettings.highestModGroupID) 
                || (steamID2 >= ModSettings.lowestModGroupID && steamID2 <= ModSettings.highestModGroupID))
            {
                Logger.Log($"Tried to add a compatibility with a group instead of a mod, which is not supported. Compatibility NOT added.", Logger.error);

                return null;
            }

            if (Compatibilities.Find(x => x.SteamID1 == steamID1 && x.SteamID2 == steamID2) != null)
            {
                // A compatibility already exists between these Steam IDs
                Logger.Log($"Tried to add a compatibility while one already exists between [Steam ID { steamID1 }] and [Steam ID { steamID2 }]. " + 
                    "Compatibility NOT added.", Logger.error);

                return null;
            }

            if (Compatibilities.Find(x => x.SteamID1 == steamID2 && x.SteamID2 == steamID1) != null)
            {
                // A compatibility already exists between these Steam IDs, but reversed
                Logger.Log($"Tried to add a compatibility between [Steam ID { steamID1 }] and [Steam ID { steamID2 }] while a reversed one already exists . " +
                    "Compatibility NOT added.", Logger.error);

                return null;
            }

            // Add a new compatibility to the list
            Compatibility compatibility = new Compatibility(steamID1, steamID2, statuses, note1, note2);

            Compatibilities.Add(compatibility);
            
            // Return a reference to the new compatibility
            return compatibility;
        }


        // Add a new exclusion to the catalog; return the exclusion id
        internal void AddExclusion(string name, ulong steamID, string category, ulong subitem = 0)
        {
            // [Todo 0.3] Needs check for existence before adding
            Exclusions.Add(new Exclusion(steamID, category, subitem));
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
            ModGroupDictionary.Clear();
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

            // Add mod groups to the dictionary
            foreach (ModGroup group in ModGroups) 
            {
                if (ModGroupDictionary.ContainsKey(group.GroupID))
                {
                    Logger.Log($"Found a duplicate mod group ID in the catalog: { group.GroupID }.", Logger.error);
                }
                else
                {
                    ModGroupDictionary.Add(group.GroupID, group);
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
