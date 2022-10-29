using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using CompatibilityReport.Settings;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.Util;

namespace CompatibilityReport.CatalogData
{
    [Serializable][XmlRoot(ModSettings.InternalName + "Catalog")]
    public class Catalog
    {
        // Catalog structure version will change on structural changes that make the xml incompatible. Version will not reset on a new StructureVersion.
        public int StructureVersion { get; private set; } = ModSettings.CurrentCatalogStructureVersion;
        public int Version { get; private set; }
        public DateTime Updated { get; private set; }

        // Game version this catalog was created for. 'Version' is not serializable, so a converted string is used.
        [XmlElement("GameVersion")] public string GameVersionString { get; private set; } = Toolkit.UnknownVersion().ToString();

        // A note about the catalog, displayed in the report, and the header and footer text for the report.
        public string Note { get; private set; }
        public string ReportHeaderText { get; private set; }
        public string ReportFooterText { get; private set; }

        // The actual mod data in four lists.
        public List<Mod> Mods { get; private set; } = new List<Mod>();
        public List<Group> Groups { get; private set; } = new List<Group>();
        public List<Compatibility> Compatibilities { get; private set; } = new List<Compatibility>();
        public List<Author> Authors { get; private set; } = new List<Author>();

        // Map Themes are technically mods, but we don't want to include them in the catalog or the report. This list is used to recognize them.
        [XmlArrayItem("SteamID")] public List<ulong> MapThemes { get; private set; } = new List<ulong>();

        // Assets that show up as required items. This is used to distinguish between a required asset and an unknown required mod.
        [XmlArrayItem("SteamID")] public List<ulong> RequiredAssets { get; private set; } = new List<ulong>();

        // Steam IDs that give warnings we don't want to see, either about an unnamed mod or about a duplicate author name (add both authors).
        [XmlArrayItem("SteamID")] public List<ulong> SuppressedWarnings { get; private set; } = new List<ulong>();

        // Temporary list of newly found unknown required Steam IDs which might be assets, to be evaluated for adding to the RequiredAssets list.
        private readonly List<ulong> potentialAssets = new List<ulong>();

        // Dictionaries for faster lookup.
        private readonly Dictionary<ulong, Mod> modIndex = new Dictionary<ulong, Mod>();
        private readonly Dictionary<ulong, Group> groupIndex = new Dictionary<ulong, Group>();
        private readonly Dictionary<ulong, Author> authorIDIndex = new Dictionary<ulong, Author>();
        private readonly Dictionary<string, Author> authorUrlIndex = new Dictionary<string, Author>();
        private readonly List<ulong> subscriptionIDIndex = new List<ulong>();
        private readonly Dictionary<string, List<ulong>> subscriptionNameIndex = new Dictionary<string, List<ulong>>();
        private readonly Dictionary<ulong, List<Compatibility>> subscriptionCompatibilityIndex = new Dictionary<ulong, List<Compatibility>>();

        [XmlIgnore] public DateTime Downloaded { get; private set; }
        [XmlIgnore] public int ReviewedModCount { get; private set; }
        [XmlIgnore] public int ReviewedSubscriptionCount { get; private set; }
        [XmlIgnore] public int LocalSubscriptionCount { get; private set; }
        [XmlIgnore] public int FakeSubscriptionCount { get; private set; }

        [XmlIgnore] public Updater.ChangeNotes ChangeNotes { get; } = new Updater.ChangeNotes();

        public static bool DownloadStarted { get; private set; }
        public static bool DownloadSuccessful { get; private set; }
        public static string SettingsUIText { get; private set; } = "unknown, catalog not loaded yet."; 


        /// <summary>Default constructor for deserialization and catalog creation.</summary>
        public Catalog()
        {
            // Nothing to do here.
        }


        /// <summary>Converts the catalog version to a string.</summary>
        /// <returns>A formatted string containing the full version, including the structure version.</returns>
        public string VersionString()
        {
            return $"{ StructureVersion }.{ Version }";
        }


        /// <summary>Increases the catalog version and sets a new update date.</summary>
        /// <remarks>The catalog structure version will also be increased if the current structure version from the mod is higher.</remarks>
        public void NewVersion(DateTime newDate)
        {
            if (Version > 99)
            {
                Version = 0;
            }
            else
            {
                Version++;
            }
            StructureVersion = StructureVersion < ModSettings.CurrentCatalogStructureVersion ? ModSettings.CurrentCatalogStructureVersion : StructureVersion;
            Updated = Toolkit.CleanDateTime(newDate);
        }


