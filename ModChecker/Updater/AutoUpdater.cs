using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ModChecker.DataTypes;
using ModChecker.Util;


// Auto updater updates the catalog with information from the Steam Workshop pages. Only the following is updated/added:
// * Mod: name, author, publish/update dates, source url, compatible game version, required DLC, required mods, only-needed-for mods (update only, not for new mods),
//        statuses: incompatible according to the workshop, removed from workshop, no description, no source available (only when a source url is found)
// * Author: name, last seen (only based on mod updates), retired (only based on mod updates)
// Note: a change in author ID will be seen as a new author


namespace ModChecker.Updater
{
    // This class has limited error handling because the auto updater is not for regular users
    internal static class AutoUpdater
    {
        // Dictionaries to collect info from the Steam Workshop
        private static readonly Dictionary<ulong, Mod> collectedModInfo = new Dictionary<ulong, Mod>();
        private static readonly Dictionary<ulong, Author> collectedAuthorIDs = new Dictionary<ulong, Author>();
        private static readonly Dictionary<string, Author> collectedAuthorURLs = new Dictionary<string, Author>();

        // Change notes, separate parts and combined
        private static StringBuilder ChangeNotesNew;
        private static StringBuilder ChangeNotesUpdated;
        private static StringBuilder ChangeNotesRemoved;
        private static string ChangeNotes;

        // [Todo 0.2] temporary, should be removed after first catalog is created
        private const uint maxNewModDownloads = 3000;

        // Date and time of this update
        private static DateTime UpdateDate;

        // Start the auto updater; will download Steam webpages, extract info, update the active catalog and save it with a new version; includes change notes
        internal static bool Start(uint maxKnownModDownloads)
        {
            // Exit if the updater is not enabled in settings, or if we can't get an active catalog
            if (!ModSettings.updaterEnabled || !ActiveCatalog.Init())
            {
                return false;
            }

            bool success = false;

            // Get basic mod and author information from the Steam Workshop mod listing pages
            if (GetBasicInfo())
            {
                // Get detailed information from the individual mod pages
                if (GetDetails(maxKnownModDownloads))
                {
                    // Update the catalog with the new info
                    if (UpdateCatalog())
                    {
                        // The filename for the new catalog and related files ('ModCheckerCatalog_v1.0001')
                        string partialPath = Path.Combine(ModSettings.UpdatedCatalogPath, $"{ ModSettings.internalName }Catalog_v{ ActiveCatalog.Instance.VersionString() }");

                        // Save the new catalog
                        if (ActiveCatalog.Instance.Save(partialPath + ".xml"))
                        {
                            // Save change notes, in the same folder as the new catalog
                            Tools.SaveToFile(ChangeNotes.ToString(), partialPath + "_ChangeNotes.txt");

                            // Copy the updater logfile to the same folder as the new catalog
                            Tools.CopyFile(ModSettings.updaterLogfileFullPath, partialPath + ".log");

                            success = true;
                        }

                        // Close and reopen the active catalog, because we made changes to it
                        Logger.Log("Closing and reopening the active catalog.");

                        ActiveCatalog.Close();

                        ActiveCatalog.Init();
                    }
                    else
                    {
                        success = true;
                        
                        Logger.UpdaterLog("No changed or new mods detected on the Steam Workshop.");
                    }
                }                    
            }

            Logger.UpdaterLog("Auto updater has shutdown.", extraLine: true, duplicateToRegularLog: true);

            // Empty the dictionaries and change notes to free memory
            collectedModInfo.Clear();
            collectedAuthorIDs.Clear();
            collectedAuthorURLs.Clear();
            ChangeNotes = null;
            ChangeNotesNew = null;
            ChangeNotesUpdated = null;
            ChangeNotesRemoved = null;

            return success;
        }
        

