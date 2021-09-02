using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        [XmlIgnore] public Version CompatibleGameVersion { get; private set; }
        public string CompatibleGameVersionString { get; private set; }

        // A note about the catalog, displayed in the report header
        public string Note { get; private set; }

        // Intro and footer for the text report
        public string ReportHeaderText { get; private set; }

        public string ReportFooterText { get; private set; }

        // The actual mod data in four lists
        public List<Mod> Mods { get; private set; } = new List<Mod>();

        public List<Compatibility> Compatibilities { get; private set; } = new List<Compatibility>();

        public List<Group> Groups { get; private set; } = new List<Group>();

        public List<Author> Authors { get; private set; } = new List<Author>();

        // Assets that show up as required items; listed to see the difference between a required asset and a required mod we don't know (unlisted or removed mod)
        [XmlArrayItem("SteamID")] public List<ulong> RequiredAssets { get; private set; } = new List<ulong>();


        // Dictionaries to make searching easier and faster
        [XmlIgnore] internal Dictionary<ulong, Mod> ModDictionary { get; private set; } = new Dictionary<ulong, Mod>();
        
        [XmlIgnore] internal Dictionary<ulong, Group> GroupDictionary { get; private set; } = new Dictionary<ulong, Group>();

        [XmlIgnore] internal Dictionary<ulong, Author> AuthorIDDictionary { get; private set; } = new Dictionary<ulong, Author>();

        [XmlIgnore] internal Dictionary<string, Author> AuthorURLDictionary { get; private set; } = new Dictionary<string, Author>();


        // The total number of mods in the catalog
        [XmlIgnore] internal int ModCount { get; private set; }
        [XmlIgnore] internal int ReviewedModCount { get; private set; }


        // Instance for the active catalog  [Todo 0.4] Can we get rid of this?
        [XmlIgnore] internal static Catalog Active { get; private set; }

        // Did we download a catalog already this session
        [XmlIgnore] private static bool downloadedThisSession;


        // Default constructor, used when creating an empty catalog for reading from disk
        public Catalog()
        {
            // Nothing to do here
        }


        // Constructor with 1 parameter, used when creating a new catalog
        internal Catalog(uint version)
        {
            StructureVersion = ModSettings.currentCatalogStructureVersion;

            Version = version;

            CompatibleGameVersion = Toolkit.UnknownVersion;

            CompatibleGameVersionString = CompatibleGameVersion.ToString();
        }


        // Catalog version string, for reporting and logging
        internal string VersionString()
        {
            return $"{ StructureVersion }.{ Version:D4}";
        }


        // Increase the version with a new update date
        internal void NewVersion(DateTime updated)
        {
            Version++;

            UpdateDate = updated;
        }


        // Update the catalog, only supplied parameters are updated; the lists can be updated directly, version and gameversion have their own update method
        internal void Update(string note = null, string reportHeaderText = null, string reportFooterText = null)
        {
            Note = note ?? Note;

            ReportHeaderText = reportHeaderText ?? ReportHeaderText;

            ReportFooterText = reportFooterText ?? ReportFooterText;
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


        // Check if the ID is a valid existing or non-existing ID   [Todo 0.4] Change 'true' default values to false default values
        internal bool IsValidID(ulong steamID, bool allowGroup = false, bool allowBuiltin = true, bool shouldExist = true)
        {
            // Check if the ID is a valid mod or group ID
            bool valid = steamID > ModSettings.highestFakeID ||
                         (allowBuiltin && ModSettings.BuiltinMods.ContainsValue(steamID)) ||
                         (allowGroup && steamID >= ModSettings.lowestGroupID && steamID <= ModSettings.highestGroupID);

            // Check if the mod or group already exists
            bool exists = ModDictionary.ContainsKey(steamID) || GroupDictionary.ContainsKey(steamID);

            return valid && (shouldExist ? exists : !exists);
        }


        // Add or update a catalog mod.
        internal Mod GetOrAddMod(ulong steamID)
        {
            Mod mod;

            if (!ModDictionary.ContainsKey(steamID))
            {
                // Mod doesn't exist yet
                mod = new Mod(steamID);

                mod.Update(addedThisSession: true);

                // Add the mod to the list and dictionary
                Mods.Add(mod);

                ModDictionary.Add(steamID, mod);
            }
            else
            {
                // This mod already exists; update the existing one
                mod = ModDictionary[steamID];
            }

            // Return a reference to the new mod
            return mod;
        }


        // Get the group this mod is a member of, if any
        internal Group GetGroup(ulong steamID)
        {
            return Groups.FirstOrDefault(x => x.GroupMembers.Contains(steamID));
        }


        // Check if a mod is a group member
        internal bool IsGroupMember(ulong steamID)
        {
            return GetGroup(steamID) != default;
        }


        // Add a new group to the catalog; return the group, or null if the group couldn't be added
        internal Group AddGroup(string name)
        {
            // Get a new group ID, either the default lowest id if we have no groups yet, or the highest current group ID + 1
            ulong newGroupID = GroupDictionary?.Any() != true ? ModSettings.lowestGroupID : GroupDictionary.Keys.Max() + 1;

            if (newGroupID > ModSettings.highestGroupID)
            {
                Logger.Log($"Could not add a new group ({ name }) because we ran out of group IDs.", Logger.error);

                return null;
            }

            // Add the new group to the list and dictionary
            Group newGroup = new Group(newGroupID, name);

            Groups.Add(newGroup);

            GroupDictionary.Add(newGroup.GroupID, newGroup);

            // Return a reference to the new group
            return newGroup;
        }


        // Add a group as required mod for all mods that have the given group member as required mod    [Todo 0.4] Move to CatalogUpdater; Name is not very descriptive
        internal void AddRequiredGroup(ulong requiredModID)
        {
            Group requiredGroup = GetGroup(requiredModID);

            // Exit if this mod is not in a group
            if (requiredGroup == default)
            {
                return;
            }

            // Get all mods that have this required mod
            List<Mod> modList = Mods.FindAll(x => x.RequiredMods.Contains(requiredModID));

            foreach (Mod mod in modList)
            {
                // Add the group ID
                if (!mod.RequiredMods.Contains(requiredGroup.GroupID))
                {
                    mod.RequiredMods.Add(requiredGroup.GroupID);

                    Logger.UpdaterLog($"Added { requiredGroup.ToString() } as required mod for { mod.ToString() }.");
                }
            }
        }


        // Get the author, or return null when the author doesn't exist
        internal Author GetAuthor(ulong authorID, string authorURL)
        {
            if (authorID != 0 && AuthorIDDictionary.ContainsKey(authorID))
            {
                return AuthorIDDictionary[authorID];
            }
            else if (!string.IsNullOrEmpty(authorURL) && AuthorURLDictionary.ContainsKey(authorURL))
            {
                return AuthorURLDictionary[authorURL];
            }
            else
            {
                return null;
            }
        }


        // Add a new author to the catalog; return the author or null if they couldn't be added
        internal Author AddAuthor(ulong authorID, string authorURL, string name)
        {
            if (AuthorIDDictionary.ContainsKey(authorID) || AuthorURLDictionary.ContainsKey(authorURL ?? ""))
            {
                // Author already exists
                Logger.Log($"Tried to add an author with a profile ID ({ authorID }) or custom URL ({ authorURL }) that is already used. Author NOT added.", Logger.error);

                return null;
            }

            // Create a new author
            Author author = new Author(authorID, authorURL, name);

            author.Update(addedThisSession: true);

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


        // Add an exclusion for a required mod, including for group and group members
        private void AddExclusionForRequiredMods(Mod mod, ulong requiredID)
        {
            // Add exclusion
            mod.AddExclusionForRequiredMods(requiredID);

            if (IsGroupMember(requiredID))
            {
                // SubItem is a group member. Add exclusion for the group and all members by calling AddExclusion with the group ID. Should not cause an infinite loop.
                AddExclusionForRequiredMods(mod, GetGroup(requiredID).GroupID);
            }
            else if (GroupDictionary.ContainsKey(requiredID))
            {
                // SubItem is a group. Add exclusion for all group members. This is done manually here and not by calling AddExclusion again, to avoid an infinite loop.
                foreach (ulong groupMember in GroupDictionary[requiredID].GroupMembers)
                {
                    mod.AddExclusionForRequiredMods(groupMember);
                }
            }
        }


        // Remove an exclusion from the catalog
        private void RemoveExclusionForRequiredMods(Mod mod, ulong requiredID)
        {
            // Remove exclusion
            bool removedSuccesful = mod.ExclusionForRequiredMods.Remove(requiredID);

            // If the exclusion is about a required mod, then we need to check for groups, and remove exclusions for both group and group members
            if (removedSuccesful && IsGroupMember(requiredID))
            {
                // SubItem is a group member. Remove exclusion for the group and all members by calling RemoveExclusion with the group ID. This should not cause a loop.
                RemoveExclusionForRequiredMods(mod, GetGroup(requiredID).GroupID);
            }
            else if (removedSuccesful && GroupDictionary.ContainsKey(requiredID))
            {
                // SubItem is a group. Remove exclusion for all group members. This is done manually here and not by calling RemoveExclusion again, to avoid a loop.
                foreach (ulong groupMember in GroupDictionary[requiredID].GroupMembers)
                {
                    mod.ExclusionForRequiredMods.Remove(groupMember);
                }
            }
        }


        // Prepare a catalog for searching  [Todo 0.4] combine with Validate()
        private void CreateIndex()
        {
            // Count the number of mods in the catalog
            ModCount = Mods.Count;

            // Count the number of mods with a manual review in the catalog
            List<Mod> reviewedMods = Mods.FindAll(x => x.ReviewDate != default);

            ReviewedModCount = reviewedMods?.Any() != true ? 0 : reviewedMods.Count;

            // If the compatible gameversion for the catalog is unknown, try to convert the string field
            if ((CompatibleGameVersion == null) || (CompatibleGameVersion == Toolkit.UnknownVersion))
            {
                CompatibleGameVersion = Toolkit.ConvertToGameVersion(CompatibleGameVersionString);

                if (CompatibleGameVersion == Toolkit.UnknownVersion)
                {
                    // Conversion failed, assume it's the mods compatible game version
                    CompatibleGameVersion = ModSettings.compatibleGameVersion;
                }
            }

            // Set the version string, so it matches (again) with the version object
            CompatibleGameVersionString = CompatibleGameVersion.ToString();

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
        private static Catalog Load(string fullPath)
        {
            // Check if file exists
            if (!File.Exists(fullPath))
            {
                Logger.Log($"Can't load nonexistent catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);

                return null;
            }

            Catalog loadedCatalog = new Catalog();

            try
            {
                // Load catalog from disk
                XmlSerializer serializer = new XmlSerializer(typeof(Catalog));

                using (TextReader reader = new StreamReader(fullPath))
                {
                    loadedCatalog = (Catalog)serializer.Deserialize(reader);
                }

                // Validate loaded catalog
                if (loadedCatalog.Version == 0 || loadedCatalog.UpdateDate == default)
                {
                    Logger.Log($"Discarded invalid catalog \"{ Toolkit.PrivacyPath(fullPath) }\". It has an incorrect version ({ loadedCatalog.VersionString() }) " +
                        $"or date ({ Toolkit.DateString(loadedCatalog.UpdateDate) }).", Logger.error);

                    return null;
                }
                
                return loadedCatalog;
            }
            catch (Exception ex)
            {
                // Loading failed
                if (ex.ToString().Contains("There is an error in XML document") || ex.ToString().Contains("Document element did not appear"))
                {
                    // XML error
                    Logger.Log($"XML error in catalog \"{ Toolkit.PrivacyPath(fullPath) }\". Catalog could not be loaded.", Logger.warning);
                }
                else
                {
                    // Other error
                    Logger.Log($"Can't load catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);

                    Logger.Exception(ex);
                }

                return null;
            }
        }


        // Load and download catalogs and make the newest the active catalog
        internal static Catalog InitActive()
        {
            // Skip if we already have an active catalog
            if (Active != null)
            {
                return Active;
            }

            Catalog downloadedCatalog = Download();

            // Download catalog should always be the same or a higher version than both the previous download and the bundled catalog
            Catalog previouslyDownloadedCatalog = downloadedCatalog == null ? LoadPreviouslyDownloaded() : null;

            Catalog bundledCatalog = downloadedCatalog == null && previouslyDownloadedCatalog == null ? LoadBundled() : null;

            // Always try to load the Updater catalog, since that could be newer than the download
            Catalog updatedCatalog = LoadUpdaterCatalog();

            // The newest catalog becomes the active catalog
            Active = Newest(Newest(downloadedCatalog, bundledCatalog), Newest(previouslyDownloadedCatalog, updatedCatalog));

            if (Active != null)
            {
                // Prepare the active catalog for searching
                Active.CreateIndex();

                // Log catalog details
                Logger.Log($"Using catalog { Active.VersionString() }, created on { Active.UpdateDate.ToLongDateString() }. " +
                    $"Catalog contains { Active.ReviewedModCount } reviewed mods and { Active.ModCount - Active.ReviewedModCount } mods with basic information.",
                    duplicateToGameLog: true);
            }

            return Active;
        }


        // Close the active catalog     [Todo 0.4] No longer needed if we get rid of Active here
        internal static void CloseActive()
        {
            // Nullify the active catalog
            Active = null;

            Logger.Log("Catalog closed.");
        }


        // Load bundled catalog
        private static Catalog LoadBundled()
        {
            Catalog bundledCatalog = Load(ModSettings.bundledCatalogFullPath);

            if (bundledCatalog == null)
            {
                Logger.Log($"Can't load bundled catalog. { ModSettings.pleaseReportText }", Logger.error, duplicateToGameLog: true);
            }
            else
            {
                Logger.Log($"Bundled catalog is version { bundledCatalog.VersionString() }.");
            }

            return bundledCatalog;
        }


        // Load the previously downloaded catalog if it exists
        private static Catalog LoadPreviouslyDownloaded()
        {
            // Check if previously downloaded catalog exists
            if (!File.Exists(ModSettings.downloadedCatalogFullPath))
            {
                Logger.Log("No previously downloaded catalog exists. This is expected when the mod has never downloaded a new catalog.");

                return null;
            }

            // Try to load it
            Catalog previouslyDownloadedCatalog = Load(ModSettings.downloadedCatalogFullPath);

            if (previouslyDownloadedCatalog != null)
            {
                Logger.Log($"Previously downloaded catalog is version { previouslyDownloadedCatalog.VersionString() }.");
            }
            // Can't be loaded; try to delete it
            else if (Toolkit.DeleteFile(ModSettings.downloadedCatalogFullPath))
            {
                Logger.Log("Coud not load previously downloaded catalog. It has been deleted.", Logger.warning);
            }
            else
            {
                Logger.Log("Can't load previously downloaded catalog and it can't be deleted either. " +
                    "This prevents saving a newly downloaded catalog for future sessions.", Logger.error);
            }

            return previouslyDownloadedCatalog;
        }


        // Download a new catalog
        private static Catalog Download()
        {
            // Exit if we already downloaded this session
            if (downloadedThisSession)
            {
                return null;
            }

            // Indicate we downloaded (or at least tried), so we won't do that again this session
            downloadedThisSession = true;

            // Temporary filename for the newly downloaded catalog
            string downloadedCatalogTemporaryFullPath = ModSettings.downloadedCatalogFullPath + ".part";

            // Delete temporary catalog if it was left over from a previous session; exit if we can't delete it
            if (!Toolkit.DeleteFile(downloadedCatalogTemporaryFullPath))
            {
                Logger.Log("Partially downloaded catalog still exists from a previous session and can't be deleted. This prevents a new download.", Logger.error);

                return null;
            }

            // Download new catalog and time it
            Stopwatch timer = Stopwatch.StartNew();

            Exception ex = Toolkit.Download(ModSettings.catalogURL, downloadedCatalogTemporaryFullPath);

            if (ex != null)
            {
                Logger.Log($"Can't download catalog from { ModSettings.catalogURL }", Logger.warning);

                // Check if the issue is TLS 1.2; only log regular exception if it isn't
                if (ex.ToString().Contains("Security.Protocol.Tls.TlsException: The authentication or decryption has failed"))
                {
                    Logger.Log("It looks like the webserver only supports TLS 1.2 or higher, while Cities: Skylines modding only supports TLS 1.1 and lower.");

                    Logger.Exception(ex, hideFromGameLog: true, debugOnly: true);
                }
                else
                {
                    Logger.Exception(ex);
                }

                // Delete empty temporary file and exit
                Toolkit.DeleteFile(downloadedCatalogTemporaryFullPath);

                return null;
            }

            // Log elapsed time
            timer.Stop();

            Logger.Log($"Catalog downloaded in { Toolkit.ElapsedTime(timer.ElapsedMilliseconds) } from { ModSettings.catalogURL }");

            // Load newly downloaded catalog
            Catalog downloadedCatalog = Load(downloadedCatalogTemporaryFullPath);

            if (downloadedCatalog == null)
            {
                Logger.Log("Could not load newly downloaded catalog.", Logger.error);
            }
            else
            {
                Logger.Log($"Downloaded catalog is version { downloadedCatalog.VersionString() }.");

                // Copy the temporary file over the previously downloaded catalog
                Toolkit.CopyFile(downloadedCatalogTemporaryFullPath, ModSettings.downloadedCatalogFullPath);
            }

            // Delete temporary file
            Toolkit.DeleteFile(downloadedCatalogTemporaryFullPath);

            return downloadedCatalog;
        }


        // Load updated catalog, if the updater is enabled
        private static Catalog LoadUpdaterCatalog()
        {
            if (!ModSettings.UpdaterEnabled)
            {
                return null;
            }

            // Get all catalog filenames
            string[] files = Directory.GetFiles(ModSettings.updaterPath, $"{ ModSettings.internalName }_Catalog*.xml");

            // Silently exit if no updater catalogs are found
            if (files.Length == 0)
            {
                return null;
            }

            // Sort the filenames
            Array.Sort(files);

            // Load the last updated catalog
            Catalog catalog = Catalog.Load(files[files.Length - 1]);

            if (catalog == null)
            {
                Logger.Log("Can't load updater catalog.", Logger.warning);
            }
            else
            {
                Logger.Log($"Updater catalog is version { catalog.VersionString() }.");
            }

            return catalog;
        }


        // Return the newest of two catalogs; return catalog1 if both are the same version
        private static Catalog Newest(Catalog catalog1, Catalog catalog2)
        {
            if (catalog1 == null || catalog2 == null)
            {
                // Return the catalog that is not null, or null if both are
                return catalog1 ?? catalog2;
            }
            else
            {
                // Age is only determinend by Version, independent of StructureVersion
                return (catalog1.Version >= catalog2.Version) ? catalog1 : catalog2;
            }
        }
    }
}