        /// <summary>Gets the game version this catalog was made for.</summary>
        /// <returns>The game version this catalog was made for.</returns>
        public Version GameVersion() 
        {
            return Toolkit.ConvertToVersion(GameVersionString);
        }


        /// <summary>Updates one or more catalog properties.</summary>
        public void Update(Version gameVersion = null, string note = null, string reportHeaderText = null, string reportFooterText = null)
        {
            GameVersionString = (gameVersion == null) ? GameVersionString : gameVersion.ToString();

            Note = note ?? Note;

            ReportHeaderText = reportHeaderText ?? ReportHeaderText;
            ReportFooterText = reportFooterText ?? ReportFooterText;
        }


        /// <summary>Checks if the ID is a valid mod ID, with the option to check if the mod either exist in the catalog or not.</summary>
        /// <remarks>By default only mod IDs that exist in the catalog are considered valid, including built-in mods.</remarks>
        /// <returns>True if the ID is valid with the selected options, false if invalid.</returns>
        public bool IsValidID(ulong steamID, bool allowBuiltin = true, bool shouldExist = true)
        {
            bool valid = (steamID > ModSettings.HighestFakeID) || 
                (allowBuiltin && ModSettings.BuiltinMods.ContainsValue(steamID));

            bool exists = modIndex.ContainsKey(steamID);

            return valid && (shouldExist ? exists : !exists);
        }


        /// <summary>Gets a mod from the catalog.</summary>
        /// <returns>A reference to the found mod, or null if the mod doesn't exist.</returns>
        public Mod GetMod(ulong steamID)
        {
            return modIndex.ContainsKey(steamID) ? modIndex[steamID] : null;
        }


        /// <summary>Adds a mod to the catalog.</summary>
        /// <returns>A reference to the new mod.</returns>
        public Mod AddMod(ulong steamID)
        {
            Mod newMod = new Mod(steamID);

            Mods.Add(newMod);
            modIndex.Add(newMod.SteamID, newMod);

            MapThemes.Remove(steamID);
            RequiredAssets.Remove(steamID);

            return newMod;
        }


        /// <summary>Removes a mod from the catalog.</summary>
        /// <returns>True if removal succeeded, false if not.</returns>
        public bool RemoveMod(Mod catalogMod)
        {
            SuppressedWarnings.Remove(catalogMod.SteamID);
            return Mods.Remove(catalogMod) && modIndex.Remove(catalogMod.SteamID);
        }


        /// <summary>Checks if a mod is a group member.</summary>
        /// <returns>True if it's a group member, false if not.</returns>
        public bool IsGroupMember(ulong steamID)
        {
            return GetThisModsGroup(steamID) != default;
        }


        /// <summary>Gets the group a mod is a member of.</summary>
        /// <returns>A reference to the group, or null if the mod is not a group member.</returns>
        public Group GetThisModsGroup(ulong steamID)
        {
            return Groups.FirstOrDefault(x => x.GroupMembers.Contains(steamID));
        }


        /// <summary>Gets a group from the catalog.</summary>
        /// <returns>A reference to the found group, or null if the group doesn't exist.</returns>
        public Group GetGroup(ulong groupID)
        {
            return groupIndex.ContainsKey(groupID) ? groupIndex[groupID] : null;
        }


        /// <summary>Adds a group to the catalog.</summary>
        /// <remarks>A group ID is automatically assigned.</remarks>
        /// <returns>A reference to the new group.</returns>
        public Group AddGroup(string groupName)
        {
            ulong newGroupID = groupIndex.Any() ? groupIndex.Keys.Max() + 1 : ModSettings.LowestGroupID;
            Group newGroup = new Group(newGroupID, groupName);

            Groups.Add(newGroup);
            groupIndex.Add(newGroup.GroupID, newGroup);

            return newGroup;
        }


        /// <summary>Removes a group from the catalog.</summary>
        /// <returns>True if removal succeeded, false if not.</returns>
        public bool RemoveGroup(Group catalogGroup)
        {
            return Groups.Remove(catalogGroup) && groupIndex.Remove(catalogGroup.GroupID);
        }


        /// <summary>Adds a compatibility to the catalog.</summary>
        public void AddCompatibility(ulong firstModID, ulong secondModID, Enums.CompatibilityStatus status, string note)
        {
            Compatibilities.Add(new Compatibility(firstModID, GetMod(firstModID).Name, secondModID, GetMod(secondModID).Name, status, note));
        }


        /// <summary>Removes a compatibility from the catalog.</summary>
        /// <returns>True if removal succeeded, false if not</returns>
        public bool RemoveCompatibility(Compatibility catalogCompatibility)
        {
            return Compatibilities.Remove(catalogCompatibility);
        }


