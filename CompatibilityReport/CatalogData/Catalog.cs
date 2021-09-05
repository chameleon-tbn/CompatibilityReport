using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using CompatibilityReport.Util;

namespace CompatibilityReport.CatalogData
{
    [Serializable][XmlRoot(ModSettings.xmlRoot)]
    public class Catalog
    {
        // Catalog structure version will change on structural changes that make the xml incompatible. Version will not reset on a new StructureVersion.
        public int StructureVersion { get; private set; } = ModSettings.currentCatalogStructureVersion;
        public int Version { get; private set; }
        public DateTime UpdateDate { get; private set; }

        // Game version this catalog was created for. 'Version' is not serializable, so a converted string is used.
        public string GameVersionString { get; private set; } = Toolkit.UnknownVersion.ToString();

        // A note about the catalog, displayed in the report, and the header and footer text for the report.
        public string Note { get; private set; }
        public string ReportHeaderText { get; private set; }
        public string ReportFooterText { get; private set; }

        // The actual mod data in four lists.
        public List<Mod> Mods { get; private set; } = new List<Mod>();
        public List<Compatibility> Compatibilities { get; private set; } = new List<Compatibility>();
        public List<Group> Groups { get; private set; } = new List<Group>();
        public List<Author> Authors { get; private set; } = new List<Author>();

        // Assets that show up as required items. This is used to distinguish between a required asset and an unknown required mod.
        [XmlArrayItem("SteamID")] public List<ulong> RequiredAssets { get; private set; } = new List<ulong>();

        // Dictionaries for faster lookup.
        [XmlIgnore] public Dictionary<ulong, Mod> ModIndex { get; private set; } = new Dictionary<ulong, Mod>();
        [XmlIgnore] public Dictionary<ulong, Group> GroupIndex { get; private set; } = new Dictionary<ulong, Group>();
        [XmlIgnore] public Dictionary<ulong, Author> AuthorIDIndex { get; private set; } = new Dictionary<ulong, Author>();
        [XmlIgnore] public Dictionary<string, Author> AuthorUrlIndex { get; private set; } = new Dictionary<string, Author>();
        [XmlIgnore] public List<ulong> SubscriptionIDIndex { get; private set; } = new List<ulong>();
        [XmlIgnore] public Dictionary<string, List<ulong>> SubscriptionNameIndex { get; private set; } = new Dictionary<string, List<ulong>>();
        [XmlIgnore] public Dictionary<ulong, List<Compatibility>> SubscriptionCompatibilityIndex { get; private set; } = new Dictionary<ulong, List<Compatibility>>();

        [XmlIgnore] public int ReviewedModCount { get; private set; }
        [XmlIgnore] public int ReviewedSubscriptionCount { get; private set; }

        private static bool s_downloadedThisSession;


        // Return a formatted catalog version string.
        public string VersionString()
        {
            return $"{ StructureVersion }.{ Version:D4}";
        }


        // Increase the catalog version, with a new update date.
        public void NewVersion(DateTime updateDate)
        {
            Version++;

            UpdateDate = updateDate;
        }


        // Return the game version this catalog was made for.
        public Version GameVersion() 
        {
            return Toolkit.ConvertToGameVersion(GameVersionString);
        } 


        // Update some catalog properties.
        public void Update(Version gameVersion = null, string note = null, string reportHeaderText = null, string reportFooterText = null)
        {
            GameVersionString = (gameVersion == null) ? GameVersionString : gameVersion.ToString();

            Note = note ?? Note;

            ReportHeaderText = reportHeaderText ?? ReportHeaderText;

            ReportFooterText = reportFooterText ?? ReportFooterText;
        }


        // Create the indexes for mods, groups or authors, to allow faster lookup. Also counts the number of mods with a manual review.
        private void CreateIndexes()
        {
            foreach (Mod mod in Mods)
            {
                if (!ModIndex.ContainsKey(mod.SteamID))
                {
                    ModIndex.Add(mod.SteamID, mod);
                }
            }

            foreach (Group group in Groups)
            {
                if (!GroupIndex.ContainsKey(group.GroupID))
                {
                    GroupIndex.Add(group.GroupID, group);
                }
            }

            foreach (Author author in Authors)
            {
                if (author.ProfileID != 0 && !AuthorIDIndex.ContainsKey(author.ProfileID))
                {
                    AuthorIDIndex.Add(author.ProfileID, author);
                }

                if (!string.IsNullOrEmpty(author.CustomURL) && !AuthorUrlIndex.ContainsKey(author.CustomURL))
                {
                    AuthorUrlIndex.Add(author.CustomURL, author);
                }
            }

            ReviewedModCount = Mods.FindAll(x => x.ReviewDate != default).Count;
        }


