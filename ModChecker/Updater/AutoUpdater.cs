using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ModChecker.DataTypes;
using ModChecker.Util;


// Auto Updater updates the catalog with information from the Steam Workshop pages. The following is updated/added:
// * Mod: name, author, publish/update dates, source url, compatible game version, required DLC, required mods, only-needed-for mods (update existing field only),
//        statuses: incompatible according to the workshop, removed from workshop, no description, no source available (only remove when a source url is found)
// * Author: name, last seen (only based on mod updates), retired (only remove on mod updates)


namespace ModChecker.Updater
{
    internal static class AutoUpdater
    {
        // Did we run already this session (successful or not)
        private static bool hasRun;


        // Start the auto updater; will download Steam webpages, extract info, update the active catalog and save it with a new version; including change notes
        internal static void Start()
        {
            // Exit if we ran already, the auto updater is not enabled in settings, or if we can't get an active catalog
            if (hasRun || !ModSettings.AutoUpdaterEnabled || !ActiveCatalog.Init())
            {
                return;
            }

            hasRun = true;

            // Initialize the dictionaries we need
            CatalogUpdater.Init();

            // Get basic mod and author information from the Steam Workshop 'mod listing' pages; we always get this info for all mods and their authors
            if (GetBasicInfo())
            {
                // Add mods from the catalog that we didn't find, for the detail check below
                AddUnfoundMods();

                // Get detailed information from the individual mod pages; we get this info for all new mods and for a maximum number of known mods
                if (GetDetails())
                {
                    // Update the catalog with the new info and save it to a new version
                    CatalogUpdater.Start(autoUpdater: true);
                }                    
            }

            Logger.UpdaterLog("Auto Updater has shutdown.", extraLine: true, duplicateToRegularLog: true);
        }
        

        // Get mod and author names and IDs from the Steam Workshop 'mod listing' pages
        private static bool GetBasicInfo()
        {
            // Time the download and processing
            Stopwatch timer = Stopwatch.StartNew();

            Logger.Log("Auto Updater started downloading Steam Workshop 'mod listing' pages. This should take less than 1 minute. See separate logfile for details.");
            Logger.UpdaterLog("Auto Updater started downloading Steam Workshop 'mod listing' pages. This should take less than 1 minute.");

            uint totalPages = 0;

            // Go through the different mod listings: mods and camera scripts, both regular and incompatible
            foreach (string steamURL in ModSettings.steamModListingURLs)
            {
                Logger.UpdaterLog($"Starting downloads from { steamURL }");
                
                uint pageNumber = 0;

                // Download and read pages until we find no more pages, or we reach the set maximum number of pages (to avoid missing the mark and continuing for eternity)
                while (pageNumber < ModSettings.steamMaxModListingPages)
                {
                    // Increase the pagenumber and download a page
                    pageNumber++;

                    Exception ex = Toolkit.Download($"{ steamURL }&p={ pageNumber }", ModSettings.steamDownloadedPageFullPath);

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

                        // Log something if no mods were found at all
                        if (pageNumber == 0)
                        {
                            Logger.UpdaterLog("Found no mods on page 1");
                        }

                        // Stop downloading pages for this type of mod listing
                        break;
                    }

                    Logger.UpdaterLog($"Found { modsFoundThisPage } mods on page { pageNumber }.");
                }

                totalPages += pageNumber;
            }

            // Delete the temporary file
            Toolkit.DeleteFile(ModSettings.steamDownloadedPageFullPath);

            // Log the elapsed time; note: >95% is download time; skipping lines with 'reader.Basestream.Seek' or stopping after 30 mods does nothing for speed
            timer.Stop();

            Logger.UpdaterLog($"Auto Updater finished checking { totalPages } Steam Workshop 'mod listing' pages in " + 
                $"{ Toolkit.ElapsedTime(timer.ElapsedMilliseconds) }. { CatalogUpdater.CollectedModInfo.Count } mods and " + 
                $"{ CatalogUpdater.CollectedAuthorIDs.Count + CatalogUpdater.CollectedAuthorURLs.Count } authors found.", duplicateToRegularLog: true);

            return (totalPages > 0) && (CatalogUpdater.CollectedModInfo.Count > 0);
        }