        /// <summary>Gets an author from the catalog.</summary>
        /// <returns>A reference to the found author, or null if the author doesn't exist.</returns>
        public Author GetAuthor(ulong authorID, string authorUrl)
        {
            return authorIDIndex.ContainsKey(authorID) ? authorIDIndex[authorID] : authorUrlIndex.ContainsKey(authorUrl ?? "") ? authorUrlIndex[authorUrl] : null;
        }


        /// <summary>Adds an author to the catalog.</summary>
        /// <returns>A reference to the new author.</returns>
        public Author AddAuthor(ulong authorID, string authorUrl)
        {
            Author author = new Author(authorID, authorUrl ?? "");

            Authors.Add(author);
            UpdateAuthorIndexes(author);

            return author;
        }


        /// <summary>Removes an author from the catalog.</summary>
        /// <returns>True if removal succeeded, false if not.</returns>
        public bool RemoveAuthor(Author oldAuthor)
        {
            bool result = Authors.Remove(oldAuthor);

            if (result && oldAuthor.SteamID != 0)
            {
                result = authorIDIndex.Remove(oldAuthor.SteamID);
            }
            if (result && !string.IsNullOrEmpty(oldAuthor.CustomUrl))
            {
                result = authorUrlIndex.Remove(oldAuthor.CustomUrl);
            }

            return result;
        }


        /// <summary>Updates the author index for a new author, or an author with a new Steam ID and/or changed custom URL.</summary>
        public void UpdateAuthorIndexes(Author catalogAuthor, string oldCustomURL = null)
        {
            if (catalogAuthor.SteamID != 0 && !authorIDIndex.ContainsKey(catalogAuthor.SteamID))
            {
                authorIDIndex.Add(catalogAuthor.SteamID, catalogAuthor);
            }

            if (!string.IsNullOrEmpty(catalogAuthor.CustomUrl) && !authorUrlIndex.ContainsKey(catalogAuthor.CustomUrl))
            {
                authorUrlIndex.Add(catalogAuthor.CustomUrl, catalogAuthor);
            }

            if (oldCustomURL != null && oldCustomURL != catalogAuthor.CustomUrl)
            {
                authorUrlIndex.Remove(oldCustomURL);
            }
        }


        /// <summary>Adds an asset to the list of required assets.</summary>
        public void AddAsset(ulong newAsset)
        {
            if (!RequiredAssets.Contains(newAsset))
            {
                RequiredAssets.Add(newAsset);
            }
        }


        /// <summary>Removes an asset from the list of required assets.</summary>
        /// <remarks>No message is logged if the asset was not on the list.</remarks>
        public void RemoveAsset(ulong catalogAsset)
        {
            RequiredAssets.Remove(catalogAsset);
        }


        /// <summary>Gets the potential assets.</summary>
        /// <returns>A string with all potential assets, seperated by commas.</returns>
        public string GetPotentialAssetsString()
        {
            return potentialAssets.Any() ? string.Join(", ", potentialAssets.Select(steamID => steamID.ToString()).ToArray()) : "";
        }


        /// <summary>Adds a Steam ID to the list of Map Themes.</summary>
        /// <returns>True if added, false if it was already in the list.</returns>
        public bool AddMapTheme(ulong steamID)
        {
            if (MapThemes.Contains(steamID))
            {
                return false;
            }

            MapThemes.Add(steamID);
            return true;
        }


        /// <summary>Removes a Steam ID from the list of Map Themes.</summary>
        /// <returns>True if removal succeeded, false if not</remarks>
        public bool RemoveMapTheme(ulong steamID)
        {
            return MapThemes.Remove(steamID);
        }


        /// <summary>Adds an asset to the list of potential assets.</summary>
        /// <returns>True if added, false if it was already in the list.</returns>
        public bool AddPotentialAsset(ulong potentialAsset)
        {
            if (potentialAssets.Contains(potentialAsset))
            {
                return false;
            }

            potentialAssets.Add(potentialAsset);
            return true;
        }


        /// <summary>Removes an asset from the list of potential assets.</summary>
        /// <remarks>No message is logged if the asset was not on the list.</remarks>
        public void RemovePotentialAsset(ulong knownAsset)
        {
            potentialAssets.Remove(knownAsset);
        }


        /// <summary>Adds a Steam ID to the list of suppressed warnings.</summary>
        /// <remarks>No message is logged if the Steam ID was already on the list.</remarks>
        public void AddSuppressedWarning(ulong steamID)
        {
            if (!SuppressedWarnings.Contains(steamID))
            {
                SuppressedWarnings.Add(steamID);
            }
        }