        // Get mod and author names and IDs from the Steam Workshop mod listing pages
        private static bool GetBasicInfo()
        {
            // Time the download and processing
            Stopwatch timer = Stopwatch.StartNew();

            Logger.Log("Auto updater started downloading Steam Workshop mod listing pages. This should take less than 1 minute. See separate logfile for details.");
            Logger.UpdaterLog("Auto updater started downloading Steam Workshop mod listing pages. This should take less than 1 minute.");

            uint totalPages = 0;

            // Go through the different mod listings: mods and camera scripts, both regular and incompatible
            foreach (string steamURL in ModSettings.SteamModListingURLs)
            {
                Logger.UpdaterLog($"Starting downloading from { steamURL }");
                
                uint pageNumber = 0;

                // Download and read pages until we find no more pages, or we reach the set maximum number of pages (to avoid missing the mark and continuing for eternity)
                while (pageNumber < ModSettings.SteamMaxModListingPages)
                {
                    // Increase the pagenumber and download a page
                    pageNumber++;

                    Exception ex = Tools.Download($"{ steamURL }&p={ pageNumber }", ModSettings.SteamWebpageFullPath);

                    if (ex != null)
                    {
                        Logger.UpdaterLog($"Download process stopped due to permanent error while downloading { steamURL }&p={ pageNumber }", Logger.error);

                        Logger.Exception(ex, toUpdaterLog: true);

                        // Decrease the pageNumber to the last succesful page
                        pageNumber--;
                        
                        // Stop downloading pages for this type of mod listing
                        break;
                    }

                    // Extract mod and author info from the downloaded page
                    uint modsFoundThisPage = ReadModListingPage(steamURL.Contains("incompatible"));

                    if (modsFoundThisPage == 0)
                    {
                        // No mods found on this page; decrease the page number to the last succesful page
                        pageNumber--;

                        // Stop downloading pages for this type of mod listing
                        break;
                    }

                    Logger.UpdaterLog($"Found { modsFoundThisPage } mods on page { pageNumber }.");
                }

                totalPages += pageNumber;
            }

            // Delete the temporary file
            Tools.DeleteFile(ModSettings.SteamWebpageFullPath);

            // Log the elapsed time; note: >95% of process time is download; skipping the first 65KB (900+ lines) with 'reader.Basestream.Seek' does nothing for speed
            timer.Stop();

            Logger.UpdaterLog($"Auto updater finished checking { totalPages } Steam Workshop mod list pages in { (double)timer.ElapsedMilliseconds / 1000:F1} seconds. " + 
                $"{ collectedModInfo.Count } mods and { collectedAuthorIDs.Count + collectedAuthorURLs.Count } authors found.", duplicateToRegularLog: true);

            return (totalPages > 0) && (collectedModInfo.Count > 0);
        }


        // Extract mod and author info from the downloaded mod listing page; return false if no mods were found on this page
        private static uint ReadModListingPage(bool incompatible)
        {
            uint modsFoundThisPage = 0;

            string line;

            // Read the downloaded file
            using (StreamReader reader = File.OpenText(ModSettings.SteamWebpageFullPath))
            {
                // Read all the lines until the end of the file
                while ((line = reader.ReadLine()) != null)
                {
                    // Search for the identifying string for the next mod; continue with next line if not found
                    if (!line.Contains(ModSettings.SteamModListingModFind))
                    {
                        continue;
                    }

                    // Found the identifying string; get the Steam ID
                    ulong steamID;

                    try
                    {
                        steamID = Convert.ToUInt64(Tools.MidString(line, ModSettings.SteamModListingModIDLeft, ModSettings.SteamModListingModIDRight));
                    }
                    catch
                    {
                        // If the Steam ID was not recognized, continue with the next lines
                        Logger.UpdaterLog("Steam ID not recognized on HTML line: " + line, Logger.warning);

                        continue;
                    }

                    // Get the mod name
                    string name = Tools.MidString(line, ModSettings.SteamModListingModNameLeft, ModSettings.SteamModListingModNameRight);

                    // Try to get the author ID and custom URL from the next line; only one will exist
                    line = reader.ReadLine();

                    ulong authorID;

                    try
                    {
                        authorID = Convert.ToUInt64(Tools.MidString(line, ModSettings.SteamModListingAuthorIDLeft, ModSettings.SteamModListingAuthorRight));
                    }
                    catch
                    {
                        // Author ID not found
                        authorID = 0;
                    }                    

                    // Author URL will be empty if not found
                    string authorURL = Tools.MidString(line, ModSettings.SteamModListingAuthorURLLeft, ModSettings.SteamModListingAuthorRight);
                    
                    // Get the author name
                    string authorName = Tools.MidString(line, ModSettings.SteamModListingAuthorNameLeft, ModSettings.SteamModListingAuthorNameRight);

                    // Add the mod to the dictionary; avoid duplicates (could happen if a new mod is published in the time of downloading all pages)
                    if (!collectedModInfo.ContainsKey(steamID))
                    {
                        Mod mod = new Mod(steamID, name, authorID, authorURL);

                        if (incompatible)
                        {
                            // Assign the incompatible status if we got the mod from an 'incompatible' mod listing page
                            mod.Update(statuses: new List<Enums.ModStatus> { Enums.ModStatus.IncompatibleAccordingToWorkshop });
                        }

                        collectedModInfo.Add(steamID, mod);

                        modsFoundThisPage++;
                    }

                    // Add the author to one of the dictionaries; avoid duplicates
                    if ((authorID != 0) && !collectedAuthorIDs.ContainsKey(authorID))
                    {
                        collectedAuthorIDs.Add(authorID, new Author(authorID, "", authorName));
                    }

                    if (!string.IsNullOrEmpty(authorURL))
                    {
                        if (!collectedAuthorURLs.ContainsKey(authorURL))
                        {
                            collectedAuthorURLs.Add(authorURL, new Author(0, authorURL, authorName));
                        }
                    }
                }
            }

            return modsFoundThisPage;
        }


