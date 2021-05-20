using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Xml.Serialization;
using ModChecker.Util;
using static ModChecker.Util.ModSettings;


// Catalog of all known/reviewed mods
// Inspired by the Settings class from Customize It Extended:
// https://github.com/Celisuis/CustomizeItExtended/blob/master/CustomizeItExtended/Settings/CustomizeItExtendedSettings.cs


namespace ModChecker.DataTypes
{
    [XmlRoot(xmlRoot)]
    public class Catalog                            // Needs to be public for XML serialization
    {
        // This will only change on structural changes in the xml that make it incompatible with a previous structure version
        public uint StructureVersion { get; private set; }

        // Catalog version and date; version always increases, even when going to a new StructureVersion
        public uint Version { get; private set; }

        public DateTime UpdateDate { get; private set; }

        // Game version this catalog was created for; 'Version' is not serializable, so a converted string is used
        [XmlIgnore] public Version CompatibleGameVersion { get; private set; }
        public string CompatibleGameVersionString { get; private set; }

        // A note about the catalog, displayed in the report
        public string Note { get; private set; }

        // Intro and footer for the text report
        public string ReportIntroText { get; private set; }

        public string ReportFooterText { get; private set; }

        // The actual data in four lists
        public List<Mod> Mods { get; private set; } = new List<Mod>();

        public List<ModCompatibility> ModCompatibilities { get; private set; } = new List<ModCompatibility>();

        public List<ModGroup> ModGroups { get; private set; } = new List<ModGroup>();

        public List<ModAuthor> ModAuthors { get; private set; } = new List<ModAuthor>();

        // Dictionaries to make searching easier and faster; Mods and Authors dictionaries will not populate automatically when adding any
        [XmlIgnore] public Dictionary<ulong, Mod> ModDictionary;
        
        [XmlIgnore] public Dictionary<ulong, ModGroup> ModGroupDictionary;

        [XmlIgnore] public Dictionary<string, ModAuthor> AuthorDictionary;

        // The total number of mods in the catalog
        [XmlIgnore] public int Count { get; private set; } = 0;
        [XmlIgnore] public int CountReviewed { get; private set; } = 0;

        // Is the catalog valid?
        [XmlIgnore] public bool IsValid { get; private set; } = false;


        // Object for the active catalog
        [XmlIgnore] internal static Catalog Active { get; private set; } = null;

        // Did we download a catalog already this session
        [XmlIgnore] private static bool downloadedValidCatalog = false;

        // ValidationCallback to get rid of "The authentication or decryption has failed." errors when downloading
        // This allows to download from sites that still support TLS 1.1 or worse, but not from sites that only support TLS 1.2+
        // Code copied from https://github.com/bloodypenguin/ChangeLoadingImage/blob/master/ChangeLoadingImage/LoadingExtension.cs by bloodypenguin
        [XmlIgnore] private static readonly RemoteCertificateValidationCallback Callback = (sender, cert, chain, sslPolicyErrors) => true;


        // Default constructor, used when creating an empty catalog (for reading from disk)
        public Catalog()
        {
            // Set to zero for reading a V0 catalog, where the field doesn't exist
            StructureVersion = 0;

            Version = 0;

            UpdateDate = DateTime.MinValue;

            ModGroupDictionary = new Dictionary<ulong, ModGroup>();
        }


        // Constructor with 3 to 5 parameters, used when creating a new catalog
        public Catalog(uint version,
                       DateTime updateDate,
                       string note,
                       string reportIntroText = "",
                       string reportFooterText = "")
        {
            StructureVersion = CurrentCatalogStructureVersion;

            Version = version;

            UpdateDate = updateDate;

            CompatibleGameVersion = GameVersion.Current;

            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            Note = note;

            ReportIntroText = string.IsNullOrEmpty(reportIntroText) ? DefaultIntroText : reportIntroText;

            ReportFooterText = string.IsNullOrEmpty(reportFooterText) ? DefaultFooterText : reportFooterText;

            ModGroupDictionary = new Dictionary<ulong, ModGroup>();
        }