        /// <summary>Removes an Steam ID from the list of suppressed warnings.</summary>
        /// <returns>True if removal succeeded, false if not</returns>
        public bool RemoveSuppressedWarning(ulong steamID)
        {
            return SuppressedWarnings.Remove(steamID);
        }


        /// <summary>Gets all subscribed, local and enabled built-in mods, and merge their info with the mods in the catalog.</summary>
        /// <remarks>Local mods and unknown Steam Workshop mods are temporarily added to the catalog in memory. 
        ///          This also gets all fake subscriptions, if supplied.</remarks>
        public void ScanSubscriptions()
        {
            List<PluginManager.PluginInfo> plugins = new List<PluginManager.PluginInfo>();
            PluginManager manager = Singleton<PluginManager>.instance;

            plugins.AddRange(manager.GetPluginsInfo());
            plugins.AddRange(manager.GetCameraPluginInfos());

            Logger.Log($"Game reports { plugins.Count } mods.", Logger.Debug);

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

                    if (MapThemes.Contains(steamID))
                    {
                        Logger.Log($"Skipped Map Theme: { Toolkit.GetPluginName(plugin) }", Logger.Debug);
                        continue;
                    }
                }
                else if (plugin.isBuiltin)
                {
                    // Built-in mod.
                    string modName = Toolkit.GetPluginName(plugin);

                    if (!plugin.isEnabled)
                    {
                        Logger.Log($"Skipped disabled built-in mod: { modName }", Logger.Debug);
                        continue;
                    }

                    if (ModSettings.BuiltinMods.ContainsKey(modName))
                    {
                        steamID = ModSettings.BuiltinMods[modName];
                    }
                    else
                    {
                        Logger.Log($"Found an unknown built-in mod: { modName }. { ModSettings.PleaseReportText }", Logger.Warning);
                        continue;
                    }
                }
                else
                {
                    // Local mod. Matching local mods to catalog mods is a future idea. For now just add it to the catalog.
                    steamID = nextLocalModID++;
                    LocalSubscriptionCount++;

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

                // Add the in-game name to the mod note, for mods where it seriously differs from the Workshop name.
                if (subscribedMod.Statuses.Contains(Enums.Status.ModNamesDiffer))
                {
                    string inGameName = Toolkit.GetPluginName(plugin);
                    if (inGameName != subscribedMod.Name)
                    {
                        subscribedMod.Update(note: $"{ (string.IsNullOrEmpty(subscribedMod.Note) ? "" : $"{ subscribedMod.Note }\n") }The in-game name is { inGameName }");
                    }
                }

                if (foundInCatalog)
                {
                    Logger.Log($"Mod found: { subscribedMod.ToString() }{ (steamID == nextLocalModID -1 ? $", Path: { Toolkit.Privacy(plugin.modPath) }" : "") }", 
                        Logger.Debug);
                }
                else
                {
                    Logger.Log($"Mod found in game but not in the catalog: { subscribedMod.ToString() }");
                }

                // Update the catalog mod with subscription info.
                // Todo 1.x Will we use downloadTime? How reliable is it? Is ToLocalTime needed? Check how Loading Order Mod does this.
                subscribedMod.UpdateSubscription(isDisabled: !plugin.isEnabled, plugin.isCameraScript, modPath: plugin.modPath, 
                    downloadedTime: PackageEntry.GetLocalModTimeUpdated(plugin.modPath).ToLocalTime());

                AddSubscription(subscribedMod);
            }

