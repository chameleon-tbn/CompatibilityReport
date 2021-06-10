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

        // Catalog version and date; version only increases and never resets, even when going to a new StructureVersion
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

        // The actual data in four lists
        public List<Mod> Mods { get; private set; } = new List<Mod>();

        public List<Compatibility> Compatibilities { get; private set; } = new List<Compatibility>();

        public List<ModGroup> Groups { get; private set; } = new List<ModGroup>();

        public List<Author> Authors { get; private set; } = new List<Author>();

        // Auto updater exclusions
        public List<UpdateExclusion> UpdateExclusions { get; private set; } = new List<UpdateExclusion>();


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

            Note = note;

            ReportIntroText = reportIntroText ?? ModSettings.defaultIntroText;

            ReportFooterText = reportFooterText ?? ModSettings.defaultFooterText;
        }


        // Constructor with all parameters, used when converting an old catalog
        public Catalog(uint version,
                       DateTime updateDate,
                       Version compatibleGameVersion,
                       string note,
                       string reportIntroText,
                       string reportFooterText,
                       List<Mod> mods,
                       List<Compatibility> modCompatibilities,
                       List<ModGroup> modGroups,
                       List<Author> modAuthors,
                       List<UpdateExclusion> updateExclusions)
        {
            StructureVersion = ModSettings.currentCatalogStructureVersion;

            Version = version;

            UpdateDate = updateDate;

            CompatibleGameVersion = compatibleGameVersion;
            
            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            Note = note;

            ReportIntroText = reportIntroText;

            ReportFooterText = reportFooterText;

            Mods = mods;

            Compatibilities = modCompatibilities;

            Groups = modGroups;

            Authors = modAuthors;

            UpdateExclusions = updateExclusions;
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
                            List<ulong> onlyNeededFor = null,
                            List<ulong> succeededBy = null,
                            List<ulong> alternatives = null,
                            List<ulong> requiredAssets = null,
                            List<Enums.ModStatus> statuses = null,
                            string note = null,
                            DateTime? reviewUpdated = null,
                            DateTime? autoReviewUpdated = null,
                            string catalogRemark = null)
        {
            // Add the new mod to the list and dictionary
            Mod mod = new Mod(steamID, name, authorID, authorURL);

            mod.Update(name, authorID, authorURL, published, updated, archiveURL, sourceURL, compatibleGameVersionString, requiredDLC, requiredMods, 
                onlyNeededFor, succeededBy, alternatives, requiredAssets, statuses, note, reviewUpdated, autoReviewUpdated, catalogRemark);

            Mods.Add(mod);

            ModDictionary.Add(steamID, mod);

            // Return a reference to the new mod
            return mod;
        }
        
        
        // Add a new mod group to the catalog
        internal ModGroup AddGroup(List<ulong> steamIDs,
                                   string description)
        {
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
                    Logger.Log("Ran out of mod group IDs. Mod Group could not be added to catalog.", Logger.error);

                    return null;
                }
            }

            // Add the new group to the list and dictionary
            ModGroup modGroup = new ModGroup(newGroupID, steamIDs, description);

            Groups.Add(modGroup);

            ModGroupDictionary.Add(newGroupID, modGroup);

            // Return a reference to the new group
            return modGroup;
        }


        // Add a new compatibility to the catalog
        internal Compatibility AddCompatibility(ulong steamID1,
                                                ulong steamID2,
                                                List<Enums.CompatibilityStatus> statuses,
                                                string note1 = "",
                                                string note2 = "")
        {
            // Add the new compatibility to the list
            Compatibility compatibility = new Compatibility(steamID1, steamID2, statuses, note1, note2);

            Compatibilities.Add(compatibility);

            // Return a reference to the new compatibility
            return compatibility;
        }


        // Add a new author to the catalog
        internal Author AddAuthor(ulong authorID,
                                  string authorURL, 
                                  string name = "",
                                  DateTime lastSeen = default,
                                  bool retired = false,
                                  string catalogRemark = "")
        {
            // Add the new author to the list
            Author author = new Author(authorID, authorURL, name, lastSeen, retired, catalogRemark);

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


        // Validate a catalog, including counting the number of mods and checking the compatible game version
        private bool Validate()
        {
            // Not valid if Version is 0 or UpdateDate is the lowest value
            if ((Version == 0) || (UpdateDate == DateTime.MinValue))
            {
                Logger.Log($"Catalog { VersionString() } has incorrect version or update date ({ UpdateDate.ToShortDateString() }).", 
                    Logger.error);

                return false;
            }

            // Not valid if there are no mods
            if (Mods?.Any() != true)
            {
                Logger.Log($"Catalog { VersionString() } contains no mods.", Logger.error); 
                
                return false;
            }

            // Get the number of mods in the catalog
            Count = Mods.Count;

            // Get the number of mods with a (manual) review in the catalog
            List<Mod> reviewedMods = Mods.FindAll(m => m.ReviewUpdated != DateTime.MinValue);

            if (reviewedMods?.Any() == null)
            {
                ReviewCount = 0;

                Logger.Log($"Catalog { VersionString() } contains no reviewed mods.", Logger.debug);
            }
            else
            {
                ReviewCount = reviewedMods.Count;
            }            

            // If the compatible gameversion for the catalog is unknown, try to convert the string field
            if ((CompatibleGameVersion == null) || (CompatibleGameVersion == GameVersion.Unknown))
            {
                CompatibleGameVersion = Tools.ConvertToGameVersion(CompatibleGameVersionString);

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
            ModDictionary = new Dictionary<ulong, Mod>();
            ModGroupDictionary = new Dictionary<ulong, ModGroup>();
            AuthorIDDictionary = new Dictionary<ulong, Author>();
            AuthorURLDictionary = new Dictionary<string, Author>();

            foreach (Mod mod in Mods) 
            { 
                ModDictionary.Add(mod.SteamID, mod); 
            }
            
            foreach (ModGroup group in Groups) 
            { 
                ModGroupDictionary.Add(group.GroupID, group); 
            }
            
            foreach (Author author in Authors) 
            { 
                // Add author to one or both of the dictionaries
                if (author.ProfileID != 0)
                {
                    AuthorIDDictionary.Add(author.ProfileID, author);
                }

                if (!string.IsNullOrEmpty(author.CustomURL))
                {
                    AuthorURLDictionary.Add(author.CustomURL, author);
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

                Logger.Log($"Created catalog { VersionString() } at \"{Tools.PrivacyPath(fullPath)}\".");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create catalog at \"{Tools.PrivacyPath(fullPath)}\".", Logger.error);

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
                Logger.Log($"Can't load nonexistent catalog \"{ Tools.PrivacyPath(fullPath) }\".", Logger.warning);

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
                    Logger.Log($"Loaded and discarded invalid catalog \"{ Tools.PrivacyPath(fullPath) }\".", Logger.warning);

                    return null;
                }
            }
            catch (Exception ex)
            {
                // Loading failed
                if (ex.ToString().Contains("There is an error in XML document"))
                {
                    // XML error, log debug exception
                    Logger.Log($"XML error in catalog \"{ Tools.PrivacyPath(fullPath) }\". Catalog could not be loaded.", Logger.warning);

                    Logger.Exception(ex, debugOnly: true, duplicateToGameLog: false);
                }
                else
                {
                    // Other error, log exception
                    Logger.Log($"Can't load catalog \"{ Tools.PrivacyPath(fullPath) }\".", Logger.warning);

                    Logger.Exception(ex);
                }

                return null;
            }
        }
    }
}