        // Check if the ID is a valid existing or non-existing mod or group ID.
        public bool IsValidID(ulong steamID, bool allowGroup = false, bool allowBuiltin = true, bool shouldExist = true)
        {
            bool valid = (steamID > ModSettings.highestFakeID) || 
                (allowBuiltin && ModSettings.BuiltinMods.ContainsValue(steamID)) || 
                (allowGroup && steamID >= ModSettings.lowestGroupID && steamID <= ModSettings.highestGroupID);

            bool exists = ModIndex.ContainsKey(steamID) || GroupIndex.ContainsKey(steamID);

            return valid && (shouldExist ? exists : !exists);
        }


        // Get a reference to an existing mod, or null if the mod doesn't exist.
        public Mod GetMod(ulong steamID)
        {
            return ModIndex.ContainsKey(steamID) ? ModIndex[steamID] : null;
        }


        // Add a mod and return a reference.
        public Mod AddMod(ulong steamID)
        {
            Mod newMod = new Mod(steamID);

            newMod.Update(addedThisSession: true);

            Mods.Add(newMod);
            ModIndex.Add(steamID, newMod);

            return newMod;
        }


        // Check if a mod is a group member.
        public bool IsGroupMember(ulong steamID)
        {
            return GetGroup(steamID) != default;
        }


        // Return a reference to the group this mod is a member of, or null if not a group member.
        public Group GetGroup(ulong steamID)
        {
            return Groups.FirstOrDefault(x => x.GroupMembers.Contains(steamID));
        }


        // Add a group and return a reference.
        public Group AddGroup(string groupName)
        {
            // Get a new group ID, either one more than the highest current group ID or the default lowest ID if we have no groups yet
            ulong newGroupID = GroupIndex.Any() ? GroupIndex.Keys.Max() + 1 : ModSettings.lowestGroupID;

            Group newGroup = new Group(newGroupID, groupName);

            Groups.Add(newGroup);
            GroupIndex.Add(newGroup.GroupID, newGroup);

            return newGroup;
        }


        // Return a reference to an existing author, or null if the author doesn't exist.
        public Author GetAuthor(ulong authorID, string authorUrl)
        {
            return AuthorIDIndex.ContainsKey(authorID) ? AuthorIDIndex[authorID] : AuthorUrlIndex.ContainsKey(authorUrl) ? AuthorUrlIndex[authorUrl] : null;
        }


        // Add an author and return a reference.
        public Author AddAuthor(ulong authorID, string authorUrl, string name)
        {
            Author author = new Author();

            author.Update(authorID, authorUrl, name);

            Authors.Add(author);

            if (authorID != 0)
            {
                AuthorIDIndex.Add(authorID, author);
            }

            if (!string.IsNullOrEmpty(authorUrl))
            {
                AuthorUrlIndex.Add(authorUrl, author);
            }

            return author;
        }


