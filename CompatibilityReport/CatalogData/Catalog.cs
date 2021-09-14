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
    [Serializable][XmlRoot(ModSettings.InternalName + "Catalog")]
    public class Catalog
    {
        // Catalog structure version will change on structural changes that make the xml incompatible. Version will not reset on a new StructureVersion.
        public int StructureVersion { get; private set; } = ModSettings.CurrentCatalogStructureVersion;
        public int Version { get; private set; }
        public DateTime UpdateDate { get; private set; }

        // Game version this catalog was created for. 'Version' is not serializable, so a converted string is used.
        public string GameVersionString { get; private set; } = Toolkit.UnknownVersion().ToString();

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
        private readonly List<ulong> unknownAssets = new List<ulong>();

        // Dictionaries for faster lookup.
        private readonly Dictionary<ulong, Mod> modIndex = new Dictionary<ulong, Mod>();
        private readonly Dictionary<ulong, Group> groupIndex = new Dictionary<ulong, Group>();
        private readonly Dictionary<ulong, Author> authorIDIndex = new Dictionary<ulong, Author>();
        private readonly Dictionary<string, Author> AuthorUrlIndex = new Dictionary<string, Author>();
        // Todo 0.4 Make subscription indexes private.
        [XmlIgnore] public List<ulong> SubscriptionIDIndex { get; } = new List<ulong>();
        [XmlIgnore] public Dictionary<string, List<ulong>> SubscriptionNameIndex { get; } = new Dictionary<string, List<ulong>>();
        [XmlIgnore] public Dictionary<ulong, List<Compatibility>> SubscriptionCompatibilityIndex { get; } = new Dictionary<ulong, List<Compatibility>>();

        [XmlIgnore] public int ReviewedModCount { get; private set; }
        [XmlIgnore] public int ReviewedSubscriptionCount { get; private set; }

        [XmlIgnore] public Updater.ChangeNotes ChangeNotes { get; } = new Updater.ChangeNotes();

        private static bool downloadedThisSession;


        // Default constructor for deserialization and catalog creation.
        public Catalog()
        {
            // Nothing to do here.
        }


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
                if (!modIndex.ContainsKey(mod.SteamID))
                {
                    modIndex.Add(mod.SteamID, mod);

                    if (mod.ReviewDate != default || mod.Stability == Enums.Stability.IncompatibleAccordingToWorkshop)
                    {
                        ReviewedModCount++;
                    }
                }
            }

            foreach (Group group in Groups)
            {
                if (!groupIndex.ContainsKey(group.GroupID))
                {
                    groupIndex.Add(group.GroupID, group);
                }
            }

            foreach (Author author in Authors)
            {
                if (author.SteamID != 0 && !authorIDIndex.ContainsKey(author.SteamID))
                {
                    authorIDIndex.Add(author.SteamID, author);
                }

                if (!string.IsNullOrEmpty(author.CustomUrl) && !AuthorUrlIndex.ContainsKey(author.CustomUrl))
                {
                    AuthorUrlIndex.Add(author.CustomUrl, author);
                }
            }
        }


        // Check if the ID is a valid existing or non-existing mod or group ID.
        public bool IsValidID(ulong steamID, bool allowGroup = false, bool allowBuiltin = true, bool shouldExist = true)
        {
            bool valid = (steamID > ModSettings.HighestFakeID) || 
                (allowBuiltin && ModSettings.BuiltinMods.ContainsValue(steamID)) || 
                (allowGroup && steamID >= ModSettings.LowestGroupID && steamID <= ModSettings.HighestGroupID);

            bool exists = modIndex.ContainsKey(steamID) || groupIndex.ContainsKey(steamID);

            return valid && (shouldExist ? exists : !exists);
        }


        // Get a reference to an existing mod, or null if the mod doesn't exist.
        public Mod GetMod(ulong steamID)
        {
            return modIndex.ContainsKey(steamID) ? modIndex[steamID] : null;
        }


        // Add a mod and return a reference.
        public Mod AddMod(ulong steamID)
        {
            Mod newMod = new Mod(steamID);

            Mods.Add(newMod);
            modIndex.Add(steamID, newMod);

            return newMod;
        }


        // Remove a mod.
        public bool RemoveMod(Mod mod)
        {
            return Mods.Remove(mod) && modIndex.Remove(mod.SteamID);
        }


        // Add a compatibility.
        public void AddCompatibility(ulong firstModID, ulong secondModID, Enums.CompatibilityStatus compatibilityStatus, string compatibilityNote)
        {
            Compatibilities.Add(new Compatibility(firstModID, GetMod(firstModID).Name, secondModID, GetMod(secondModID).Name, compatibilityStatus, compatibilityNote));
        }


        // Check if a mod is a group member.
        public bool IsGroupMember(ulong steamID)
        {
            return GetThisModsGroup(steamID) != default;
        }


        // Return a reference to the group this mod is a member of, or null if not a group member.
        public Group GetThisModsGroup(ulong steamID)
        {
            return Groups.FirstOrDefault(x => x.GroupMembers.Contains(steamID));
        }


        // Get a reference to an existing group, or null if the group doesn't exist.
        public Group GetGroup(ulong groupID)
        {
            return groupIndex.ContainsKey(groupID) ? groupIndex[groupID] : null;
        }


        // Add a group and return a reference.
        public Group AddGroup(string groupName)
        {
            ulong newGroupID = groupIndex.Any() ? groupIndex.Keys.Max() + 1 : ModSettings.LowestGroupID;
            Group newGroup = new Group(newGroupID, groupName);

            Groups.Add(newGroup);
            groupIndex.Add(newGroup.GroupID, newGroup);

            return newGroup;
        }


        // Remove a group.
        public bool RemoveGroup(Group group)
        {
            return Groups.Remove(group) && groupIndex.Remove(group.GroupID);
        }


        // Return a reference to an existing author, or null if the author doesn't exist.
        public Author GetAuthor(ulong authorID, string authorUrl)
        {
            return authorIDIndex.ContainsKey(authorID) ? authorIDIndex[authorID] : AuthorUrlIndex.ContainsKey(authorUrl ?? "") ? AuthorUrlIndex[authorUrl] : null;
        }


        // Add an author and return a reference.
        public Author AddAuthor(ulong authorID, string authorUrl)
        {
            Author author = new Author(authorID, authorUrl);
            Authors.Add(author);

            if (authorID != 0)
            {
                authorIDIndex.Add(authorID, author);
            }

            if (!string.IsNullOrEmpty(authorUrl))
            {
                AuthorUrlIndex.Add(authorUrl, author);
            }

            return author;
        }


        // Get the list of potential assets.
        public string GetUnknownAssetsString()
        {
            return unknownAssets.Any() ? string.Join(", ", unknownAssets.Select(steamID => steamID.ToString()).ToArray()) : "";
        }


        // Add an asset to the list of potential assets.
        public bool AddUnknownAsset(ulong unknownAsset)
        {
            if (unknownAssets.Contains(unknownAsset))
            {
                return false;
            }

            unknownAssets.Add(unknownAsset);
            return true;
        }


        // Remove an asset from the list of potential assets.
        public void RemoveUnknownAsset(ulong knownAsset)
        {
            unknownAssets.Remove(knownAsset);
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
            ulong nextLocalModID = ModSettings.LowestLocalModID;

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
                        Logger.Log($"Skipped an unknown builtin mod: { modName }. { ModSettings.PleaseReportText }", Logger.Warning);
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

                foundInCatalog = foundInCatalog || modIndex.ContainsKey(steamID);

                Mod subscribedMod = GetMod(steamID) ?? AddMod(steamID);

                // Update the name for mods that weren't found in the catalog.
                if (string.IsNullOrEmpty(subscribedMod.Name))
                {
                    subscribedMod.Update(name: Toolkit.GetPluginName(plugin));
                }

                Logger.Log($"Mod found{ (foundInCatalog ? "" : " in game but not in the catalog") }: { subscribedMod.ToString() }");

                // Update the catalog mod with subscription info.
                // Todo 0.4 How reliable is downloadTime? Is ToLocalTime needed? Check how Loading Order Mod does this.
                subscribedMod.UpdateSubscription(isDisabled: !plugin.isEnabled, plugin.isCameraScript,
                    downloadedTime: PackageEntry.GetLocalModTimeUpdated(plugin.modPath).ToLocalTime());

                if (subscribedMod.ReviewDate != default)
                {
                    ReviewedSubscriptionCount++;
                }

                SubscriptionIDIndex.Add(subscribedMod.SteamID);

                if (SubscriptionNameIndex.ContainsKey(subscribedMod.Name))
                {
                    // Identical name found earlier for another mod. Add the Steam ID to the list of Steam IDs for this name and sort the list.
                    SubscriptionNameIndex[subscribedMod.Name].Add(subscribedMod.SteamID);
                    SubscriptionNameIndex[subscribedMod.Name].Sort();
                }
                else
                {
                    SubscriptionNameIndex.Add(subscribedMod.Name, new List<ulong> { subscribedMod.SteamID });
                }

                // Add an empty entry to the compatibilities index for this mod, to make sure every subscription has an empty list in that index instead of null.
                SubscriptionCompatibilityIndex.Add(subscribedMod.SteamID, new List<Compatibility>());
            }

            SubscriptionIDIndex.Sort();

            // Find all compatibilities with two subscribed mods.
            foreach (Compatibility catalogCompatibility in Compatibilities)
            {
                if (SubscriptionIDIndex.Contains(catalogCompatibility.FirstSteamID) && SubscriptionIDIndex.Contains(catalogCompatibility.SecondSteamID))
                {
                    SubscriptionCompatibilityIndex[catalogCompatibility.FirstSteamID].Add(catalogCompatibility);
                    SubscriptionCompatibilityIndex[catalogCompatibility.SecondSteamID].Add(catalogCompatibility);
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

                Logger.Log($"Created catalog { VersionString() } at \"{Toolkit.Privacy(fullPath)}\".");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create catalog at \"{Toolkit.Privacy(fullPath)}\".", Logger.Error);
                Logger.Exception(ex);

                return false;
            }
        }


        // Load a catalog from disk.
        private static Catalog LoadFromDisk(string fullPath)
        {
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
                    Logger.Log($"XML error in catalog \"{ Toolkit.Privacy(fullPath) }\". Catalog could not be loaded.", Logger.Error);
                    Logger.Exception(ex, hideFromGameLog: true, debugOnly: true);
                }
                else
                {
                    Logger.Log($"Can't load catalog \"{ Toolkit.Privacy(fullPath) }\".", Logger.Error);
                    Logger.Exception(ex);
                }

                return null;
            }

            if (loadedCatalog.Version == 0 || loadedCatalog.UpdateDate == default)
            {
                Logger.Log($"Discarded invalid catalog \"{ Toolkit.Privacy(fullPath) }\". It has an incorrect version ({ loadedCatalog.VersionString() }) " +
                    $"or date ({ Toolkit.DateString(loadedCatalog.UpdateDate) }).", Logger.Error);

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
            if (downloadedThisSession)
            {
                return null;
            }

            downloadedThisSession = true;

            string temporaryFile = Path.Combine(ModSettings.WorkPath, $"{ ModSettings.DownloadedCatalogFileName }.part");

            if (!Toolkit.DeleteFile(temporaryFile))
            {
                Logger.Log("Partially downloaded catalog still exists from a previous session and can't be deleted. This prevents a new download.", Logger.Error);
                return null;
            }

            Stopwatch timer = Stopwatch.StartNew();

            if (!Toolkit.Download(ModSettings.CatalogURL, temporaryFile))
            {
                Toolkit.DeleteFile(temporaryFile);

                Logger.Log($"Can't download new catalog from { ModSettings.CatalogURL }", Logger.Warning);
                return null;
            }

            timer.Stop();

            Logger.Log($"Catalog downloaded in { Toolkit.TimeString(timer.ElapsedMilliseconds) }.");

            Catalog downloadedCatalog = LoadFromDisk(temporaryFile);

            if (downloadedCatalog == null)
            {
                Logger.Log("Could not load newly downloaded catalog.", Logger.Error);
            }
            else
            {
                Logger.Log($"Downloaded catalog is version { downloadedCatalog.VersionString() }.");

                // Copy the temporary file over the previously downloaded catalog.
                Toolkit.CopyFile(temporaryFile, Path.Combine(ModSettings.WorkPath, ModSettings.DownloadedCatalogFileName));
            }

            Toolkit.DeleteFile(temporaryFile);

            return downloadedCatalog;
        }


        // Load the previously downloaded catalog and return a reference, or null if it doesn't exist or can't load.
        private static Catalog LoadPreviouslyDownloaded()
        {
            string previouslyDownloadedFullPath = Path.Combine(ModSettings.WorkPath, ModSettings.DownloadedCatalogFileName);

            if (!File.Exists(previouslyDownloadedFullPath))
            {
                return null;
            }

            Catalog previouslyDownloadedCatalog = LoadFromDisk(previouslyDownloadedFullPath);

            if (previouslyDownloadedCatalog == null)
            {
                if (Toolkit.DeleteFile(previouslyDownloadedFullPath))
                {
                    Logger.Log("Coud not load previously downloaded catalog. It has been deleted.", Logger.Warning);
                }
                else
                {
                    Logger.Log("Can't load previously downloaded catalog and it can't be deleted either. " +
                        "This prevents saving a newly downloaded catalog for future sessions.", Logger.Error);
                }
            }
            else
            {
                Logger.Log($"Previously downloaded catalog is version { previouslyDownloadedCatalog.VersionString() }.");
            }

            return previouslyDownloadedCatalog;
        }


        // Load the bundled catalog and return a refence, or null if it somehow doesn't exist or can't load.
        private static Catalog LoadBundled()
        {
            if (!File.Exists(ModSettings.BundledCatalogFullPath))
            {
                Logger.Log($"No bundled catalog found. { ModSettings.PleaseReportText }", Logger.Error, duplicateToGameLog: true);
                return null;
            }

            Catalog bundledCatalog = LoadFromDisk(ModSettings.BundledCatalogFullPath);

            if (bundledCatalog == null)
            {
                Logger.Log($"Can't load bundled catalog. { ModSettings.PleaseReportText }", Logger.Error, duplicateToGameLog: true);
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
            if (!ModSettings.UpdaterAvailable)
            {
                return null;
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog*.xml");
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
                Logger.Log("Can't load updater catalog.", Logger.Error);
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