            // Add fake subscriptions from a file, if it exists. Ignores lines that are not a Steam ID and all IDs that are subscribed or don't exist in the catalog.
            if (File.Exists(ModSettings.FakeSubscriptionsFileFullPath))
            {
                using (StreamReader reader = File.OpenText(ModSettings.FakeSubscriptionsFileFullPath))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        Mod subscribedMod = GetMod(Toolkit.ConvertToUlong(line));

                        if (subscribedMod != null && !subscriptionIDIndex.Contains(subscribedMod.SteamID))
                        {
                            Logger.Log($"Fake subscription added for testing: { subscribedMod.ToString() }");

                            subscribedMod.Update(note: (string.IsNullOrEmpty(subscribedMod.Note) ? "" : $"{ subscribedMod.Note }\n") +
                                "This is a fake subscription for testing purposes. This is not really subscribed.");

                            AddSubscription(subscribedMod);
                            FakeSubscriptionCount++;
                        }
                    }
                }
            }

            subscriptionIDIndex.Sort();

            // Find all compatibilities with two subscribed mods.
            foreach (Compatibility catalogCompatibility in Compatibilities)
            {
                if (subscriptionIDIndex.Contains(catalogCompatibility.FirstModID) && subscriptionIDIndex.Contains(catalogCompatibility.SecondModID))
                {
                    subscriptionCompatibilityIndex[catalogCompatibility.FirstModID].Add(catalogCompatibility);
                    subscriptionCompatibilityIndex[catalogCompatibility.SecondModID].Add(catalogCompatibility);
                }
            }
        }


        private void AddSubscription(Mod subscribedMod)
        {
            if (subscribedMod.Stability > Enums.Stability.NotReviewed)
            {
                ReviewedSubscriptionCount++;
            }

            subscriptionIDIndex.Add(subscribedMod.SteamID);

            if (subscriptionNameIndex.ContainsKey(subscribedMod.Name))
            {
                // Identical name found earlier for another mod. Add the Steam ID to the list of Steam IDs for this name and sort the list.
                subscriptionNameIndex[subscribedMod.Name].Add(subscribedMod.SteamID);
                subscriptionNameIndex[subscribedMod.Name].Sort();
            }
            else
            {
                subscriptionNameIndex.Add(subscribedMod.Name, new List<ulong> { subscribedMod.SteamID });
            }

            // Add an empty entry to the compatibilities index for this mod, to make sure every subscription has an empty list in that index instead of null.
            subscriptionCompatibilityIndex.Add(subscribedMod.SteamID, new List<Compatibility>());
        }


        /// <summary>Get the number of subscribed, local and enabled built-in mods.</summary>
        /// <returns>The number of subscriptions.</returns>
        public int SubscriptionCount()
        {
            return subscriptionIDIndex.Count;
        }


        /// <summary>Gets a subscribed, local or enabled built-in mod from the catalog.</summary>
        /// <returns>A reference to the found mod, or null if the subscription doesn't exist.</returns>
        public Mod GetSubscription(ulong steamID)
        {
            return subscriptionIDIndex.Contains(steamID) && modIndex.ContainsKey(steamID) ? modIndex[steamID] : null;
        }


        /// <summary>Get the Steam IDs of all subscribed, local and enabled built-in mods.</summary>
        /// <returns>A reference to the list of subscription Steam IDs.</returns>
        public List<ulong> GetSubscriptionIDs()
        {
            return subscriptionIDIndex;
        }


        /// <summary>Get the names of all subscribed, local and enabled built-in mods.</summary>
        /// <returns>A sorted list of mod names.</returns>
        public List<string> GetSubscriptionNames()
        {
            List<string> subscriptionNames = new List<string>(subscriptionNameIndex.Keys);
            subscriptionNames.Sort();
            return subscriptionNames;
        }


        /// <summary>Get all Steam IDs of subscribed, local or built-in mods with a given name.</summary>
        /// <returns>A reference to a list of Steam IDs, or an empty list if the name does not match any.</returns>
        public List<ulong> GetSubscriptionIDsByName(string name)
        {
            return subscriptionNameIndex.ContainsKey(name) ? subscriptionNameIndex[name] : new List<ulong>();
        }


        /// <summary>Get all compatibilities of a subscribed, local or built-in mod.</summary>
        /// <returns>A reference to the list of compatibilities for this subscription, or an empty list if the subscription does not exist.</returns>
        public List<Compatibility> GetSubscriptionCompatibilities(ulong steamID)
        {
            return GetSubscription(steamID) == null ? new List<Compatibility>() : subscriptionCompatibilityIndex[steamID];
        }


        /// <summary>Saves the catalog to disk, both in plain text and gzipped.</summary>
        /// <remarks>The fullPath parameter should not contain the GZip extension.</remarks>
        /// <returns>True if saved succesfully, false otherwise.</returns>
        public bool Save(string fullPath)
        {
            try
            {
                // Catalog as plain text XML file.
                XmlSerializer serializer = new XmlSerializer(typeof(Catalog));

                using (TextWriter writer = new StreamWriter(fullPath))
                {
                    serializer.Serialize(writer, this);
                }

                Logger.Log($"Created catalog { VersionString() } at \"{Toolkit.Privacy(fullPath)}\".");

                // Catalog as gzipped XML file.
                fullPath = $"{ fullPath }.gz";

                using (FileStream file = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
                using (TextWriter writer = new StreamWriter(gzip))
                {
                    serializer.Serialize(writer, this);
                }

                Logger.Log($"Created catalog { VersionString() } at \"{Toolkit.Privacy(fullPath)}\".");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to create catalog at \"{Toolkit.Privacy(fullPath)}\".", Logger.Debug);
                Logger.Exception(ex, Logger.Debug);

                return false;
            }
        }


        /// <summary>Loads a catalog from disk.</summary>
        /// <remarks>Support gzipped and plain text files.</remarks>
        /// <returns>A reference to the catalog, or null if loading failed.</returns>
        private static Catalog LoadFromDisk(string fullPath)
        {
            Catalog loadedCatalog = new Catalog();

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Catalog));

                try
                {
                    using (FileStream file = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                    using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
                    using (TextReader reader = new StreamReader(gzip))
                    {
                        loadedCatalog = (Catalog)serializer.Deserialize(reader);
                    }
                }
                catch (IOException)
                {
                    Logger.Log($"Catalog is not a valid GZip file, will attempt to read as plain text: \"{ Toolkit.Privacy(fullPath) }\"", Logger.Debug);

                    using (TextReader reader = new StreamReader(fullPath))
                    {
                        loadedCatalog = (Catalog)serializer.Deserialize(reader);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message.Contains("<html xmlns=''> was not expected"))
                {
                    Logger.Log($"Catalog not downloaded due to download limits at the website.", Logger.Warning);
                }
                else
                {
                    Logger.Log($"Can't load catalog \"{ Toolkit.Privacy(fullPath) }\".", Logger.Debug);
                }
                Logger.Exception(ex, Logger.Debug);
                return null;
            }
            catch (XmlException ex)
            {
                Logger.Log($"XML error while reading the catalog. Catalog could not be loaded: \"{ Toolkit.Privacy(fullPath) }\".", Logger.Debug);
                Logger.Exception(ex, Logger.Debug);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Can't load catalog \"{ Toolkit.Privacy(fullPath) }\".", Logger.Debug);
                Logger.Exception(ex, Logger.Debug);
                return null;
            }

            if (loadedCatalog.Version == 0 || loadedCatalog.Updated == default)
            {
                Logger.Log($"Discarded invalid catalog \"{ Toolkit.Privacy(fullPath) }\". It has an incorrect version ({ loadedCatalog.VersionString() }) " +
                    $"or date ({ Toolkit.DateString(loadedCatalog.Updated) }).", Logger.Debug);

                return null;
            }

            loadedCatalog.Downloaded = File.GetLastWriteTime(fullPath);

            return loadedCatalog;
        }


        /// <summary>Loads and downloads catalogs and determines which has the highest version.</summary>
        /// <remarks>Four catalogs are considered: new download, previously downloaded, bundled and updater. If a download is succesful, 
        ///          the previously downloaded and bundled catalogs are not checked. An updater catalog only exists for maintainers of this mod.</remarks>
        /// <returns>A reference to the catalog with the highest version, or null if none could be loaded.</returns>
        public static Catalog Load(bool force = false, bool updaterRun = false)
        {
#if CATALOG_DOWNLOAD
            // Downloaded catalog is always newer than, or same as, the previously downloaded and bundled catalogs, so no need to load those after succesful download.
            Catalog downloadedCatalog = Download(force);
#else
            Catalog downloadedCatalog = null;
#endif
            Catalog previouslyDownloadedCatalog = downloadedCatalog == null ? LoadPreviouslyDownloaded() : null;
            Catalog bundledCatalog = downloadedCatalog == null ? LoadBundled() : null;
            Catalog newestCatalog;
            if (updaterRun)
            {
                Catalog webCrawlerCatalog = LoadWebCrawlerCatalog();
                Catalog updaterCatalog = LoadUpdaterCatalog();
                newestCatalog = Newest(Newest(Newest(downloadedCatalog, previouslyDownloadedCatalog), Newest(bundledCatalog, updaterCatalog)), webCrawlerCatalog);
            }
            else
            {
                newestCatalog = Newest(Newest(downloadedCatalog, previouslyDownloadedCatalog), bundledCatalog);
            }

            if (newestCatalog != null)
            {
                newestCatalog.CreateIndexes();

                SettingsUIText = $"<color { ModSettings.SettingsUIColor }>{ newestCatalog.VersionString() }</color>, created on { newestCatalog.Updated:d MMM yyyy}" +
                    $", downloaded on { newestCatalog.Downloaded:d MMM yyyy}";

                Logger.Log($"Using catalog { newestCatalog.VersionString() }, created on { newestCatalog.Updated.ToLocalTime().ToLongDateString() }. " +
                    $"Catalog contains { newestCatalog.ReviewedModCount } reviewed mods with { newestCatalog.Compatibilities.Count } compatibilities and " +
                    $"{ newestCatalog.Mods.Count - newestCatalog.ReviewedModCount } mods with basic information.");
                
                SettingsManager.NotifyCatalogUpdated();
            }

            return newestCatalog;
        }
        