        // Get all subscribed, builtin and local mods and merge the found info into the catalog. Local mods are temporarily added to the catalog in memory.
        public void GetSubscriptions()
        {
            List<PluginManager.PluginInfo> plugins = new List<PluginManager.PluginInfo>();
            PluginManager manager = Singleton<PluginManager>.instance;

            plugins.AddRange(manager.GetPluginsInfo());
            plugins.AddRange(manager.GetCameraPluginInfos());

            Logger.Log($"Game reports { plugins.Count } mods.");

            // Local mods get a fake Steam ID.
            ulong nextLocalModID = ModSettings.lowestLocalModID;
            ReviewedSubscriptionCount = 0;

            foreach (PluginManager.PluginInfo plugin in plugins)
            {
                ulong steamID;
                bool foundInCatalog = false;

                if (plugin.publishedFileID != PublishedFileId.invalid)
                {
                    // Steam Workshop mod.
                    steamID = plugin.publishedFileID.AsUInt64;
                }
                else if (plugin.isBuiltin)
                {
                    // Builtin mod.
                    string modName = Toolkit.GetPluginName(plugin);

                    if (!plugin.isEnabled)
                    {
                        Logger.Log($"Skipped disabled builtin mod: { modName }");
                        continue;
                    }

                    if (ModSettings.BuiltinMods.ContainsKey(modName))
                    {
                        steamID = ModSettings.BuiltinMods[modName];
                    }
                    else
                    {
                        Logger.Log($"Skipped an unknown builtin mod: { modName }. { ModSettings.pleaseReportText }", Logger.error);
                        continue;
                    }
                }
                else
                {
                    // Local mod. Matching local mods to catalog mods is a future idea. For now just add it to the catalog.
                    steamID = nextLocalModID++;

                    // Set foundInCatalog true to avoid a 'not found in catalog' log message for local mods.
                    foundInCatalog = true;
                }

                foundInCatalog = foundInCatalog || ModIndex.ContainsKey(steamID);

                Mod subscribedMod = GetMod(steamID) ?? AddMod(steamID);

                // Update the name for mods that weren't found in the catalog.
                if (string.IsNullOrEmpty(subscribedMod.Name))
                {
                    subscribedMod.Update(name: Toolkit.GetPluginName(plugin));
                }

                Logger.Log($"Mod found{ (foundInCatalog ? "" : " in game but not in the catalog") }: { subscribedMod.ToString() }");

                // Update the catalog mod with subscription info.
                // [Todo 0.4] How reliable is downloadTime? Is ToLocalTime needed? Check how Loading Order Mod does this.
                subscribedMod.UpdateSubscription(isDisabled: !plugin.isEnabled, plugin.isCameraScript,
                    downloadedTime: PackageEntry.GetLocalModTimeUpdated(plugin.modPath).ToLocalTime());

                if (subscribedMod.ReviewDate != default)
                {
                    ReviewedSubscriptionCount++;
                }

                SubscriptionIDIndex.Add(subscribedMod.SteamID);

                if (SubscriptionNameIndex.ContainsKey(subscribedMod.Name))
                {
                    // Identical name found earlier for another mod; add the Steam ID to the list of Steam IDs for this name.
                    SubscriptionNameIndex[subscribedMod.Name].Add(subscribedMod.SteamID);
                }
                else
                {
                    SubscriptionNameIndex.Add(subscribedMod.Name, new List<ulong> { subscribedMod.SteamID });
                }

                // Add an empty entry to the compatibilities index for this mod, to make sure every subscription has an empty list in that index instead of null.
                SubscriptionCompatibilityIndex.Add(subscribedMod.SteamID, new List<Compatibility>());
            }

            // Find all compatibilities with two subscribed mods.
            foreach (Compatibility catalogCompatibility in Compatibilities)
            {
                if (SubscriptionIDIndex.Contains(catalogCompatibility.FirstModID) && SubscriptionIDIndex.Contains(catalogCompatibility.SecondModID))
                {
                    SubscriptionCompatibilityIndex[catalogCompatibility.FirstModID].Add(catalogCompatibility);
                    SubscriptionCompatibilityIndex[catalogCompatibility.SecondModID].Add(catalogCompatibility);
                }
            }
        }


        // Save a catalog to disk.
        public bool Save(string fullPath)
        {
            try
            {
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


        // Load a catalog from disk.
        private static Catalog LoadFromDisk(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Logger.Log($"Can't load nonexistent catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);

                return null;
            }

            Catalog loadedCatalog = new Catalog();

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Catalog));

                using (TextReader reader = new StreamReader(fullPath))
                {
                    loadedCatalog = (Catalog)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("There is an error in XML document") || ex.ToString().Contains("Document element did not appear"))
                {
                    Logger.Log($"XML error in catalog \"{ Toolkit.PrivacyPath(fullPath) }\". Catalog could not be loaded.", Logger.warning);
                    Logger.Exception(ex, hideFromGameLog: true, debugOnly: true);
                }
                else
                {
                    Logger.Log($"Can't load catalog \"{ Toolkit.PrivacyPath(fullPath) }\".", Logger.warning);
                    Logger.Exception(ex);
                }

                return null;
            }

            if (loadedCatalog.Version == 0 || loadedCatalog.UpdateDate == default)
            {
                Logger.Log($"Discarded invalid catalog \"{ Toolkit.PrivacyPath(fullPath) }\". It has an incorrect version ({ loadedCatalog.VersionString() }) " +
                    $"or date ({ Toolkit.DateString(loadedCatalog.UpdateDate) }).", Logger.error);

                return null;
            }

            return loadedCatalog;
        }


