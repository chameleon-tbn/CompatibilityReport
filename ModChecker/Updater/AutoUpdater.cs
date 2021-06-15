using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ModChecker.DataTypes;
using ModChecker.Util;


// Auto updater updates the catalog with information from the Steam Workshop pages. Only the following is updated/added:
// * Mod: name, author, publish/update dates, source url, compatible game version, required DLC, required mods, only-needed-for mods (update only, not for new mods),
//        statuses: incompatible according to the workshop, removed from workshop, no description, no source available (only when a source url is found)
// * Author: name, last seen (only based on mod updates), retired (only based on mod updates)
// Note: a change in author URL might be seen as a new author


namespace ModChecker.Updater
{
    internal static class AutoUpdater
    {
        // Did we run already this session (successful or not)
        private static bool hasRun;


        // Start the auto updater; will download Steam webpages, extract info, update the active catalog and save it with a new version; includes change notes
        internal static bool Start(uint maxKnownModDownloads = ModSettings.SteamMaxKnownModDownloads)
        {
            // Exit if we ran already, the updater is not enabled in settings, or if we can't get an active catalog
            if (hasRun || !ModSettings.UpdaterEnabled || !ActiveCatalog.Init())
            {
                return false;
            }

            hasRun = true;

            bool success = false;

            // Initialize the dictionaries we need
            CatalogUpdater.Init();

            // Get basic mod and author information from the Steam Workshop mod listing pages
            if (GetBasicInfo())
            {
                // Get detailed information from the individual mod pages
                if (GetDetails(maxKnownModDownloads))
                {
                    // Update the catalog with the new info and save it to a new version
                    success = CatalogUpdater.Start();
                }                    
            }

            Logger.UpdaterLog("Auto updater has shutdown.", extraLine: true, duplicateToRegularLog: true);

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
            foreach (string steamURL in ModSettings.steamModListingURLs)
            {
                Logger.UpdaterLog($"Starting downloading from { steamURL }");
                
                uint pageNumber = 0;

                // Download and read pages until we find no more pages, or we reach the set maximum number of pages (to avoid missing the mark and continuing for eternity)
                while (pageNumber < ModSettings.steamMaxModListingPages)
                {
                    // Increase the pagenumber and download a page
                    pageNumber++;

                    Exception ex = Tools.Download($"{ steamURL }&p={ pageNumber }", ModSettings.steamWebpageFullPath);

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
            Tools.DeleteFile(ModSettings.steamWebpageFullPath);

            // Log the elapsed time; note: >95% of process time is download; skipping the first 65KB (900+ lines) with 'reader.Basestream.Seek' does nothing for speed
            timer.Stop();

            Logger.UpdaterLog($"Auto updater finished checking { totalPages } Steam Workshop mod list pages in " + 
                $"{ Tools.FormattedTime(timer.ElapsedMilliseconds, showDecimal: true) }. { CatalogUpdater.CollectedModInfo.Count } mods and " + 
                $"{ CatalogUpdater.CollectedAuthorIDs.Count + CatalogUpdater.CollectedAuthorURLs.Count } authors found.", duplicateToRegularLog: true);

            return (totalPages > 0) && (CatalogUpdater.CollectedModInfo.Count > 0);
        }


        // Extract mod and author info from the downloaded mod listing page; return false if no mods were found on this page
        private static uint ReadModListingPage(bool incompatible)
        {
            uint modsFoundThisPage = 0;

            string line;

            // Read the downloaded file
            using (StreamReader reader = File.OpenText(ModSettings.steamWebpageFullPath))
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
                    ulong steamID;

                    try
                    {
                        steamID = Convert.ToUInt64(Tools.MidString(line, ModSettings.steamModListingModIDLeft, ModSettings.steamModListingModIDRight));
                    }
                    catch
                    {
                        // If the Steam ID was not recognized, continue with the next lines
                        Logger.UpdaterLog("Steam ID not recognized on HTML line: " + line, Logger.warning);

                        continue;
                    }

                    // Get the mod name, and clean it a bit
                    string name = Tools.MidString(line, ModSettings.steamModListingModNameLeft, ModSettings.steamModListingModNameRight).Replace("&amp;", "&");

                    // Try to get the author ID and custom URL from the next line; only one will exist
                    line = reader.ReadLine();

                    ulong authorID;

                    try
                    {
                        authorID = Convert.ToUInt64(Tools.MidString(line, ModSettings.steamModListingAuthorIDLeft, ModSettings.steamModListingAuthorRight));
                    }
                    catch
                    {
                        // Author ID not found
                        authorID = 0;
                    }                    

                    // Author URL will be empty if not found
                    string authorURL = Tools.MidString(line, ModSettings.steamModListingAuthorURLLeft, ModSettings.steamModListingAuthorRight);
                    
                    // Get the author name
                    string authorName = Tools.MidString(line, ModSettings.steamModListingAuthorNameLeft, ModSettings.steamModListingAuthorNameRight);

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
                    }

                    // Add the author to one of the dictionaries; avoid duplicates
                    if ((authorID != 0) && !CatalogUpdater.CollectedAuthorIDs.ContainsKey(authorID))
                    {
                        CatalogUpdater.CollectedAuthorIDs.Add(authorID, new Author(authorID, "", authorName));
                    }

                    if (!string.IsNullOrEmpty(authorURL))
                    {
                        if (!CatalogUpdater.CollectedAuthorURLs.ContainsKey(authorURL))
                        {
                            CatalogUpdater.CollectedAuthorURLs.Add(authorURL, new Author(0, authorURL, authorName));
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

            // Estimate is 500 ms per download, and allowing for 50 new mods
            long estimated = 500 * Math.Min(maxKnownModDownloads + 50, CatalogUpdater.CollectedModInfo.Count);

            Logger.UpdaterLog($"Auto updater started checking individual Steam Workshop mod pages. This should take about { Tools.FormattedTime(estimated) }.",
                duplicateToRegularLog: true);

            // Initialize counters
            uint newModsDownloaded = 0;
            uint knownModsDownloaded = 0;
            uint modsFound = 0;
            uint failedDownloads = 0;

            // Get a random number between 0 and the number of mods we found minus the number we're allowed to download; will be 0 if we're allowed to download them all
            int skip = new Random().Next(Math.Max(CatalogUpdater.CollectedModInfo.Count - (int)maxKnownModDownloads + 1, 0));
            int skipped = 0;

            // Check all mods we gathered, one by one
            foreach (ulong steamID in CatalogUpdater.CollectedModInfo.Keys)
            {
                // New mod or a mod already in the catalog
                bool newMod = !ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID);

                // Skip a random number of mods before starting the downloads for known mods; this randomizes which mods to download for details
                if (skipped < skip)
                {
                    // Increase the counter
                    skipped++;

                    // Skip the mod if it's a known mod; new mods are still processed as usual
                    if (!newMod)
                    {
                        continue;
                    }
                }

                // Stop if we reached the maximum number of known mods
                if (!newMod && knownModsDownloaded >= maxKnownModDownloads)
                {
                    continue;
                }

                // Download the Steam Workshop mod page
                if (Tools.Download(Tools.GetWorkshopURL(steamID), ModSettings.steamWebpageFullPath) != null)
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

                    // Log every 100 as a sign of life
                    if (modsFound / 100 == Math.Ceiling((double)modsFound / 100))
                    {
                        Logger.UpdaterLog($"{ modsFound } mods checked.");
                    }
                }
                else
                {
                    Logger.UpdaterLog($"Can't find the Steam ID on downloaded page for { CatalogUpdater.CollectedModInfo[steamID].ToString(cutOff: false) }. " + 
                        "Mod info not updated.", Logger.error);
                }
            }

            // Delete the temporary file
            Tools.DeleteFile(ModSettings.steamWebpageFullPath);

            // Log the elapsed time
            timer.Stop();

            // [Todo 0.2] move to Tools
            /*string elapsedMinutes = timer.ElapsedMilliseconds < 120000 ? "" : 
                " (" +  ((int)timer.ElapsedMilliseconds / 60000).ToString() + ":" + ((timer.ElapsedMilliseconds /1000) % 60).ToString("00") + " minutes)";

            Logger.UpdaterLog($"Auto updater finished checking { modsFound } individual Steam Workshop mod pages in " + 
                $"{ (float)timer.ElapsedMilliseconds / 1000:F1} seconds{ elapsedMinutes }.", duplicateToRegularLog: true);*/

            Logger.UpdaterLog($"Auto updater finished checking { modsFound } individual Steam Workshop mod pages in " + 
                $"{ Tools.FormattedTime(timer.ElapsedMilliseconds, showBoth: true) }.", duplicateToRegularLog: true);

            // return true if we downloaded at least one mod, or we were not allowed to download any
            return (knownModsDownloaded + newModsDownloaded) > 0 || maxKnownModDownloads == 0;
        }


        // Extract detailed mod information from the downloaded mod page; return false if the Steam ID can't be found on this page
        private static bool ReadModPage(ulong steamID)
        {
            // Get the mod
            Mod mod = CatalogUpdater.CollectedModInfo[steamID];

            // Keep track if we find the correct Steam ID on this page, to avoid messing up one mod with another mods info
            bool steamIDmatched = false;

            // Read the page back from file
            string line;

            using (StreamReader reader = File.OpenText(ModSettings.steamWebpageFullPath))
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
                    
                    // Compatible game version tag
                    if (line.Contains(ModSettings.steamModPageVersionTagFind))
                    {
                        // Get the version as string
                        string gameVersion = Tools.MidString(line, ModSettings.steamModPageVersionTagLeft, ModSettings.steamModPageVersionTagRight);

                        // Update the mod, but first convert the gameversion string back and forth to ensure a correctly formatted string
                        mod.Update(compatibleGameVersionString: GameVersion.Formatted(Tools.ConvertToGameVersion(gameVersion)));
                    }
                    // Publish and update dates; also update author last seen date and retired state
                    else if (line.Contains(ModSettings.steamModPageDatesFind))
                    {
                        // Skip two lines
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        // Get the publish date
                        DateTime published = Tools.ConvertWorkshopDateTime(Tools.MidString(line, ModSettings.steamModPageDatesLeft, ModSettings.steamModPageDatesRight));

                        // Skip another line
                        line = reader.ReadLine();

                        // Get the update date, if available; 
                        DateTime updated = Tools.ConvertWorkshopDateTime(Tools.MidString(line, ModSettings.steamModPageDatesLeft, ModSettings.steamModPageDatesRight));

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

                        // Get the required DLC
                        string dlcString = Tools.MidString(line, ModSettings.steamModPageRequiredDLCLeft, ModSettings.steamModPageRequiredDLCRight);

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
                                Logger.UpdaterLog($"Cannot convert \"{ dlcString }\" to DLC enum for { mod.ToString(cutOff: false) }.", Logger.warning);
                            }
                        }
                    }
                    // Required mods
                    else if (line.Contains(ModSettings.steamModPageRequiredModFind))
                    {
                        // Skip one line
                        line = reader.ReadLine();

                        // Get the required mods Steam ID
                        string requiredString = Tools.MidString(line, ModSettings.steamModPageRequiredModLeft, ModSettings.steamModPageRequiredModRight);

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
                    else if (line.Contains(ModSettings.steamModPageDescriptionFind))
                    {
                        // Skip one line
                        line = reader.ReadLine();

                        // Get the description length: the number of characters between the left and right boundary texts
                        int descriptionLength = line.Length -
                            line.IndexOf(ModSettings.steamModPageDescriptionLeft) - ModSettings.steamModPageDescriptionLeft.Length -
                            ModSettings.steamModPageDescriptionRight.Length;

                        // Tag as 'no description' if the description is not at least a few characters longer than the mod name
                        if (descriptionLength <= mod.Name.Length + 3)
                        {
                            mod.Statuses.Add(Enums.ModStatus.NoDescription);
                        }

                        // Get the source url, if any
                        if (line.Contains(ModSettings.steamModPageSourceURLLeft))
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
            string sourceURL = Tools.MidString(line, ModSettings.steamModPageSourceURLLeft, ModSettings.steamModPageSourceURLRight);

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
            while (line.IndexOf(ModSettings.steamModPageSourceURLLeft) != line.LastIndexOf(ModSettings.steamModPageSourceURLLeft) && tries < 50)
            {
                tries++;

                // Set the start the string to just after the first occurrence
                int index = line.IndexOf(ModSettings.steamModPageSourceURLLeft);
                line = line.Substring(index + 1, line.Length - index - 1);

                // Get the second source url
                secondSourceURL = Tools.MidString(line, ModSettings.steamModPageSourceURLLeft, ModSettings.steamModPageSourceURLRight);

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
            }

            return sourceURL;
        }
    }
}