#if CATALOG_DOWNLOAD
        /// <summary>Downloads a new catalog and loads it into memory.</summary>
        /// <remarks>A download will only be started once per session. On download errors, the download will be retried immediately a few times.</remarks>
        /// <returns>A reference to the catalog, or null if the download failed.</returns>
        private static Catalog Download(bool force = false)
        {
            int downloadFrequency = GlobalConfig.Instance.GeneralConfig.DownloadFrequency;
            if (!force)
            {
                // once a week or never(on demand)
                try
                {
                    FileInfo previouslyDownloadedCatalog = new FileInfo(Path.Combine(ModSettings.WorkPath, ModSettings.DownloadedCatalogFileName));
                    if (!Toolkit.IsOlderThanFrequencyOption(previouslyDownloadedCatalog.LastWriteTimeUtc, downloadFrequency))
                    {
                        // Not enough time passed before last update
                        Logger.Log($"Catalog update skipped. Selected frequency: {GeneralConfig.DownloadFrequencyToString(downloadFrequency)}, last update: {previouslyDownloadedCatalog.LastWriteTimeUtc}");
                        return null;
                    }
                }
                catch (Exception e)
                {
                    // catalog file probably does not exist, continue
                    Logger.Exception(e);  
                }
            }

            if (!force && DownloadStarted)
            {
                return null;
            }

            DownloadStarted = true;

            string temporaryFile = Path.Combine(ModSettings.WorkPath, $"{ ModSettings.DownloadedCatalogFileName }.tmp");

            if (!Toolkit.DeleteFile(temporaryFile))
            {
                Logger.Log("Partially downloaded catalog still exists from a previous session and can't be deleted. This prevents a new download.", Logger.Error);
                return null;
            }

            Catalog downloadedCatalog = null;
            Stopwatch timer = Stopwatch.StartNew();

            if (!Toolkit.Download(ModSettings.CatalogUrl, temporaryFile))
            {
                Logger.Log($"Can't download new catalog from { ModSettings.CatalogUrl }", Logger.Warning);
            }
            else
            {
                timer.Stop();
                downloadedCatalog = LoadFromDisk(temporaryFile);

                if (downloadedCatalog == null)
                {
                    Logger.Log($"Can't load newly downloaded catalog (download time { Toolkit.TimeString(timer.ElapsedMilliseconds) }).", Logger.Error);
                }
                else
                {
                    Logger.Log($"Catalog version { downloadedCatalog.VersionString() } downloaded in { Toolkit.TimeString(timer.ElapsedMilliseconds) }.");

                    // Copy the temporary file over the previously downloaded catalog.
                    Toolkit.CopyFile(temporaryFile, Path.Combine(ModSettings.WorkPath, ModSettings.DownloadedCatalogFileName));

                    DownloadSuccessful = true;
                }
            }

            Toolkit.DeleteFile(temporaryFile);

            return downloadedCatalog;
        }