        // Load and download catalogs and return a reference to the one with the highest version.
        public static Catalog Load()
        {
            // Downloaded catalog is always newer than, or same as, the previously downloaded and bundled catalogs, so no need to load those after succesful download.
            Catalog downloadedCatalog = Download();
            Catalog previouslyDownloadedCatalog = downloadedCatalog == null ? LoadPreviouslyDownloaded() : null;
            Catalog bundledCatalog = downloadedCatalog == null ? LoadBundled() : null;
            Catalog updaterCatalog = LoadUpdaterCatalog();

            Catalog newestCatalog = Newest(Newest(downloadedCatalog, previouslyDownloadedCatalog), Newest(bundledCatalog, updaterCatalog));

            if (newestCatalog != null)
            {
                newestCatalog.CreateIndexes();

                Logger.Log($"Using catalog { newestCatalog.VersionString() }, created on { newestCatalog.UpdateDate.ToLongDateString() }. " +
                    $"Catalog contains { newestCatalog.ReviewedModCount } reviewed mods and { newestCatalog.Mods.Count - newestCatalog.ReviewedModCount } " +
                    "mods with basic information.", duplicateToGameLog: true);
            }

            return newestCatalog;
        }


        // Download a new catalog and return a reference, or null if download was not succesful. Only try a download once per session.
        private static Catalog Download()
        {
            if (s_downloadedThisSession)
            {
                return null;
            }

            s_downloadedThisSession = true;

            string temporaryFile = ModSettings.downloadedCatalogFullPath + ".part";

            if (!Toolkit.DeleteFile(temporaryFile))
            {
                Logger.Log("Partially downloaded catalog still exists from a previous session and can't be deleted. This prevents a new download.", Logger.error);

                return null;
            }

            Stopwatch timer = Stopwatch.StartNew();

            Exception ex = Toolkit.Download(ModSettings.catalogURL, temporaryFile);

            timer.Stop();

            if (ex != null)
            {
                Logger.Log($"Can't download catalog from { ModSettings.catalogURL }", Logger.warning);

                if (ex.ToString().Contains("Security.Protocol.Tls.TlsException: The authentication or decryption has failed"))
                {
                    Logger.Log("It looks like the download link only supports TLS 1.2 or higher, while Cities: Skylines modding only supports TLS 1.1 and lower.");
                    Logger.Exception(ex, hideFromGameLog: true, debugOnly: true);
                }
                else
                {
                    Logger.Exception(ex);
                }

                Toolkit.DeleteFile(temporaryFile);

                return null;
            }

            Logger.Log($"Catalog downloaded in { Toolkit.ElapsedTime(timer.ElapsedMilliseconds) } from { ModSettings.catalogURL }");

            Catalog downloadedCatalog = LoadFromDisk(temporaryFile);

            if (downloadedCatalog == null)
            {
                Logger.Log("Could not load newly downloaded catalog.", Logger.error);
            }
            else
            {
                Logger.Log($"Downloaded catalog is version { downloadedCatalog.VersionString() }.");

                // Copy the temporary file over the previously downloaded catalog
                Toolkit.CopyFile(temporaryFile, ModSettings.downloadedCatalogFullPath);
            }

            Toolkit.DeleteFile(temporaryFile);

            return downloadedCatalog;
        }


        // Load the previously downloaded catalog and return a reference, or null if it doesn't exist or can't load.
        private static Catalog LoadPreviouslyDownloaded()
        {
            if (!File.Exists(ModSettings.downloadedCatalogFullPath))
            {
                return null;
            }

            Catalog previouslyDownloadedCatalog = LoadFromDisk(ModSettings.downloadedCatalogFullPath);

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


        // Load the bundled catalog and return a refence, or null if it somehow doesn't exist or can't load.
        private static Catalog LoadBundled()
        {
            Catalog bundledCatalog = LoadFromDisk(ModSettings.bundledCatalogFullPath);

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


        // Load updater catalog, if the updater is enabled and an updater catalog can be found. The last one in an alphabetically sorted list will be loaded.
        private static Catalog LoadUpdaterCatalog()
        {
            if (!ModSettings.UpdaterEnabled)
            {
                return null;
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(ModSettings.updaterPath, $"{ ModSettings.internalName }_Catalog*.xml");
            }
            catch
            {
                return null;
            }

            if (files.Length == 0)
            {
                return null;
            }

            Array.Sort(files);

            Catalog catalog = LoadFromDisk(files.Last());

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


        // From two catalogs return the one with the highest version. Return the first catalog if both are the same version, or null if both are null.
        private static Catalog Newest(Catalog catalog1, Catalog catalog2)
        {
            if (catalog1 == null || catalog2 == null)
            {
                return catalog1 ?? catalog2;
            }
            else
            {
                return (catalog1.Version >= catalog2.Version) ? catalog1 : catalog2;
            }
        }
    }
}