        // Get mod information from the individual mod pages on the Steam Workshop
        private static bool GetDetails(uint maxKnownModDownloads)
        {
            // Time the download and processing
            Stopwatch timer = Stopwatch.StartNew();

            // Estimate is two downloads per second; [Todo 0.2] estimate should calculate with new mods found
            double estimate = Math.Ceiling(0.5 * Math.Min(maxKnownModDownloads + maxNewModDownloads, collectedModInfo.Count) / 60);

            Logger.UpdaterLog($"Auto updater started checking individual Steam Workshop mod pages. This should take less than { estimate } minutes.",
                duplicateToRegularLog: true);

            // Initialize counters
            uint newModsDownloaded = 0;
            uint knownModsDownloaded = 0;
            uint modsFound = 0;
            uint failedDownloads = 0;


            // Check all mods we gathered, one by one; [Todo 0.2] add a random starting point, to randomize which known mods will be updated
            foreach (ulong steamID in collectedModInfo.Keys)
            {
                // New mod or a mod already in the catalog
                bool newMod = !ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID);
                
                // Stop if we reached the maximum number of both types of mods; continue with the next Steam ID if we only reached the maximum for this type of mod
                if ((newModsDownloaded >= maxNewModDownloads) && (knownModsDownloaded >= maxKnownModDownloads))
                {
                    break;
                }
                else if ((newMod && (newModsDownloaded >= maxNewModDownloads)) || (!newMod && (knownModsDownloaded >= maxKnownModDownloads)))
                {
                    continue;
                }

                // Download the Steam Workshop mod page
                if (Tools.Download(Tools.GetWorkshopURL(steamID), ModSettings.SteamWebpageFullPath) != null)
                {
                    // Download error
                    failedDownloads++;

                    if (failedDownloads <= ModSettings.SteamMaxFailedPages)
                    {
                        // Download error might be mod specific. Go to the next mod.
                        Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for { collectedModInfo[steamID].ToString() }. " + 
                            "Will continue with next mod.", Logger.warning);

                        continue;
                    }
                    else
                    {
                        // Too many failed downloads. Stop downloading
                        Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for { collectedModInfo[steamID].ToString() }. " + 
                            "Download process stopped.", Logger.error);

                        break;
                    }
                }

                // Page downloaded, increase the counter
                if (newMod)
                {
                    newModsDownloaded++;
                }
                else
                {
                    knownModsDownloaded++;
                }