#endif

        /// <summary>Loads the previously downloaded catalog.</summary>
        /// <returns>A reference to the catalog, or null if loading failed.</returns>
        private static Catalog LoadPreviouslyDownloaded()
        {
            string previouslyDownloadedFullPath = Path.Combine(ModSettings.WorkPath, ModSettings.DownloadedCatalogFileName);
            string oldPreviouslyDownloadedFullPath = Path.Combine(ModSettings.WorkPath, ModSettings.OldDownloadedCatalogFileName);

            if (File.Exists(previouslyDownloadedFullPath))
            {
                // Delete old file with previously used filename.
                Toolkit.DeleteFile(oldPreviouslyDownloadedFullPath);
            }
            else if (File.Exists(oldPreviouslyDownloadedFullPath))
            {
                previouslyDownloadedFullPath = oldPreviouslyDownloadedFullPath;
            }
            else
            {
                return null;
            }

            Catalog previouslyDownloadedCatalog = LoadFromDisk(previouslyDownloadedFullPath);

            if (previouslyDownloadedCatalog == null)
            {
                if (Toolkit.DeleteFile(previouslyDownloadedFullPath))
                {
                    Logger.Log("Can't load previously downloaded catalog. Deleted it now.", Logger.Debug);
                }
                else
                {
                    Logger.Log("Can't load previously downloaded catalog and it can't be deleted either. This prevents saving a newly downloaded catalog.", Logger.Warning);
                }
            }
            else
            {
                Logger.Log($"Previously downloaded catalog is version { previouslyDownloadedCatalog.VersionString() }.");
            }

            return previouslyDownloadedCatalog;
        }


        /// <summary>Loads the bundled catalog.</summary>
        /// <remarks>The bundled catalog is the catalog that comes with this mod, downloaded from the Steam Workshop on subscribe and every mod update.</remarks>
        /// <returns>A reference to the catalog, or null if loading failed.</returns>
        private static Catalog LoadBundled()
        {
            if (!File.Exists(ModSettings.BundledCatalogFullPath))
            {
                Logger.Log($"No bundled catalog found. { ModSettings.PleaseReportText }", Logger.Error);
                return null;
            }

            Catalog bundledCatalog = LoadFromDisk(ModSettings.BundledCatalogFullPath);

            if (bundledCatalog == null)
            {
                Logger.Log($"Can't load bundled catalog. { ModSettings.PleaseReportText }", Logger.Error);
            }
            else
            {
                Logger.Log($"Bundled catalog is version { bundledCatalog.VersionString() }.");
            }

            return bundledCatalog;
        }


        /// <summary>Loads the updater catalog.</summary>
        /// <remarks>An updater catalog only exists for maintainers of this mod that run the Updater. If more than one updater catalog exists, 
        ///          only the last one in an alphabetically sorted list will be loaded.</remarks>
        /// <returns>A reference to the catalog, or null if loading failed.</returns>
        private static Catalog LoadUpdaterCatalog()
        {
            if (!ModSettings.UpdaterAvailable)
            {
                return null;
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog*.xml*");
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

            string catalogPath = files.Last();
            Logger.Log($"Loading Updater Catalog from: {catalogPath}");
            
            Catalog catalog = LoadFromDisk(catalogPath);

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
        
        /// <summary>Loads the web crawler dump catalog.</summary>
        /// <remarks>A web crawler catalog only exists for maintainers of this mod that run the Web Crawler.</remarks>
        /// <returns>A reference to the web crawler dump catalog, or null if loading failed.</returns>
        private static Catalog LoadWebCrawlerCatalog()
        {
            if (!ModSettings.UpdaterAvailable)
            {
                return null;
            }

            string[] files;

            try
            {
                files = Directory.GetFiles(ModSettings.UpdaterPath, $"{ ModSettings.CatalogDumpFileName }");
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

            string catalogPath = files.Last();
            Logger.Log($"Loading WebCrawler Catalog from: {catalogPath}");
            
            Catalog catalog = LoadFromDisk(catalogPath);

            if (catalog == null)
            {
                Logger.Log("Can't load WebCrawler catalog.", Logger.Error);
            }
            else
            {
                Logger.Log($"WebCrawler catalog is version { catalog.VersionString() }.");
            }

            return catalog;
        }


        /// <summary>Determines which of two catalogs has the highest version.</summary>
        /// <remarks>If both catalogs are the same version, the first supplied catalog is chosen.</remarks>
        /// <returns>A reference to the catalog with the highest version, or null if both catalogs are null.</returns>
        private static Catalog Newest(Catalog catalog1, Catalog catalog2)
        {
            return (catalog1 == null || catalog2 == null) ? catalog1 ?? catalog2 : (catalog1.Version >= catalog2.Version) ? catalog1 : catalog2;
        }


        /// <summary>Creates the indexes for mods, groups or authors, to allow for faster lookup. Also counts the number of mods with a manual review.</summary>
        /// <remarks>Mods that are incompatible according to the Steam Workshop count as reviewed.</remarks>
        private void CreateIndexes()
        {
            foreach (Mod catalogMod in Mods)
            {
                if (!modIndex.ContainsKey(catalogMod.SteamID))
                {
                    modIndex.Add(catalogMod.SteamID, catalogMod);

                    if (catalogMod.Stability > Enums.Stability.NotReviewed)
                    {
                        ReviewedModCount++;
                    }
                }
            }

            foreach (Group catalogGroup in Groups)
            {
                if (!groupIndex.ContainsKey(catalogGroup.GroupID))
                {
                    groupIndex.Add(catalogGroup.GroupID, catalogGroup);
                }
            }

            foreach (Author catalogAuthor in Authors)
            {
                if (catalogAuthor.SteamID != 0 && !authorIDIndex.ContainsKey(catalogAuthor.SteamID))
                {
                    authorIDIndex.Add(catalogAuthor.SteamID, catalogAuthor);
                }

                if (!string.IsNullOrEmpty(catalogAuthor.CustomUrl) && !authorUrlIndex.ContainsKey(catalogAuthor.CustomUrl))
                {
                    authorUrlIndex.Add(catalogAuthor.CustomUrl, catalogAuthor);
                }
            }
        }
    }
}
