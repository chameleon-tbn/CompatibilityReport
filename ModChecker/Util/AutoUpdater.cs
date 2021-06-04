using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ModChecker.DataTypes;


namespace ModChecker.Util
{
    // This class has limited error handling because the auto updater is not for regular users
    internal static class AutoUpdater
    {
        // Dictionaries to collect info from the Steam Workshop
        private static readonly Dictionary<ulong, Mod> collectedModInfo = new Dictionary<ulong, Mod>();
        private static readonly Dictionary<string, Author> collectedAuthorInfo = new Dictionary<string, Author>();

        // Change notes
        private static StringBuilder ChangeNotes = new StringBuilder();


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
                    UpdateCatalog();

                    // The filename for the new catalog and related files ('ModCheckerCatalog_v1.0001')
                    string partialPath = Path.Combine(ModSettings.UpdatedCatalogPath, $"{ ModSettings.internalName }Catalog_v{ ActiveCatalog.Instance.VersionString() }");

                    // Save the new catalog
                    if (ActiveCatalog.Instance.Save(partialPath + ".xml"))
                    {
                        // Save changes notes, in the same folder as the new catalog
                        Tools.SaveToFile(ChangeNotes.ToString(), partialPath + "_ChangesNotes.txt");

                        // Copy the updater logfile to the same folder as the new catalog
                        Tools.CopyFile(ModSettings.updaterLogfileFullPath, partialPath + ".log");

                        success = true;
                    }
                }                    
            }

            Logger.UpdaterLog("Auto updater has shutdown.", extraLine: true, duplicateToRegularLog: true);

            // Empty the dictionaries and changes notes to free memory
            collectedModInfo.Clear();
            collectedAuthorInfo.Clear();
            ChangeNotes = new StringBuilder();

            return success;
        }
        

        // Get mod and author names and IDs from the Steam Workshop mod listing pages
        private static bool GetBasicInfo()
        {
            // Time the download and processing
            Stopwatch timer = Stopwatch.StartNew();

            Logger.Log("Auto updater started downloading Steam Workshop mod listing pages. This should take about 30 to 40 seconds. See separate logfile for details.");
            Logger.UpdaterLog("Auto updater started downloading Steam Workshop mod listing pages. This should take about 30 to 40 seconds.");

            uint totalPages = 0;

            // Go through the different mod listings: mods and camera scripts, both regular and incompatible
            foreach (string steamURL in ModSettings.SteamModListingURLs)
            {
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

            Logger.UpdaterLog($"Auto updater finished checking { totalPages } Steam Workshop mod list pages in { (float)timer.ElapsedMilliseconds / 1000:F1} seconds. " + 
                $"{ collectedModInfo.Count } mods and { collectedAuthorInfo.Count } authors found.", duplicateToRegularLog: true);

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
                    ulong steamID = Convert.ToUInt64(Tools.MidString(line, ModSettings.SteamModListingModIDLeft, ModSettings.SteamModListingModIDRight));

                    // If the Steam ID was not recognized, continue with the next lines
                    if (steamID == 0)
                    {
                        Logger.UpdaterLog("Steam ID not recognized on HTML line: " + line, Logger.warning);

                        continue;
                    }

                    // Get the mod name
                    string name = Tools.MidString(line, ModSettings.SteamModListingModNameLeft, ModSettings.SteamModListingModNameRight);

                    // Try to get the author ID from the next line
                    line = reader.ReadLine();

                    string authorID = Tools.MidString(line, ModSettings.SteamModListingAuthorIDLeft, ModSettings.SteamModListingAuthorIDRight);

                    bool authorIDIsProfile = authorID == "";

                    if (authorIDIsProfile)
                    {
                        // Author ID not found, get the profile number instead (as a string)
                        authorID = Tools.MidString(line, ModSettings.SteamModListingAuthorProfileLeft, ModSettings.SteamModListingAuthorIDRight);
                    }

                    // Get the author name
                    string authorName = Tools.MidString(line, ModSettings.SteamModListingAuthorNameLeft, ModSettings.SteamModListingAuthorNameRight);

                    // Add the mod to the dictionary; avoid duplicates (could happen if a new mod is published in the time of downloading all pages)
                    if (!collectedModInfo.ContainsKey(steamID))
                    {
                        Mod mod = new Mod(steamID, name, authorID);

                        if (incompatible)
                        {
                            // Assign the incompatible status if we got the mod from an 'incompatible' mod listing page
                            mod.Update(statuses: new List<Enums.ModStatus> { Enums.ModStatus.IncompatibleAccordingToWorkshop });
                        }

                        collectedModInfo.Add(steamID, mod);

                        modsFoundThisPage++;

                        Logger.UpdaterLog($"Mod found: { mod.ToString() }", Logger.debug);
                    }

                    // Add the author to the dictionary; avoid duplicates
                    if (!collectedAuthorInfo.ContainsKey(authorID))
                    {
                        // Last seen will be updated with the detail information later
                        collectedAuthorInfo.Add(authorID, new Author(authorID, authorIDIsProfile, authorName, lastSeen: DateTime.MinValue, retired: false));

                        Logger.UpdaterLog($"Author found: [{ authorID }] { authorName }", Logger.debug);
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

            Logger.UpdaterLog("Auto updater started checking individual Steam Workshop mod pages. This could take up to 20 minutes, depending on settings.", 
                duplicateToRegularLog: true);

            // [Todo 0.2] temporary, should be removed after first catalog is created
            uint maxNewModDownloads = 10;

            // Initialize counters
            uint newModsDownloaded = 0;
            uint knownModsDownloaded = 0;
            uint modsFound = 0;
            uint failedDownloads = 0;

            // [Todo 0.2] at a starting point, to randomize which known mods to download
            // Check all mods we gathered, one by one
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

            return (knownModsDownloaded + newModsDownloaded) > 0;
        }


        // Extract detailed mod information from the downloaded mod page; return false if the Steam ID can't be found on this page
        private static bool ReadModPage(ulong steamID)
        {
            // Keep track if we find the correct Steam ID on this page, to avoid messing up one mod with another mods info
            bool steamIDmatched = false;
            
            string line;
            
            // Read the page back from file
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
                        collectedModInfo[steamID].Update(
                            compatibleGameVersionString: GameVersion.Formatted(Tools.ConvertToGameVersion(gameVersion)));
                    }
                    // Publish and update dates
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
                        collectedModInfo[steamID].Update(published: published, updated: updated);
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
                                collectedModInfo[steamID].RequiredDLC.Add((Enums.DLC)Convert.ToUInt32(dlcString));
                            }
                            catch
                            {
                                Logger.UpdaterLog($"Cannot convert \"{ dlcString }\" to DLC enum for { collectedModInfo[steamID].ToString() }.", Logger.warning);
                            }
                        }
                    }
                    // Required mods
                    else if (line.Contains(ModSettings.SteamModPageRequiredModFind))
                    {
                        // Skip one line
                        line = reader.ReadLine();

                        // Get the required mods Steam ID
                        string modString = Tools.MidString(line, ModSettings.SteamModPageRequiredModLeft, ModSettings.SteamModPageRequiredModRight);

                        ulong modID = Convert.ToUInt64(modString);

                        // Update the mod
                        if (modID != 0)
                        {
                            collectedModInfo[steamID].RequiredMods.Add(modID);
                        }
                        else
                        {
                            Logger.UpdaterLog($"Steam ID not recognized for required mod: { modString }.", Logger.warning);
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
                        if (descriptionLength <= collectedModInfo[steamID].Name.Length + 3)
                        {
                            collectedModInfo[steamID].Statuses.Add(Enums.ModStatus.NoDescription);
                        }

                        // Get the source url, if any
                        if (line.Contains(ModSettings.SteamModPageSourceURLLeft))
                        {
                            collectedModInfo[steamID].Update(sourceURL: GetSourceURL(line, steamID));
                        }

                        // Description is the last info we need from the page, so exit the while loop
                        break;
                    }
                }
            }

            return steamIDmatched;
        }


        // Get the source URL; if more than one is found, pick the most likely
        private static string GetSourceURL(string line, ulong steamID)
        {
            string sourceURL = Tools.MidString(line, ModSettings.SteamModPageSourceURLLeft, ModSettings.SteamModPageSourceURLRight);

            if (string.IsNullOrEmpty(sourceURL))
            {
                return null;
            }

            // Complete the url again
            sourceURL = "https://github.com/" + sourceURL;

            // Try to find a second source url
            string searchString = line;
            string secondSourceURL;

            // Keep comparing two source url's until we cannot find any more
            while (searchString.IndexOf(ModSettings.SteamModPageSourceURLLeft) != searchString.LastIndexOf(ModSettings.SteamModPageSourceURLLeft))
            {
                // Set the start the search string to just after the first occurrence
                int index = searchString.IndexOf(ModSettings.SteamModPageSourceURLLeft);
                searchString = line.Substring(index + 1, line.Length - index - 1);

                // Get the second source url
                secondSourceURL = Tools.MidString(searchString, ModSettings.SteamModPageSourceURLLeft, ModSettings.SteamModPageSourceURLRight);

                if (!string.IsNullOrEmpty(secondSourceURL))
                {
                    secondSourceURL = "https://github.com/" + secondSourceURL;

                    // Compare the previous and new found url's and choose which one to keep
                    string first = sourceURL.ToLower();
                    string second = secondSourceURL.ToLower();

                    // Keep the new one if it doesn't contain wiki or issue, and the previous one does; or if the previous one refers to Pardeike Harmony
                    if (((first.Contains("wiki") || first.Contains("issue")) && !second.Contains("wiki") && !second.Contains("issue")) || 
                        first.Contains("https://github.com/pardeike/harmony"))
                    {
                        // Keep the newly found
                        Logger.UpdaterLog($"Found more than one source url for [{ steamID }]: " +
                            $"\"{ secondSourceURL }\" (kept) and \"{ sourceURL }\" (discarded)");

                        sourceURL = secondSourceURL;
                    }
                    // Otherwise keep the previous one
                    else
                    {
                        Logger.UpdaterLog($"Found more than one source url for [{ steamID }]: " +
                            $"\"{ sourceURL }\" (kept) and \"{ secondSourceURL }\" (discarded)");
                    }
                }
            }

            return sourceURL;
        }


        // Update the active catalog with the found information
        private static void UpdateCatalog()
        {
            // Check the found mods one by one
            foreach (ulong steamID in collectedModInfo.Keys)
            {


                // [Todo 0.2] Fix and finish this
                // Remove incorrect steam IDs from the required mods list
                foreach (ulong modID in collectedModInfo[steamID].RequiredMods)
                {
                    // Remove the mod ID if we didn't find it on the Workshop, or if it accidentally points to itself
                    if (!collectedModInfo.ContainsKey(modID) || (modID == steamID))
                    {
                        collectedModInfo[steamID].RequiredMods.Remove(modID);

                        Logger.UpdaterLog($"Required mod [Steam ID { modID }] can't be found for { collectedModInfo[steamID].ToString() }].", Logger.warning);
                    }
                }

                // vars to be used: (Steam ID), basicinfomod.Name, basicinfomod.AuthorID, published, updated, requiredDLC, requiredMods (check groups), sourceURL,
                //                              compatibleVersion, statuses - IncompatibleAccordingToWorkshop & noDescription
                //                  AuthorID, IDisProfile, Authorname
                //                  indirect:   status RemovedFromWorkshop (false for everything we find, true for unfound but in catalog)
                //                              AutoReviewUpdated = now,
                //                              SourceUnavailable (if sourceURL)
                //                              NeededFor (only update if it is already used for a mod)
                //                              Author isRetired (false for new and updated mods)
                //                              Author lastseen (updatedate if > current lastseen)

                // Get the mod from the active catalog or create a new object
                Mod catalogMod;

                if (!ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID))
                {
                    // Add a new mod to the catalog
                    catalogMod = ActiveCatalog.Instance.AddMod(steamID, collectedModInfo[steamID].Name, collectedModInfo[steamID].AuthorID);
                }
                else
                {
                    // Get a reference to the mod in the catalog
                    catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];
                }
            }

            // [Todo 0.2] Fix and finish this
            // Not updated:         ArchiveURL, SucceededBy, Alternatives, Note, most Statuses, Compatibilities, Mod Groups
            // Only half updated:   Author last seen, Author retired, NeededFor

            // Increase the catalog version
            ActiveCatalog.Instance.NewVersion();
        }
    }
}