        // Extract mod and author info from the downloaded mod listing page; returns the number of mods that were found on this page
        private static uint ReadModListingPage(bool incompatible)
        {
            uint modsFoundThisPage = 0;

            string line;

            // Read the downloaded file
            using (StreamReader reader = File.OpenText(ModSettings.steamDownloadedPageFullPath))
            {
                // Read all the lines until the end of the file
                while ((line = reader.ReadLine()) != null)
                {
                    // Search for the identifying string for the next mod; continue with next line if not found
                    if (!line.Contains(ModSettings.steamModListingModFind))
                    {
                        continue;
                    }

                    // Found the identifying string; get the Steam ID
                    ulong steamID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.steamModListingModIDLeft, ModSettings.steamModListingModIDRight));

                    if (steamID == 0) 
                    {
                        // Steam ID was not recognized, continue with the next line
                        Logger.UpdaterLog("Steam ID not recognized on HTML line: " + line, Logger.warning);

                        continue;
                    }

                    // Get the mod name
                    string name = Toolkit.CleanString(Toolkit.MidString(line, ModSettings.steamModListingModNameLeft, ModSettings.steamModListingModNameRight));

                    // Skip one line
                    line = reader.ReadLine();

                    // Get the author ID and custom URL; only one will exist, the other will be zero / empty
                    ulong authorID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.steamModListingAuthorIDLeft, ModSettings.steamModListingAuthorRight));

                    string authorURL = Toolkit.MidString(line, ModSettings.steamModListingAuthorURLLeft, ModSettings.steamModListingAuthorRight);
                    
                    // Get the author name
                    string authorName = Toolkit.CleanString(Toolkit.MidString(line, ModSettings.steamModListingAuthorNameLeft, ModSettings.steamModListingAuthorNameRight));

                    // Steam sometimes incorrectly puts the profile ID in place of the name; log if it happens. However, there are authors that actually have the ID as name
                    if (authorName == authorID.ToString())
                    {
                        if (ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(authorID))
                        {
                            if (ActiveCatalog.Instance.AuthorIDDictionary[authorID].Name != authorID.ToString())
                            {
                                Logger.UpdaterLog($"Author found in HTML with profile ID as name ({ authorID }). This could be a Steam error.", Logger.warning);
                            }
                        }
                        else
                        {
                            Logger.UpdaterLog($"Author found in HTML with profile ID as name ({ authorID }). This could be a Steam error.", Logger.warning);
                        }
                    }

                    // Add the mod to the dictionary; avoid duplicates (could happen if a new mod is published in the time of downloading all pages)
                    if (!CatalogUpdater.CollectedModInfo.ContainsKey(steamID))
                    {
                        Mod mod = new Mod(steamID, name, authorID, authorURL);

                        if (incompatible)
                        {
                            // Assign the incompatible status if we got the mod from an 'incompatible' mod listing page
                            mod.Update(statuses: new List<Enums.ModStatus> { Enums.ModStatus.IncompatibleAccordingToWorkshop });
                        }

                        CatalogUpdater.CollectedModInfo.Add(steamID, mod);

                        modsFoundThisPage++;

                        // Debug messages [Todo 0.5] Remove?
                        if (string.IsNullOrEmpty(name))
                        {
                            Logger.Log($"Mod name not found: { steamID }.", Logger.debug);
                        }

                        if (authorID == 0 && string.IsNullOrEmpty(authorURL))
                        {
                            Logger.Log($"Author ID and URL not found: { steamID }.", Logger.debug);
                        }
                        else if (authorID != 0 && !string.IsNullOrEmpty(authorURL))
                        {
                            Logger.Log($"Author ID and URL both found at the same time: { steamID }.", Logger.debug);
                        }
                    }

                    // Add the author to one of the dictionaries; avoid duplicates
                    if (authorID != 0)
                    {
                        if (!CatalogUpdater.CollectedAuthorIDs.ContainsKey(authorID))
                        {
                            CatalogUpdater.CollectedAuthorIDs.Add(authorID, new Author(authorID, authorURL, authorName));
                        }
                    }
                    else if (!string.IsNullOrEmpty(authorURL))
                    {
                        if (!CatalogUpdater.CollectedAuthorURLs.ContainsKey(authorURL))
                        {
                            CatalogUpdater.CollectedAuthorURLs.Add(authorURL, new Author(authorID, authorURL, authorName));
                        }
                    }
                }
            }

            return modsFoundThisPage;
        }


        // Add unfound catalog mods to the collected mod dictionary for a detail check
        private static void AddUnfoundMods()
        {
            foreach (Mod catalogMod in ActiveCatalog.Instance.Mods)
            {
                // Skip any mods we found already and any non-Steam mods
                if (CatalogUpdater.CollectedModInfo.ContainsKey(catalogMod.SteamID) || catalogMod.SteamID <= ModSettings.highestFakeID)
                {
                    continue;
                }

                // Create a copy of the catalog mod
                Mod unfoundMod = Mod.Copy(catalogMod);

                // Add the unknown status if it doesn't have the unlisted or removed status (will be changed to removed or unlisted later)
                if (!unfoundMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop) && !unfoundMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                {
                    unfoundMod.Statuses.Add(Enums.ModStatus.Unknown);

                    Logger.UpdaterLog($"Mod from catalog without 'removed' or 'unlisted' status not found: { unfoundMod.ToString(cutOff: false) }", Logger.debug);
                }

                // Add the mod to the collected mods dictionary
                CatalogUpdater.CollectedModInfo.Add(unfoundMod.SteamID, unfoundMod);
            }
        }


        // Get mod information from the individual mod pages on the Steam Workshop; we get this info for all new mods and for a maximum number of known mods
        private static bool GetDetails()
        {
            // If the current active catalog is version 1, we're (re)building the catalog from scratch; version 2 should not include any details, so exit here
            if (ActiveCatalog.Instance.Version == 1)
            {
                Logger.UpdaterLog($"Auto Updater skipped checking individual Steam Workshop mod pages for the second catalog.");

                CatalogUpdater.SetNote(ModSettings.secondCatalogNote);

                return true;
            }

            // If the current active catalog is version 2, we're still (re)building the catalog from scratch; version 3 is the first 'real' catalog
            if (ActiveCatalog.Instance.Version == 2)
            {
                CatalogUpdater.SetNote(ModSettings.thirdCatalogNote);
            }

            // Reset the catalog note if it is still the default note
            else if (ActiveCatalog.Instance.Note == ModSettings.thirdCatalogNote)
            {
                CatalogUpdater.SetNote("");
            }

            // Time the download and processing
            Stopwatch timer = Stopwatch.StartNew();

            // Estimated time in milliseconds is about half a second per download
            long estimated = 550 * CatalogUpdater.CollectedModInfo.Count;

            Logger.UpdaterLog($"Auto Updater started checking individual Steam Workshop mod pages. Estimated time: { Toolkit.ElapsedTime(estimated) }.", 
                duplicateToRegularLog: true);

            // Initialize counters
            uint knownModsDownloaded = 0;
            uint newModsDownloaded = 0;
            uint failedDownloads = 0;

            // Check all mods we gathered, one by one
            foreach (ulong steamID in CatalogUpdater.CollectedModInfo.Keys)
            {
                bool knownMod = ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID);

                // Download the Steam Workshop mod page
                if (Toolkit.Download(Toolkit.GetWorkshopURL(steamID), ModSettings.steamDownloadedPageFullPath) != null)
                {
                    // Download error
                    failedDownloads++;

                    if (failedDownloads <= ModSettings.SteamMaxFailedPages)
                    {
                        // Download error might be mod specific. Go to the next mod.
                        Logger.UpdaterLog("Permanent error while downloading Steam Workshop page for " + 
                            $"{ CatalogUpdater.CollectedModInfo[steamID].ToString(cutOff: false) }. Will continue with next mod.", Logger.warning);

                        continue;
                    }
                    else
                    {
                        // Too many failed downloads. Stop downloading
                        Logger.UpdaterLog("Permanent error while downloading Steam Workshop page for " + 
                            $"{ CatalogUpdater.CollectedModInfo[steamID].ToString(cutOff: false) }. Download process stopped.", Logger.error);

                        break;
                    }
                }

                // Page downloaded, increase the counter
                if (knownMod)
                {
                    knownModsDownloaded++;
                }
                else
                {
                    newModsDownloaded++;
                }

                // Log a sign of life every 100 mods
                if ((knownModsDownloaded + newModsDownloaded) % 100 == 0)
                {
                    Logger.UpdaterLog($"{ knownModsDownloaded + newModsDownloaded } mods checked.");
                }

                // Extract detailed info from the downloaded page
                ReadModPage(steamID);
            }

            // Delete the temporary file
            Toolkit.DeleteFile(ModSettings.steamDownloadedPageFullPath);

            // Log the elapsed time
            timer.Stop();

            Logger.UpdaterLog($"Auto Updater finished downloading { knownModsDownloaded + newModsDownloaded } individual Steam Workshop mod pages in " + 
                $"{ Toolkit.ElapsedTime(timer.ElapsedMilliseconds, alwaysShowSeconds: true) }.", duplicateToRegularLog: true);

            // return true if we downloaded at least one mod
            return (knownModsDownloaded + newModsDownloaded) > 0;
        }


        // Extract detailed mod information from the downloaded mod page; return false if the Steam ID can't be found on this page
        private static void ReadModPage(ulong steamID)
        {
            // Get the mod
            Mod mod = CatalogUpdater.CollectedModInfo[steamID];

            // Keep track if we find the correct Steam ID on this page, to avoid messing up one mod with another mods info
            bool steamIDmatched = false;

            // Read the page back from file
            string line;

            using (StreamReader reader = File.OpenText(ModSettings.steamDownloadedPageFullPath))
            {
                // Read all the lines until the end of the file
                while ((line = reader.ReadLine()) != null)
                {
                    // First find the correct Steam ID on this page; it appears before all other info
                    if (!steamIDmatched)
                    {
                        steamIDmatched = line.Contains(ModSettings.steamModPageSteamID + steamID.ToString());

                        // Don't update anything if we don't find the Steam ID first
                        continue;
                    }

                    // We found the steam ID, so this mod is definitely not / no longer removed from the Steam Workshop (can still be unlisted)
                    mod.Statuses.Remove(Enums.ModStatus.RemovedFromWorkshop);
                    
                    // If we gave the mod the unknown status before (at AddUnfoundMods), change it to unlisted
                    if (mod.Statuses.Contains(Enums.ModStatus.Unknown))
                    {
                        mod.Statuses.Remove(Enums.ModStatus.Unknown);

                        mod.Statuses.Add(Enums.ModStatus.UnlistedInWorkshop);
                    }

                    // Author profile ID or custom URL, and author name; only for unlisted mods (we have this info for other mods already)
                    if (line.Contains(ModSettings.steamModPageAuthorFind) && mod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                    {
                        ulong authorID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.steamModPageAuthorFind + "profiles/", 
                            ModSettings.steamModPageAuthorMid));

                        string authorURL = Toolkit.MidString(line, ModSettings.steamModPageAuthorFind + "id/", ModSettings.steamModPageAuthorMid);
                        
                        // Empty the author custom URL if author ID was found or if custom URL was not found, preventing updating the custom URL to an empty string
                        if (authorID != 0 || string.IsNullOrEmpty(authorURL))
                        {
                            authorURL = null;
                        }

                        string authorName = Toolkit.CleanString(Toolkit.MidString(line, ModSettings.steamModPageAuthorMid, ModSettings.steamModPageAuthorRight));
                        
                        // Update the mod
                        mod.Update(authorID: authorID, authorURL: authorURL);

                        // Add the author to one of the dictionaries, if we don't have this author yet; if somehow we didn't get an authorName, don't empty it
                        if ((authorID != 0) && !CatalogUpdater.CollectedAuthorIDs.ContainsKey(authorID))
                        {
                            CatalogUpdater.CollectedAuthorIDs.Add(authorID, new Author(authorID, authorURL, string.IsNullOrEmpty(authorName) ? null : authorName));

                            // Debug messages [Todo 0.5] Remove?
                            if (string.IsNullOrEmpty(authorName))
                            {
                                Logger.Log($"Author name not found for unlisted mod { steamID }.", Logger.debug);
                            }
                        }

                        if (!string.IsNullOrEmpty(authorURL))
                        {
                            if (!CatalogUpdater.CollectedAuthorURLs.ContainsKey(authorURL))
                            {
                                CatalogUpdater.CollectedAuthorURLs.Add(authorURL, new Author(authorID, authorURL, string.IsNullOrEmpty(authorName) ? null : authorName));

                                // Debug messages [Todo 0.5] Remove?
                                if (string.IsNullOrEmpty(authorName))
                                {
                                    Logger.Log($"Author name not found for unlisted mod { steamID }.", Logger.debug);
                                }
                            }
                        }
                    }

                    // Mod name; only for unlisted mods (we have this info for other mods already)
                    else if (line.Contains(ModSettings.steamModPageNameLeft) && mod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                    {
                        // Update the mod
                        mod.Update(name: Toolkit.CleanString(Toolkit.MidString(line, ModSettings.steamModPageNameLeft, ModSettings.steamModPageNameRight)));
                    }

                    // Compatible game version tag
                    else if (line.Contains(ModSettings.steamModPageVersionTagFind))
                    {
                        string gameVersion = Toolkit.MidString(line, ModSettings.steamModPageVersionTagLeft, ModSettings.steamModPageVersionTagRight);

                        // Update the mod, but first convert the gameversion string back and forth to ensure a correctly formatted string
                        mod.Update(compatibleGameVersionString: GameVersion.Formatted(Toolkit.ConvertToGameVersion(gameVersion)));
                    }

                    // Publish and update dates; also update author last seen date and retired state
                    else if (line.Contains(ModSettings.steamModPageDatesFind))
                    {
                        // Skip two lines
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        DateTime published = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.steamModPageDatesLeft, ModSettings.steamModPageDatesRight));

                        // Skip another line
                        line = reader.ReadLine();

                        // Get the update date, if available; this will return DateTime.MinValue if no updated date was found on the page
                        DateTime updated = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.steamModPageDatesLeft, ModSettings.steamModPageDatesRight));

                        // Update the mod with both dates
                        mod.Update(published: published, updated: updated);

                        // If the updated date is more recent than the author's last seen date, set the latter to the former and reset the authors retired state
                        Author author = mod.AuthorID != 0 ? CatalogUpdater.CollectedAuthorIDs[mod.AuthorID] : 
                            !string.IsNullOrEmpty(mod.AuthorURL) ? CatalogUpdater.CollectedAuthorURLs[mod.AuthorURL] : null;

                        if (author != null)
                        {
                            if (mod.Updated > author.LastSeen)
                            {
                                author.Update(lastSeen: mod.Updated, retired: false);
                            }
                        }
                    }

                    // Required DLC
                    else if (line.Contains(ModSettings.steamModPageRequiredDLCFind))
                    {
                        // Skip one line
                        line = reader.ReadLine();

                        string dlcString = Toolkit.MidString(line, ModSettings.steamModPageRequiredDLCLeft, ModSettings.steamModPageRequiredDLCRight);
                        
                        if (!string.IsNullOrEmpty(dlcString))
                        {
                            try
                            {
                                // Convert the dlc string to number to enum and add it
                                mod.RequiredDLC.Add((Enums.DLC)Convert.ToUInt32(dlcString));
                            }
                            catch
                            {
                                Logger.UpdaterLog($"Cannot convert \"{ dlcString }\" to DLC enum for { mod.ToString(cutOff: false) }.", Logger.warning);
                            }
                        }
                    }

                    // Required mods and assets
                    else if (line.Contains(ModSettings.steamModPageRequiredModFind))
                    {
                        // Try to find required mods until we find no more; max. 50 times to avoid infinite loops on code errors
                        for (uint tries = 1; tries <= 50; tries++)
                        {
                            // Read the next line
                            line = reader.ReadLine();

                            // Get the required Steam ID as string
                            string requiredString = Toolkit.MidString(line, ModSettings.steamModPageRequiredModLeft, ModSettings.steamModPageRequiredModRight);

                            // Convert the required Steam ID string to ulong
                            ulong requiredID = Toolkit.ConvertToUlong(requiredString);

                            if (requiredID == 0)
                            {
                                // No more steam IDs found for required mods
                                if (tries == 1)
                                {
                                    Logger.UpdaterLog($"Steam ID not recognized for required mod: { requiredString }.", Logger.warning);
                                }

                                // Exit the required mods loop
                                break;
                            }

                            // Add the required mod
                            mod.RequiredMods.Add(requiredID);

                            // Skip three lines
                            line = reader.ReadLine();
                            line = reader.ReadLine();
                            line = reader.ReadLine();
                        }
                    }

                    // Description for 'no description' status and for source url
                    else if (line.Contains(ModSettings.steamModPageDescriptionFind))
                    {
                        // Skip one line; the complete description is on the next line
                        line = reader.ReadLine();

                        // Get the description length: number of chars after the left boundary text, minus the length of the right one (which is at the end of the line)
                        int descriptionLength = line.Length - line.IndexOf(ModSettings.steamModPageDescriptionLeft) - 
                            ModSettings.steamModPageDescriptionLeft.Length - ModSettings.steamModPageDescriptionRight.Length;

                        // Tag as 'no description' if the description is not at least a few characters longer than the mod name
                        if (descriptionLength <= mod.Name.Length + 3)
                        {
                            mod.Statuses.Add(Enums.ModStatus.NoDescription);
                        }

                        // Get the source url, if any
                        else if (line.Contains(ModSettings.steamModPageSourceURLLeft))
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
                // Indicate we checked details for this mod; this will be used by the CatalogUpdater class
                mod.Update(changeNotes: "Details checked");
            }
            else
            {
                // We didn't find this mod; if we gave the mod the unknown status before (at AddUnfoundMods), change it to removed
                if (mod.Statuses.Contains(Enums.ModStatus.Unknown))
                {
                    mod.Statuses.Remove(Enums.ModStatus.Unknown);

                    mod.Statuses.Add(Enums.ModStatus.RemovedFromWorkshop);
                }
                else
                {
                    // The download page for this mod couldn't be read correctly
                    Logger.UpdaterLog($"Can't find the Steam ID on downloaded page for { CatalogUpdater.CollectedModInfo[steamID].ToString(cutOff: false) }. " +
                        "Mod info not updated.", Logger.error);
                }
            }
        }


        // Get the source URL; if more than one is found, pick the most likely; this is not foolproof and will need a manual update for some [Todo 0.3] Skip if exclusion
        private static string GetSourceURL(string line, ulong steamID)
        {
            string sourceURL = Toolkit.MidString(line, ModSettings.steamModPageSourceURLLeft, ModSettings.steamModPageSourceURLRight);

            // Exit if we find none
            if (string.IsNullOrEmpty(sourceURL))
            {
                return null;
            }

            // Complete the url and get ready to find another
            sourceURL = "https://github.com/" + sourceURL;

            string secondSourceURL;

            uint tries = 0;

            // Keep comparing source url's until we find no more; max. 50 times to avoid infinite loops on code errors
            while (line.IndexOf(ModSettings.steamModPageSourceURLLeft) != line.LastIndexOf(ModSettings.steamModPageSourceURLLeft) && tries < 50)
            {
                tries++;

                // Cut off the start of the string to just after the previous occurrence (more specific: to the second char of the previous source url)
                int index = line.IndexOf(ModSettings.steamModPageSourceURLLeft) + 1;
                line = line.Substring(index, line.Length - index);

                // Get the second source url
                secondSourceURL = Toolkit.MidString(line, ModSettings.steamModPageSourceURLLeft, ModSettings.steamModPageSourceURLRight);

                // Decide which source url to use if a second source url was found and both url's are not identical
                if (!string.IsNullOrEmpty(secondSourceURL) && sourceURL != "https://github.com/" + secondSourceURL)
                {
                    secondSourceURL = "https://github.com/" + secondSourceURL;

                    string lower = sourceURL.ToLower();

                    // We want an url without 'issue', 'wiki', 'documentation', 'readme', 'guide', 'translation', pardeike's Harmony or Sschoener's detour
                    if (lower.Contains("issue") || lower.Contains("wiki") || lower.Contains("documentation") || lower.Contains("readme") || lower.Contains("guide")
                        || lower.Contains("translation") 
                        || lower.Contains("https://github.com/pardeike") || lower.Contains("https://github.com/sschoener/cities-skylines-detour"))
                    {
                        // Keep the new
                        Logger.UpdaterLog($"Found multiple source url's for [Steam ID { steamID, 10 }]: \"{ secondSourceURL }\" (kept) and \"{ sourceURL }\" (discarded)");

                        sourceURL = secondSourceURL;
                    }
                    else
                    {
                        // Keep the previous; keep finding the rest for complete logging of all source url's found
                        Logger.UpdaterLog($"Found multiple source url's for [Steam ID { steamID, 10 }]: \"{ sourceURL }\" (kept) and \"{ secondSourceURL }\" (discarded)");
                    }
                }
            }

            return sourceURL;
        }
    }
}