                // Extract detailed info from the downloaded page
                if (ReadModPage(steamID))
                {
                    modsFound++;
                }
                else
                {
                    Logger.UpdaterLog($"Can't find the Steam ID on downloaded page for { collectedModInfo[steamID].ToString() }. Mod info not updated.", Logger.error);
                }
            }

            // Delete the temporary file
            Tools.DeleteFile(ModSettings.SteamWebpageFullPath);

            // Log the elapsed time
            timer.Stop();

            Logger.UpdaterLog($"Auto updater finished checking { modsFound } individual Steam Workshop mod pages in " + 
                $"{ (float)timer.ElapsedMilliseconds / 1000:F1} seconds.", duplicateToRegularLog: true);

            // return true if we downloaded at least one mod, or we were not allowed to download any
            return (knownModsDownloaded + newModsDownloaded) > 0 || maxKnownModDownloads == 0;
        }


        // Extract detailed mod information from the downloaded mod page; return false if the Steam ID can't be found on this page
        private static bool ReadModPage(ulong steamID)
        {
            // Get the mod
            Mod mod = collectedModInfo[steamID];

            // Keep track if we find the correct Steam ID on this page, to avoid messing up one mod with another mods info
            bool steamIDmatched = false;

            // Read the page back from file
            string line;

            using (StreamReader reader = File.OpenText(ModSettings.SteamWebpageFullPath))
            {
                // Read all the lines until the end of the file
                while ((line = reader.ReadLine()) != null)
                {
                    // First find the correct Steam ID on this page; it appears before all other info
                    if (!steamIDmatched)
                    {
                        steamIDmatched = line.Contains(ModSettings.SteamModPageSteamID + steamID.ToString());

                        // Don't update anything if we don't find the Steam ID first
                        continue;
                    }
                    
                    // Compatible game version tag
                    if (line.Contains(ModSettings.SteamModPageVersionTagFind))
                    {
                        // Get the version as string
                        string gameVersion = Tools.MidString(line, ModSettings.SteamModPageVersionTagLeft, ModSettings.SteamModPageVersionTagRight);

                        // Update the mod, but first convert the gameversion string back and forth to ensure a correctly formatted string
                        mod.Update(compatibleGameVersionString: GameVersion.Formatted(Tools.ConvertToGameVersion(gameVersion)));
                    }
                    // Publish and update dates; also update author last seen date and retired state
                    else if (line.Contains(ModSettings.SteamModPageDatesFind))
                    {
                        // Skip two lines
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        // Get the publish date
                        DateTime published = Tools.ConvertWorkshopDateTime(Tools.MidString(line, ModSettings.SteamModPageDatesLeft, ModSettings.SteamModPageDatesRight));

                        // Skip another line
                        line = reader.ReadLine();

                        // Get the update date, if available; 
                        DateTime updated = Tools.ConvertWorkshopDateTime(Tools.MidString(line, ModSettings.SteamModPageDatesLeft, ModSettings.SteamModPageDatesRight));

                        // Update the mod with both dates
                        mod.Update(published: published, updated: updated);

                        // If the updated date is more recent than the author's last seen date, set the latter to the former and reset the authors retired state
                        Author author = mod.AuthorID != 0 ? collectedAuthorIDs[mod.AuthorID] : 
                            !string.IsNullOrEmpty(mod.AuthorURL) ? collectedAuthorURLs[mod.AuthorURL] : null;

                        if (author != null)
                        {
                            if (mod.Updated > author.LastSeen)
                            {
                                author.Update(lastSeen: mod.Updated, retired: false);
                            }
                        }
                    }
                    // Required DLC
                    else if (line.Contains(ModSettings.SteamModPageRequiredDLCFind))
                    {
                        // Skip one line
                        line = reader.ReadLine();

                        // Get the required DLC
                        string dlcString = Tools.MidString(line, ModSettings.SteamModPageRequiredDLCLeft, ModSettings.SteamModPageRequiredDLCRight);

                        // Update the mod
                        if (!string.IsNullOrEmpty(dlcString))
                        {
                            try
                            {
                                // Convert the dlc string to number to enum and add it
                                mod.RequiredDLC.Add((Enums.DLC)Convert.ToUInt32(dlcString));
                            }
                            catch
                            {
                                Logger.UpdaterLog($"Cannot convert \"{ dlcString }\" to DLC enum for { mod.ToString() }.", Logger.warning);
                            }
                        }
                    }
                    // Required mods
                    else if (line.Contains(ModSettings.SteamModPageRequiredModFind))
                    {
                        // Skip one line
                        line = reader.ReadLine();

                        // Get the required mods Steam ID
                        string requiredString = Tools.MidString(line, ModSettings.SteamModPageRequiredModLeft, ModSettings.SteamModPageRequiredModRight);

                        try
                        {
                            ulong requiredID = Convert.ToUInt64(requiredString);

                            // Update the mod
                            mod.RequiredMods.Add(requiredID);
                        }
                        catch
                        {
                            Logger.UpdaterLog($"Steam ID not recognized for required mod: { requiredString }.", Logger.warning);
                        }                        
                    }
                    // Description - check for 'no description' status and source url
                    else if (line.Contains(ModSettings.SteamModPageDescriptionFind))
                    {
                        // Skip one line
                        line = reader.ReadLine();

                        // Get the description length: the number of characters between the left and right boundary texts
                        int descriptionLength = line.Length -
                            line.IndexOf(ModSettings.SteamModPageDescriptionLeft) - ModSettings.SteamModPageDescriptionLeft.Length -
                            ModSettings.SteamModPageDescriptionRight.Length;

                        // Tag as 'no description' if the description is not at least a few characters longer than the mod name
                        if (descriptionLength <= mod.Name.Length + 3)
                        {
                            mod.Statuses.Add(Enums.ModStatus.NoDescription);
                        }

                        // Get the source url, if any
                        if (line.Contains(ModSettings.SteamModPageSourceURLLeft))
                        {
                            mod.Update(sourceURL: GetSourceURL(line, steamID));
                        }

                        // Description is the last info we need from the page, so exit the while loop
                        break;
                    }
                }
            }

            if (steamIDmatched)
            {
                // Indicate we checked details for this mod
                mod.Update(catalogRemark: "Details checked");

                return true;
            }
            else
            {
                return false;
            }
        }


        // Get the source URL; if more than one is found, pick the most likely
        private static string GetSourceURL(string line, ulong steamID)
        {
            string sourceURL = Tools.MidString(line, ModSettings.SteamModPageSourceURLLeft, ModSettings.SteamModPageSourceURLRight);

            // Exit if we find none
            if (string.IsNullOrEmpty(sourceURL))
            {
                return null;
            }

            // Complete the url and get ready to find another
            sourceURL = "https://github.com/" + sourceURL;

            string secondSourceURL;

            uint tries = 0;

            // Keep comparing source url's until we find a good enough one or we find no more; max. 50 times to avoid infinite loops on code errors
            while (line.IndexOf(ModSettings.SteamModPageSourceURLLeft) != line.LastIndexOf(ModSettings.SteamModPageSourceURLLeft) && tries < 50)
            {
                tries++;

                // Set the start the string to just after the first occurrence
                int index = line.IndexOf(ModSettings.SteamModPageSourceURLLeft);
                line = line.Substring(index + 1, line.Length - index - 1);

                // Get the second source url
                secondSourceURL = Tools.MidString(line, ModSettings.SteamModPageSourceURLLeft, ModSettings.SteamModPageSourceURLRight);

                // Decide which source url to use if a second source url was found
                if (!string.IsNullOrEmpty(secondSourceURL))
                {
                    secondSourceURL = "https://github.com/" + secondSourceURL;

                    // Do nothing if both source url's are identical
                    if (sourceURL != secondSourceURL)
                    {
                        string lower = sourceURL.ToLower();

                        // We want an url that doesn't contain 'issue', 'wiki', 'documentation', 'readme', 'guide' or 'translation'
                        // Also skip pardeike's Harmony and Sschoener's cities-skylines-detour
                        if (lower.Contains("issue") || lower.Contains("wiki") || lower.Contains("documentation") || lower.Contains("readme") || lower.Contains("guide")
                            || lower.Contains("translation") || lower.Contains("https://github.com/pardeike")
                            || lower.Contains("https://github.com/sschoener/cities-skylines-detour"))
                        {
                            // Keep the new
                            Logger.UpdaterLog($"Found multiple source url's for [{ steamID, 10 }]: \"{ secondSourceURL }\" (kept) and \"{ sourceURL }\" (discarded)");

                            sourceURL = secondSourceURL;
                        }
                        else
                        {
                            // Keep the previous; keep finding the rest for complete logging of all source url's found
                            Logger.UpdaterLog($"Found multiple source url's for [{ steamID }]: \"{ sourceURL }\" (kept) and \"{ secondSourceURL }\" (discarded)");
                        }
                    }
                }
            }

            return sourceURL;
        }


        // [Todo 0.2] Add check for exclusions; also add exclusions dictionary in catalog; Exclusions needed for:
        // * source url of 2414618415, 2139980554, and more
        // *     was also: 2071210858, 1957515502, 1806759255, 1776052533, 1637663252, 1386697922, 1322787091, 958161597, 938049744, and more

        // Update the active catalog with the found information
        private static bool UpdateCatalog()
        {
            UpdateDate = DateTime.Now;

            ChangeNotesNew = new StringBuilder();
            ChangeNotesUpdated = new StringBuilder();
            ChangeNotesRemoved = new StringBuilder();

            // Add or update all found mods
            foreach (ulong steamID in collectedModInfo.Keys)
            {
                // Get the found mod
                Mod collectedMod = collectedModInfo[steamID];

                // Clean out assets from the required mods list
                List<ulong> removalList = new List<ulong>();

                foreach (ulong requiredID in collectedMod.RequiredMods)
                {
                    // Remove the required ID if we didn't find it on the Workshop; ignore builtin required mods; [Todo 0.2] include groups
                    if (!collectedModInfo.ContainsKey(requiredID) && (requiredID > ModSettings.highestFakeID))
                    {
                        // We can't remove it here directly, because the RequiredMods list is used in the foreach loop, so we just collect here and (re)move below
                        removalList.Add(requiredID);

                        // Don't log if it's a known asset to ignore
                        if (!ModSettings.RequiredIDsToIgnore.Contains(requiredID))
                        {
                            Logger.UpdaterLog($"Required item [Steam ID { requiredID, 10 }] not found, probably an asset. For { collectedMod.ToString(cutOff: false) }.");
                        }                        
                    }
                }

                foreach (ulong requiredID in removalList)
                {
                    // Now move the above collected IDs to the asset list
                    collectedMod.RequiredMods.Remove(requiredID);

                    collectedMod.RequiredAssets.Add(requiredID);
                }

                // Clean up compatible gameversion
                if (collectedMod.CompatibleGameVersionString == null)
                {
                    collectedMod.Update(compatibleGameVersionString: GameVersion.Formatted(GameVersion.Unknown));
                }

                // Add or update the mod in the catalog
                if (!ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID))
                {
                    // New mod; add to the catalog
                    AddMod(steamID);
                }
                else
                {
                    // Known mod; update all info
                    UpdateMod(steamID);
                }
            }
            
            // Add or update all found authors, by author ID
            foreach (ulong authorID in collectedAuthorIDs.Keys)
            {
                if (!ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(authorID))
                {
                    // New author; add to the catalog
                    Author collectedAuthor = collectedAuthorIDs[authorID];

                    AddAuthor(collectedAuthor);
                }
                else
                {
                    // Known author
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[authorID];

                    Author collectedAuthor = collectedAuthorIDs[authorID];

                    // Update all info
                    UpdateAuthor(catalogAuthor, collectedAuthor);
                }
            }

            // Add or update all found authors, by author custom URL
            foreach (string authorURL in collectedAuthorURLs.Keys)
            {
                if (!ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(authorURL))
                {
                    // New author; add to the catalog
                    Author collectedAuthor = collectedAuthorURLs[authorURL];

                    AddAuthor(collectedAuthor);
                }
                else
                {
                    // Known author
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[authorURL];

                    Author collectedAuthor = collectedAuthorURLs[authorURL];

                    // Update all info
                    UpdateAuthor(catalogAuthor, collectedAuthor);
                }
            }

            // Mods no longer available in the Steam Workshop
            foreach (ulong steamID in ActiveCatalog.Instance.ModDictionary.Keys)
            {
                Mod catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];

                // Ignore mods we just found, and local and builtin mods, and mods that already have the 'removed' status
                if (!collectedModInfo.ContainsKey(steamID) && (steamID > ModSettings.highestFakeID) && !catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.RemovedFromWorkshop);

                    catalogMod.Update(catalogRemark: $"AutoUpdated as removed on { UpdateDate.ToShortDateString() }.");

                    ChangeNotesRemoved.AppendLine("Mod no longer available on the workshop: " + catalogMod.ToString(cutOff: false));
                }
            }

            /* [Todo 0.2] Rethink this. It doesn't work well, since Steam sometimes randomly gives the ID link instead of the URL link in mod listing pages

            // Authors no longer available in the Steam Workshop, by author ID
            foreach (ulong authorID in ActiveCatalog.Instance.AuthorIDDictionary.Keys)
            {
                // Ignore authors that already have the 'retired' status
                if (!collectedAuthorIDs.ContainsKey(authorID) && !ActiveCatalog.Instance.AuthorIDDictionary[authorID].Retired)
                {
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[authorID];
                    
                    catalogAuthor.Update(retired: true, catalogRemark: $"AutoUpdated as retired on { UpdateDate.ToShortDateString() }.");

                    ChangeNotesRemoved.AppendLine($"Author no longer has mods on the workshop: { ActiveCatalog.Instance.AuthorIDDictionary[authorID].Name }");
                }
            }

            // Authors no longer available in the Steam Workshop, by author custom URL
            foreach (string authorURL in ActiveCatalog.Instance.AuthorURLDictionary.Keys)
            {
                // Ignore authors that already have the 'retired' status
                if (!collectedAuthorURLs.ContainsKey(authorURL) && !ActiveCatalog.Instance.AuthorURLDictionary[authorURL].Retired)
                {
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[authorURL];

                    catalogAuthor.Update(retired: true, catalogRemark: $"AutoUpdated as retired on { UpdateDate.ToShortDateString() }.");

                    ChangeNotesRemoved.AppendLine($"Author no longer has mods on the workshop: { ActiveCatalog.Instance.AuthorURLDictionary[authorURL].Name }");
                }
            }
            */

            // Did we find any changes?
            if (ChangeNotesNew.Length == 0 && ChangeNotesUpdated.Length == 0 && ChangeNotesRemoved.Length == 0)
            {
                // Nothing changed
                return false;
            }
            else
            {
                // Increase the catalog version and update date
                ActiveCatalog.Instance.NewVersion(UpdateDate);

                // Combine the change notes
                ChangeNotes = $"Change Notes for Catalog { ActiveCatalog.Instance.VersionString() }\n" +
                    "-------------------------------\n" +
                    $"{ UpdateDate:D}, { UpdateDate:t}\n\n" +
                    "*** ADDED: ***\n" +
                    ChangeNotesNew.ToString() + "\n" +
                    "*** UPDATED: ***\n" +
                    ChangeNotesUpdated.ToString() + "\n" +
                    "*** REMOVED: ***\n" +
                    ChangeNotesRemoved.ToString();

                return true;
            }
        }


        // Add a new author to the catalog
        private static void AddAuthor(Author collectedAuthor)
        {
            ActiveCatalog.Instance.AddAuthor(collectedAuthor.ProfileID, collectedAuthor.CustomURL, collectedAuthor.Name, collectedAuthor.LastSeen, retired: false,
                catalogRemark: $"Added by AutoUpdater on { UpdateDate.ToShortDateString() }.");

            // Change notes
            ChangeNotesNew.AppendLine($"New author: { collectedAuthor.Name }");
        }


        // Update changed info for an author; return the change notes text
        private static void UpdateAuthor(Author catalogAuthor, Author collectedAuthor)
        {
            // Keep track of changes
            string changes = "";

            // Name
            if ((catalogAuthor.Name != collectedAuthor.Name) && !string.IsNullOrEmpty(collectedAuthor.Name))
            {
                catalogAuthor.Update(name: collectedAuthor.Name);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "name changed";
            }

            // Last seen and retired
            if (catalogAuthor.LastSeen < collectedAuthor.LastSeen)
            {
                if (catalogAuthor.Retired)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "no longer retired";
                }

                catalogAuthor.Update(lastSeen: collectedAuthor.LastSeen, retired: false);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'last seen' date updated";
            }

            // Change notes
            if (!string.IsNullOrEmpty(changes))
            {
                catalogAuthor.Update(catalogRemark: $"AutoUpdated on { UpdateDate.ToShortDateString() }: { changes }.");

                ChangeNotesUpdated.AppendLine($"Author { catalogAuthor.Name }: { changes }");
            }
        }
        
        
        // Add a new mod to the active catalog
        private static void AddMod(ulong steamID)
        {
            Mod collectedMod = collectedModInfo[steamID];

            Mod catalogMod = ActiveCatalog.Instance.AddMod(steamID);

            catalogMod.Update(collectedMod.Name, collectedMod.AuthorID, collectedMod.AuthorURL, collectedMod.Published, collectedMod.Updated, archiveURL: null, 
                collectedMod.SourceURL, collectedMod.CompatibleGameVersionString, collectedMod.RequiredDLC, collectedMod.RequiredMods, onlyNeededFor: null, 
                succeededBy: null, alternatives: null, collectedMod.RequiredAssets, collectedMod.Statuses, note: null, reviewUpdated: null, UpdateDate, 
                catalogRemark: $"Added by AutoUpdater on { UpdateDate.ToShortDateString() }.");

            // Change notes
            ChangeNotesNew.AppendLine($"New mod: { catalogMod.ToString(cutOff: false) }");
        }


        // Update a mod in the catalog with new info
        private static void UpdateMod(ulong steamID)
        {
            // Get a reference to the mod in the catalog and to the mod with the collected info
            Mod catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];

            Mod collectedMod = collectedModInfo[catalogMod.SteamID];

            // Did we check details for this mod?
            bool detailsChecked = collectedMod.CatalogRemark == "Details checked";

            // Keep track of changes
            string changes = "";

            // Name
            if ((catalogMod.Name != collectedMod.Name) && !string.IsNullOrEmpty(collectedMod.Name))
            {
                catalogMod.Update(name: collectedMod.Name);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "name changed";
            }

            // Author ID; only update if it was unknown; author ID can never changed and a mod can't change primary owner, so don't remove if we didn't find it anymore
            if ((catalogMod.AuthorID == 0) && (collectedMod.AuthorID != 0))
            {
                // Author ID found where there was none before
                catalogMod.Update(authorID: collectedMod.AuthorID);

                Logger.UpdaterLog($"Author ID found for { catalogMod.ToString() }");

                // [Todo 0.2] Add author id to other mods with the same Author URL (elsewhere in the updater after all collected mods are processed)
                // [Todo 0.2] Add author id to author (link through author URL)
            }

            // Author URL
            if (string.IsNullOrEmpty(catalogMod.AuthorURL) && !string.IsNullOrEmpty(collectedMod.AuthorURL))
            {
                // Author URL found where there was none before
                catalogMod.Update(authorURL: collectedMod.AuthorURL);

                Logger.UpdaterLog($"Author URL found for { catalogMod.ToString() }");
            }
            else if (catalogMod.AuthorURL != collectedMod.AuthorURL)
            {
                // Author URL has changed or was no longer found

                // [Todo 0.2] Finish this; use this to merge the two authors with old and new url (link through author ID if known)
                // also change author URL for every mod that has it; but how certain are we that this change is real? What if another author now uses this URL?

                // changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "author URL changed";

                Logger.UpdaterLog($"Author URL change detected (but not updated in the catalog) for { catalogMod.ToString() }; " + 
                    $"from \"{ catalogMod.AuthorURL }\" to \"{ collectedMod.AuthorURL }\".", Logger.debug);
            }

            // Published (only if details for this mod were checked)
            if (catalogMod.Published < collectedMod.Published && detailsChecked)
            {
                // No mention in the change notes, but log if the publish date was already a valid date
                if (catalogMod.Published != DateTime.MinValue)
                {
                    Logger.UpdaterLog($"Published date changed from { catalogMod.Published.ToShortDateString() } to { collectedMod.Published.ToShortDateString() }. " + 
                        $"This should not happen. Mod { catalogMod.ToString() }", Logger.warning);
                }

                catalogMod.Update(published: collectedMod.Published);
            }

            // Updated (only if details for this mod were checked)
            if (catalogMod.Updated < collectedMod.Updated && detailsChecked)
            {
                catalogMod.Update(updated: collectedMod.Updated);

                // Only mention in the change notes if it was really an update (and not a copy of the published date)
                if (catalogMod.Updated != catalogMod.Published)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "new update";
                }
            }

            // Source URL (only if details for this mod were checked)
            if (catalogMod.SourceURL != collectedMod.SourceURL && detailsChecked)
            {
                if (string.IsNullOrEmpty(catalogMod.SourceURL) && !string.IsNullOrEmpty(collectedMod.SourceURL))
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "source url found";

                    // Remove 'source unavailable' status
                    if (catalogMod.Statuses.Contains(Enums.ModStatus.SourceUnavailable))
                    {
                        catalogMod.Statuses.Remove(Enums.ModStatus.SourceUnavailable);

                        changes += " ('source unavailable' status removed)";
                    }
                }
                else if (string.IsNullOrEmpty(collectedMod.SourceURL) && !string.IsNullOrEmpty(catalogMod.SourceURL))
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "source url removed";
                }
                else if (!string.IsNullOrEmpty(collectedMod.SourceURL) && !string.IsNullOrEmpty(catalogMod.SourceURL))
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "source url changed";
                }

                catalogMod.Update(sourceURL: collectedMod.SourceURL);
            }

            // Compatible game version (only if details for this mod were checked)
            if (catalogMod.CompatibleGameVersionString != collectedMod.CompatibleGameVersionString && detailsChecked)
            {
                string unknown = GameVersion.Unknown.ToString();

                if (catalogMod.CompatibleGameVersionString == unknown && collectedMod.CompatibleGameVersionString != unknown)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "compatible game version tag found";
                }
                else if (catalogMod.CompatibleGameVersionString != unknown && collectedMod.CompatibleGameVersionString == unknown)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "compatible game version tag removed";
                }
                else if (catalogMod.CompatibleGameVersionString != unknown && collectedMod.CompatibleGameVersionString != unknown)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "compatible game version tag changed";
                }

                catalogMod.Update(compatibleGameVersionString: collectedMod.CompatibleGameVersionString);
            }

            // Required DLC (only if details for this mod were checked)
            if (catalogMod.RequiredDLC.ToString() != collectedMod.RequiredDLC.ToString() && detailsChecked)
            {
                // Add new required dlc
                foreach (Enums.DLC dlc in collectedMod.RequiredDLC)
                {
                    if (!catalogMod.RequiredDLC.Contains(dlc))
                    {
                        catalogMod.RequiredDLC.Add(dlc);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required dlc added ({ dlc })";
                    }
                }

                // Remove no longer required dlc
                foreach (Enums.DLC dlc in catalogMod.RequiredDLC)
                {
                    if (!collectedMod.RequiredDLC.Contains(dlc))
                    {
                        catalogMod.RequiredDLC.Remove(dlc);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required dlc removed ({ dlc })";
                    }
                }
            }

            // Required mods (only if details for this mod were checked), including updating existing NeededFor lists; [Todo 0.2] mod groups
            if (catalogMod.RequiredMods.ToString() != collectedMod.RequiredMods.ToString() && detailsChecked)
            {
                // Add new required mods
                foreach (ulong requiredID in collectedMod.RequiredMods)
                {
                    if (!catalogMod.RequiredMods.Contains(requiredID))
                    {
                        catalogMod.RequiredMods.Add(requiredID);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod added ({ requiredID })";
                    }
                }

                // Remove no longer needed mods
                foreach (ulong requiredID in catalogMod.RequiredMods)
                {
                    if (!collectedMod.RequiredMods.Contains(requiredID))
                    {
                        catalogMod.RequiredMods.Remove(requiredID);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod removed ({ requiredID })";

                        // Remove this mod from the previously required mods 'only needed for' list
                        if (ActiveCatalog.Instance.ModDictionary.ContainsKey(requiredID))
                        {
                            Mod requiredMod = ActiveCatalog.Instance.ModDictionary[requiredID];

                            if (requiredMod.NeededFor.Contains(steamID))
                            {
                                requiredMod.NeededFor.Remove(steamID);

                                ChangeNotesUpdated.AppendLine($"Mod { requiredMod.ToString(cutOff: false) }: removed { steamID } from 'only needed for' list");
                            }
                        }
                    }
                }
            }

            // Required assets (only if details for this mod were checked)
            if (catalogMod.RequiredAssets.ToString() != collectedMod.RequiredAssets.ToString() && detailsChecked)
            {
                // We're not really interested in these; just replace the list
                catalogMod.Update(requiredAssets: collectedMod.RequiredAssets);

                Logger.UpdaterLog($"Required assets changed for [{ steamID, 10 }]: { collectedMod.RequiredAssets }.", Logger.debug);
            }

            // Add new Statuses: incompatible, no description (only if details for this mod were checked)
            if (collectedMod.Statuses.Count > 0)
            {
                if (collectedMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop) &&
                    !catalogMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.IncompatibleAccordingToWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'IncompatibleAccordingToWorkshop' status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) && !catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailsChecked)
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'NoDescription' status added";
                }
            }

            // Remove Statuses: incompatible, no description (only if details for this mod were checked), removed from workshop
            if (catalogMod.Statuses.Count > 0)
            {
                // Remove statuses
                if (catalogMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop) && 
                    !collectedMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.IncompatibleAccordingToWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'IncompatibleAccordingToWorkshop' status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) && !collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailsChecked)
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'NoDescription' status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.RemovedFromWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'RemovedFromWorkshop' status removed";
                }

            }

            // Auto review update date, catalog remark and change notes
            if (!string.IsNullOrEmpty(changes))
            {
                catalogMod.Update(autoReviewUpdated: UpdateDate, catalogRemark: $"AutoUpdated on { UpdateDate.ToShortDateString() }: { changes }.");

                ChangeNotesUpdated.AppendLine($"Mod { catalogMod.ToString(cutOff: false) }: { changes }");
            }
        }
    }
}