        // Constructor with all parameters, used when converting an old catalog
        public Catalog(uint version,
                       DateTime updateDate,
                       Version compatibleGameVersion,
                       string note,
                       string reportIntroText,
                       string reportFooterText,
                       List<Mod> mods,
                       List<ModCompatibility> modCompatibilities,
                       List<ModGroup> modGroups,
                       List<ModAuthor> modAuthors)
        {
            StructureVersion = CurrentCatalogStructureVersion;

            Version = version;

            UpdateDate = updateDate;

            CompatibleGameVersion = compatibleGameVersion;
            
            CompatibleGameVersionString = (compatibleGameVersion == null) ? "" : compatibleGameVersion.ToString();

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
        public string VersionString() 
        {
            return $"{ StructureVersion }.{ Version:D4}";
        }

        
        // Add a new group to a catalog
        internal ulong AddGroup(List<ulong> steamIDs, string description)
        {
            // Get a new group ID
            ulong newGroupID;

            if (ModGroupDictionary?.Any() != true)
            {
                // No groups yet, so use the lowest group ID
                newGroupID = lowestModGroupID;
            }
            else
            {
                // Set it one higher than the highest used group ID
                newGroupID = ModGroupDictionary.Keys.Max() + 1;
            }            

            if (newGroupID > highestModGroupID)
            {
                Logger.Log("Ran out of mod group IDs. Mod Group could not be added to catalog.", Logger.error);

                return 0;
            }

            // Add the new group to the list and dictionary
            ModGroup modGroup = new ModGroup(newGroupID, steamIDs, description);

            ModGroups.Add(modGroup);

            ModGroupDictionary.Add(newGroupID, modGroup);

            // Return the ID for the new group
            return newGroupID;
        }
        
        
        // Validate a catalog, including counting the number of mods; can't be private because it is used for the example catalog
        internal bool Validate()
        {
            IsValid = false;

            // Not valid if Version is 0 or UpdateDate is the Constructor assigned lowest value
            if ((Version == 0) || (UpdateDate == DateTime.MinValue))
            {
                Logger.Log($"Invalid catalog version { VersionString() } has incorrect version or update date ({ UpdateDate.ToShortDateString() }).", 
                    Logger.error);

                return false;
            }

            // Not valid if there are no mods
            if (Mods?.Any() != true)
            {
                Logger.Log($"Invalid catalog version { VersionString() } contains no mods", Logger.error); 
                
                return false;
            }

            // Get the number of mods in the catalog
            Count = Mods.Count;

            // Get the number of mods with a review in the catalog
            List<Mod> reviewedMods = Mods.FindAll(m => m.ReviewUpdated != null);

            if (reviewedMods?.Any() == null)
            {
                CountReviewed = 0;

                Logger.Log($"Catalog version { VersionString() } contains no reviewed mods.", Logger.debug);
            }
            else
            {
                CountReviewed = reviewedMods.Count;
            }            

            // If the compatible gameversion for the catalog is unknown, try to convert the string field
            if ((CompatibleGameVersion == null) || (CompatibleGameVersion == GameVersion.Unknown))
            {
                CompatibleGameVersion = Tools.ConvertToGameVersion(CompatibleGameVersionString);

                if (CompatibleGameVersion == GameVersion.Unknown) 
                {
                    // Conversion failed, assume it's the mods compatible game version
                    CompatibleGameVersion = ModSettings.CompatibleGameVersion;
                }
            }

            // Set the version string, so it matches (again) with the version object
            CompatibleGameVersionString = CompatibleGameVersion.ToString();

            IsValid = true;

            return true;
        }


        // Load and download catalogs and make the newest the active catalog
        internal static bool InitActive()
        {
            // Load the catalog that was included with the mod
            Catalog bundledCatalog = Bundled();

            // Load the downloaded catalog, either a previously downloaded or a newly downloaded catalog, whichever is newest
            Catalog downloadedCatalog = Download();

            // The newest catalog becomes the active catalog; if both are the same version, use the downloaded catalog
            Active = Newest(downloadedCatalog, bundledCatalog);

            // Check if we have an active catalog
            if (Active == null)
            {
                return false;
            }

            // Prepare the active catalog for searching
            Active.ModDictionary = new Dictionary<ulong, Mod>();
            Active.ModGroupDictionary = new Dictionary<ulong, ModGroup>();
            Active.AuthorDictionary = new Dictionary<string, ModAuthor>();

            foreach (Mod mod in Active.Mods) { Active.ModDictionary.Add(mod.SteamID, mod); }
            foreach (ModGroup group in Active.ModGroups) { Active.ModGroupDictionary.Add(group.GroupID, group); }
            foreach (ModAuthor author in Active.ModAuthors) { Active.AuthorDictionary.Add(author.Tag, author); }

            // Return true if we have a valid active catalog
            return Active.IsValid;
        }


        // Close the active catalog
        internal static void CloseActive()
        {
            if (Active == null)
            {
                Logger.Log("Asked to close active catalog without having one.", Logger.debug);
            }
            else
            {
                // Nullify the active catalog
                Active = null;

                Logger.Log("Catalog closed.");
            }
        }


        // Load bundled catalog
        private static Catalog Bundled()
        {
            Catalog catalog = Load(BundledCatalogFullPath);

            if (catalog == null)
            {
                Logger.Log("Can't load bundled catalog.", Logger.error, gameLog: true);

                return null;
            }

            if (catalog.Validate())
            {
                Logger.Log($"Bundled catalog version { catalog.VersionString() }.");

                return catalog;
            }
            else
            {
                Logger.Log($"Bundled catalog does not validate. { PleaseReportText }", Logger.error, gameLog: true);

                return null;
            }            
        }


        // Check for a previously downloaded catalog, download a new catalog and activate the newest of the two
        private static Catalog Download()
        {
            // Filename and object for previously downloaded catalog
            string previousCatalogFileName = DownloadedCatalogFullPath;

            Catalog previousCatalog = null;            

            // Load and validate previously downloaded catalog if it exists; delete if not valid
            if (File.Exists(previousCatalogFileName))
            {
                previousCatalog = Load(previousCatalogFileName);

                if (previousCatalog == null)
                {
                    Logger.Log("Can't load previously downloaded catalog.", Logger.warning);
                }
                else if (previousCatalog.Validate())
                {
                    Logger.Log($"Previously downloaded catalog version { previousCatalog.VersionString() }.");
                }
                else
                {
                    previousCatalog = null;

                    try
                    {
                        File.Delete(previousCatalogFileName);

                        Logger.Log("Previously downloaded catalog was not valid and has been deleted.", Logger.warning);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Previously downloaded catalog is not valid but can't be deleted. " + 
                            "This prevents saving a newly downloaded catalog for future sessions.", Logger.error);

                        Logger.Exception(ex);                        
                    }
                }
            }
            else
            {
                Logger.Log("No previously downloaded catalog exists. This is expected when the mod has never downloaded a new catalog.");
            }

            // If we already downloaded this session, exit returning the previously downloaded catalog (could be null if it was manually deleted)
            if (downloadedValidCatalog)
            {
                return previousCatalog;
            }

            // Temporary filename for the newly downloaded catalog
            string newCatalogFileName = DownloadedCatalogFullPath + ".part";

            // Delete temporary catalog if it was left over from a previous session; exit if we can't delete it
            if (File.Exists(newCatalogFileName))
            {
                try
                {
                    File.Delete(newCatalogFileName);

                    Logger.Log("Partially downloaded catalog still existed from a previous session but has been deleted now.");
                }
                catch (Exception ex)
                {
                    Logger.Log("Partially downloaded catalog still existed from a previous session and couldn't be deleted. This prevents a new download.", Logger.error);

                    Logger.Exception(ex);

                    return previousCatalog;
                }
            }

            // Activate our callback
            ServicePointManager.ServerCertificateValidationCallback += Callback;

            // Download new catalog; exit if we can't
            using (WebClient webclient = new WebClient())
            {
                try
                {
                    // Start download and time it
                    Stopwatch timer = Stopwatch.StartNew();

                    // Download
                    webclient.DownloadFile(CatalogURL, newCatalogFileName);

                    // Get and log the elapsed time in seconds, rounded to one decimal
                    timer.Stop();

                    Logger.Log($"Catalog downloaded in { timer.ElapsedMilliseconds / 1000:F1} seconds from { CatalogURL }");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Can't download catalog from { CatalogURL }",Logger.warning, gameLog: true);

                    // Check if the issue is TLS 1.2; only log regular exception if it isn't
                    if (ex.ToString().Contains("Security.Protocol.Tls.TlsException: The authentication or decryption has failed"))
                    {
                        Logger.Log("It looks like the webserver only supports TLS 1.2 or higher, while Cities: Skylines modding only supports TLS 1.1 and lower.", gameLog: true);

                        Logger.Exception(ex, debugOnly: true, gameLog: false);
                    }
                    else
                    {
                        Logger.Exception(ex);
                    }

                    // Delete empty temporary file
                    try
                    {
                        File.Delete(newCatalogFileName);
                    }
                    catch (Exception ex2)
                    {
                        Logger.Log("Can't delete temporary catalog file from failed download.", Logger.error);

                        Logger.Exception(ex2);
                    }

                    // Deactivate our callback
                    ServicePointManager.ServerCertificateValidationCallback -= Callback;

                    // Exit download method
                    return previousCatalog;
                }
            }

            // Deactivate our callback
            ServicePointManager.ServerCertificateValidationCallback -= Callback;

            // Load and validate newly downloaded catalog
            Catalog newCatalog = Load(newCatalogFileName);

            if (newCatalog == null)
            {
                Logger.Log("Could not load newly downloaded catalog.", Logger.error);
            }
            else if (newCatalog.Validate())
            {
                Logger.Log($"Downloaded catalog version { newCatalog.VersionString() }.");
            }
            else
            {
                newCatalog = null;

                Logger.Log($"Downloaded catalog is not valid. It will be deleted.", Logger.error);
            }

            // Make newly downloaded valid catalog the previously downloaded catalog, if it is newer
            if (newCatalog != null)
            {
                // Age is only determinend by Version, independent of StructureVersion
                if ((previousCatalog == null) || (previousCatalog.Version < newCatalog.Version))
                {
                    try
                    {
                        File.Copy(newCatalogFileName, previousCatalogFileName, overwrite: true);

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

            // Delete temporary file for newly downloaded catalog
            try
            {
                File.Delete(newCatalogFileName);
            }
            catch (Exception ex)
            {
                Logger.Log("Can't delete temporary file from download. This might prevent downloading a new catalog in the future.", Logger.error);

                Logger.Exception(ex);
            }

            // return the newest catalog or null if both are null
            // if both catalogs are the same version, the previously downloaded will be returned; this way local edits will be kept until a newer version is downloaded
            return Newest(previousCatalog, newCatalog);
        }


        // Return the newest of two catalogs, or null if both are null; return catalog1 if both are the same version
        private static Catalog Newest(Catalog catalog1, Catalog catalog2)
        {
            if ((catalog1 != null) && (catalog2 != null))
            {
                // Age is only determinend by Version, independent of StructureVersion
                return (catalog1.Version > catalog2.Version) ? catalog1 : catalog2;
            }
            else if (catalog1 != null)
            {
                return catalog1;
            }
            else
            {
                return catalog2;
            }
        }
        
        
        // Load a catalog from disk
        private static Catalog Load(string fullPath)
        {
            if (File.Exists(fullPath))
            {
                Catalog catalog = new Catalog();

                try
                {
                    // Load and deserialize catalog from disk
                    XmlSerializer serializer = new XmlSerializer(typeof(Catalog));

                    using (TextReader reader = new StreamReader(fullPath))
                    {
                        catalog = (Catalog)serializer.Deserialize(reader);
                    }

                    return catalog;
                }
                catch (Exception ex)
                {
                    if (ex.ToString().Contains("There is an error in XML document")) 
                    {
                        Logger.Log($"Can't load catalog \"{ Tools.PrivacyPath(fullPath) }\". The XML has an error.");

                        Logger.Exception(ex, debugOnly: true, gameLog: false);
                    }
                    else
                    {
                        Logger.Log($"Can't load catalog \"{ Tools.PrivacyPath(fullPath) }\".");

                        Logger.Exception(ex);
                    }

                    return null;
                }
            }
            else
            {
                Logger.Log($"Can't load nonexistent catalog \"{ Tools.PrivacyPath(fullPath) }\".");

                return null;
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

                Logger.Log($"Created catalog version { VersionString() } at \"{Tools.PrivacyPath(fullPath)}\".");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create catalog at \"{Tools.PrivacyPath(fullPath)}\".", Logger.error);

                Logger.Exception(ex);

                return false;
            }
        }
    }
}
