using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using ModChecker.Util;


// Catalog of all known mods, reviewed or not

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

        public List<Compatibility> ModCompatibilities { get; private set; } = new List<Compatibility>();

        public List<ModGroup> ModGroups { get; private set; } = new List<ModGroup>();

        public List<Author> ModAuthors { get; private set; } = new List<Author>();


        // Dictionaries to make searching easier and faster
        [XmlIgnore] public Dictionary<ulong, Mod> ModDictionary { get; private set; } = new Dictionary<ulong, Mod>();
        
        [XmlIgnore] public Dictionary<ulong, ModGroup> ModGroupDictionary { get; private set; } = new Dictionary<ulong, ModGroup>();

        [XmlIgnore] public Dictionary<string, Author> AuthorDictionary { get; private set; } = new Dictionary<string, Author>();


        // The total number of mods in the catalog
        [XmlIgnore] public int Count { get; private set; }
        [XmlIgnore] public int ReviewCount { get; private set; }


        // Object for the active catalog
        [XmlIgnore] internal static Catalog Active { get; private set; }

        // Did we download a catalog already this session
        [XmlIgnore] private static bool downloadedValidCatalog;


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
                       List<Author> modAuthors)
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

            ModCompatibilities = modCompatibilities;

            ModGroups = modGroups;

            ModAuthors = modAuthors;

            ModGroupDictionary = new Dictionary<ulong, ModGroup>();
        }


        // Catalog version string, for reporting and logging
        public string VersionString() => $"{ StructureVersion }.{ Version:D4}";


        // Increase the version, used for the Updater
        internal void NewVersion() => Version++;


        // Add a new mod to the catalog
        internal Mod AddMod(ulong steamID,
                            string name = "",
                            string authorID = "",
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
            // Add the new mod to the list and dictionary
            Mod mod = new Mod(steamID, name, authorID);

            mod.Update(name, authorID, published, updated, archiveURL, sourceURL, compatibleGameVersionString, requiredDLC, requiredMods, 
                onlyNeededFor, succeededBy, alternatives, statuses, note, reviewUpdated, autoReviewUpdated, catalogRemark);

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

            ModGroups.Add(modGroup);

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

            ModCompatibilities.Add(compatibility);

            // Return a reference to the new compatibility
            return compatibility;
        }


        // Add a new author to the catalog
        internal Author AddAuthor(string id,
                                     bool idIsProfile,
                                     string name = "",
                                     DateTime lastSeen = default,
                                     bool retired = false)
        {
            // Add the new group to the list and dictionary
            Author author = new Author(id, idIsProfile, name, lastSeen, retired);

            ModAuthors.Add(author);

            AuthorDictionary.Add(id, author);

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
        private static Catalog Load(string fullPath)
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


        // [Todo 0.2] Move to its own class
        // Load and download catalogs and make the newest the active catalog
        internal static bool InitActive()
        {
            // Load the catalog that was included with the mod
            Catalog bundledCatalog = LoadBundled();

            // Load the downloaded catalog, either a previously downloaded or a newly downloaded catalog, whichever is newest
            Catalog downloadedCatalog = Download();

            // The newest catalog becomes the active catalog; if both are the same version, use the bundled catalog
            Active = Newest(bundledCatalog, downloadedCatalog);

            // Check if we have an active catalog
            if (Active == null)
            {
                return false;
            }

            // Prepare the active catalog for searching
            Active.ModDictionary = new Dictionary<ulong, Mod>();
            Active.ModGroupDictionary = new Dictionary<ulong, ModGroup>();
            Active.AuthorDictionary = new Dictionary<string, Author>();

            foreach (Mod mod in Active.Mods) { Active.ModDictionary.Add(mod.SteamID, mod); }
            foreach (ModGroup group in Active.ModGroups) { Active.ModGroupDictionary.Add(group.GroupID, group); }
            foreach (Author author in Active.ModAuthors) { Active.AuthorDictionary.Add(author.ID, author); }

            return true;
        }


        // Close the active catalog
        internal static void CloseActive()
        {
            // Nullify the active catalog
            Active = null;

            Logger.Log("Catalog closed.");
        }


        // Load bundled catalog
        private static Catalog LoadBundled()
        {
            Catalog catalog = Load(ModSettings.bundledCatalogFullPath);

            if (catalog == null)
            {
                Logger.Log($"Can't load bundled catalog. { ModSettings.pleaseReportText }", Logger.error, duplicateToGameLog: true);
            }
            else
            {
                Logger.Log($"Bundled catalog { catalog.VersionString() } loaded.");
            }

            return catalog;
        }


        // Check for a previously downloaded catalog, download a new catalog and activate the newest of the two
        private static Catalog Download()
        {
            // Object for previously downloaded catalog
            Catalog previousCatalog = null;

            // Check if previously downloaded catalog exists
            if (!File.Exists(ModSettings.downloadedCatalogFullPath))
            {
                // Did not exist
                Logger.Log("No previously downloaded catalog exists. This is expected when the mod has never downloaded a new catalog.");
            }
            else
            {
                // Exists, try to load it
                previousCatalog = Load(ModSettings.downloadedCatalogFullPath);

                if (previousCatalog != null)
                {
                    Logger.Log($"Previously downloaded catalog { previousCatalog.VersionString() } loaded.");
                }
                // Can't be loaded; try to delete it
                else if (Tools.DeleteFile(ModSettings.downloadedCatalogFullPath))
                {
                    Logger.Log("Coud not load previously downloaded catalog. It has been deleted.", Logger.warning);
                }
                else
                {
                    // Can't be deleted
                    Logger.Log("Can't load previously downloaded catalog and it can't be deleted either. " +
                        "This prevents saving a newly downloaded catalog for future sessions.", Logger.error);
                }
            }

            // If we already downloaded this session, exit returning the previously downloaded catalog (could be null if it was manually deleted)
            if (downloadedValidCatalog)
            {
                return previousCatalog;
            }

            // Temporary filename for the newly downloaded catalog
            string newCatalogTemporaryFullPath = ModSettings.downloadedCatalogFullPath + ".part";

            // Delete temporary catalog if it was left over from a previous session; exit if we can't delete it
            if (!Tools.DeleteFile(newCatalogTemporaryFullPath))
            {
                Logger.Log("Partially downloaded catalog still existed from a previous session and couldn't be deleted. This prevents a new download.", Logger.error);

                return previousCatalog;
            }

            // Download new catalog and time it
            Stopwatch timer = Stopwatch.StartNew();

            Exception exception = Tools.Download(ModSettings.catalogURL, newCatalogTemporaryFullPath);

            timer.Stop();

            if (exception == null)
            {
                Logger.Log($"Catalog downloaded in { timer.ElapsedMilliseconds / 1000:F1} seconds from { ModSettings.catalogURL }");
            }
            else
            {
                Logger.Log($"Can't download catalog from { ModSettings.catalogURL }", Logger.warning);

                // Check if the issue is TLS 1.2; only log regular exception if it isn't
                if (exception.ToString().Contains("Security.Protocol.Tls.TlsException: The authentication or decryption has failed"))
                {
                    Logger.Log("It looks like the webserver only supports TLS 1.2 or higher, while Cities: Skylines modding only supports TLS 1.1 and lower.");

                    Logger.Exception(exception, debugOnly: true, duplicateToGameLog: false);
                }
                else
                {
                    Logger.Exception(exception);
                }

                // Delete empty temporary file
                Tools.DeleteFile(newCatalogTemporaryFullPath);

                // Exit
                return previousCatalog;
            }

            // Load newly downloaded catalog
            Catalog newCatalog = Load(newCatalogTemporaryFullPath);

            if (newCatalog == null)
            {
                Logger.Log("Could not load newly downloaded catalog.", Logger.error);
            }
            else
            {
                Logger.Log($"Downloaded catalog { newCatalog.VersionString() } loaded.");

                // Make newly downloaded valid catalog the previously downloaded catalog, if it is newer (only determinend by Version, independent of StructureVersion)
                if ((previousCatalog == null) || (previousCatalog.Version < newCatalog.Version))
                {
                    try
                    {
                        File.Copy(newCatalogTemporaryFullPath, ModSettings.downloadedCatalogFullPath, overwrite: true);

                        // Indicate we downloaded a valid catalog, so we won't do that again this session
                        downloadedValidCatalog = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Can't overwrite previously downloaded catalog. Newly downloaded catalog will not be saved for next session.", Logger.error);

                        Logger.Exception(ex);
                    }
                }
            }

            // Delete temporary file
            Tools.DeleteFile(newCatalogTemporaryFullPath);

            // Return the newest catalog or null if both are null
            // If both catalogs are the same version, the previously downloaded will be returned; this way local edits will be kept until a newer version is downloaded
            return Newest(previousCatalog, newCatalog);
        }


        // Return the newest of two catalogs, or null if both are null; return catalog1 if both are the same version
        private static Catalog Newest(Catalog catalog1, Catalog catalog2)
        {
            if ((catalog1 != null) && (catalog2 != null))
            {
                // Age is only determinend by Version, independent of StructureVersion
                return (catalog1.Version >= catalog2.Version) ? catalog1 : catalog2;
            }
            else
            {
                // Return the catalog that is not null, or null if both are
                return catalog1 ?? catalog2;
            }
        }
    }
}
