using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ModChecker.DataTypes;

namespace ModChecker.Util
{
    internal static class Updater       // This class has limited error handling because the updater is not for regular users
    {
        // Lists to collect info from the Steam Workshop
        private static List<Mod> collectedModInfo;
        private static List<Author> collectedAuthorInfo;
        private static Dictionary<ulong, string> allSteamIDandNames;


        // Start the updater
        internal static bool Start()
        {
            bool success = false;

            // Only if the updater is enabled in settings and we have an active catalog
            if ((ModSettings.updaterEnabled) && (Catalog.Active != null))
            {
                // Get basic mod and author information from the Steam Workshop mod listing pages
                if (GetBasicModAndAuthorInfo())
                {
                    // Get detailed information from the individual mod pages and update the active catalog
                    if (GetDetailsAndUpdateCatalog())
                    {
                        // Increase the catalog version and save the new catalog
                        Catalog.Active.NewVersion();

                        string catalogFileName = $"{ ModSettings.internalName }Catalog_v{ Catalog.Active.VersionString() }.xml";

                        success = Catalog.Active.Save(Path.Combine(ModSettings.UpdatedCatalogPath, catalogFileName));

                        // [Todo 0.2] Save change notes (full and summary)
                    }                    
                }

                Logger.UpdaterLog("Updater has shutdown.", extraLine: true, duplicateToRegularLog: true);
            }

            // empty the lists to free memory
            collectedModInfo = null;
            collectedAuthorInfo = null;
            allSteamIDandNames = null;

            return success;
        }
        

        // Get mod and author names and IDs from the Steam Workshop mod listing pages
        private static bool GetBasicModAndAuthorInfo()
        {
            // Initialize the lists and dictionary
            collectedModInfo = new List<Mod>();
            collectedAuthorInfo = new List<Author>();
            allSteamIDandNames = new Dictionary<ulong, string>();
            
            // Initialize counters and triggers
            bool morePagesToDownload = true;
            uint pageNumber = 0;
            uint modsFound = 0;
            uint authorsFound = 0;

            Logger.Log("Updater started downloading Steam Workshop mod listing pages. This should take about 30 to 40 seconds. See separate logfile for details.");

            Logger.UpdaterLog("Updater started downloading Steam Workshop mod listing pages. This should take about 30 to 40 seconds.");

            // Time the total download and processing
            Stopwatch timer = Stopwatch.StartNew();

            // [Todo 0.2] Need to de-complicate the following, way too many nested while/foreach/if/try/using statements

            // Go through all the different mod listings, for regular mods and camera scripts, both normal and incompatible
            foreach (string steamURL in ModSettings.SteamModsListingURLs)
            {
                // Download and read pages until we find no more pages, or we reach the set maximum number of pages (to avoid missing the mark and continuing for eternity)
                while (morePagesToDownload && (pageNumber < ModSettings.SteamMaxModListingPages))
                {
                    // Increase the pagenumber
                    pageNumber++;

                    // Download a page
                    Exception ex = Tools.Download(steamURL + $"&p={ pageNumber }", ModSettings.SteamWebpageFullPath);

                    if (ex != null)
                    {
                        Logger.UpdaterLog($"Permanent error while downloading Steam Workshop mod listing page { pageNumber }. Download process stopped.", Logger.error);

                        Logger.Exception(ex, toUpdaterLog: true);

                        // Lower the pageNumber to the last succesful page
                        pageNumber--;
                        
                        // Stop downloading and continue with already downloaded pages (if any)
                        break;
                    }

                    // Keep track of mods on this page
                    uint modsFoundThisPage = 0;

                    // Read the downloaded file back
                    using (StreamReader reader = File.OpenText(ModSettings.SteamWebpageFullPath))
                    {
                        string line;
                    
                        try     // Limited error handling because the Updater is not for regular users
                        {
                            // Read all the lines until the end of the file or until we found all the mods we can find on one page
                            // Note: >95% of process time is download; skipping the first 65KB (900+ lines) with 'reader.Basestream.Seek' does nothing for speed
                            while ((line = reader.ReadLine()) != null)
                            {
                                // Search for the identifying string
                                if (line.Contains(ModSettings.SteamModListingModFind))
                                {
                                    // Get the Steam ID
                                    string SteamIDString = Tools.MidString(line, ModSettings.SteamModListingModIDLeft, ModSettings.SteamModListingModIDRight);

                                    if (string.IsNullOrEmpty(SteamIDString))
                                    {
                                        Logger.UpdaterLog("Steam ID not recognized on HTML line: " + line, Logger.warning);

                                        continue;   // To the next ReadLine
                                    }

                                    ulong steamID = Convert.ToUInt64(SteamIDString);

                                    // Get the mod name
                                    string name = Tools.MidString(line, ModSettings.SteamModListingModNameLeft, ModSettings.SteamModListingModNameRight);

                                    // The author is on the next line
                                    line = reader.ReadLine();

                                    // Try to get the author ID
                                    bool authorIDIsProfile = false;

                                    string authorID = Tools.MidString(line, ModSettings.SteamModListingAuthorIDLeft, ModSettings.SteamModListingAuthorIDRight);

                                    if (authorID == "")
                                    {
                                        // Author ID not found, get the profile number instead (as a string)
                                        authorID = Tools.MidString(line, ModSettings.SteamModListingAuthorProfileLeft, ModSettings.SteamModListingAuthorIDRight);

                                        authorIDIsProfile = true;
                                    }

                                    // Get the author name
                                    string authorName = Tools.MidString(line, ModSettings.SteamModListingAuthorNameLeft, ModSettings.SteamModListingAuthorNameRight);

                                    // Add the mod to the list and dictionary; avoid duplicates (could happen if a new mod is published in the 30 seconds of downloading all pages)
                                    if (!allSteamIDandNames.ContainsKey(steamID))
                                    {
                                        Mod mod = new Mod(steamID, name, authorID);

                                        if (steamURL.Contains("incompatible"))
                                        {
                                            // Assign the incompatible status if we got the mod from an 'incompatible' mod listing
                                            List<Enums.ModStatus> modStatus = new List<Enums.ModStatus> { Enums.ModStatus.IncompatibleAccordingToWorkshop };

                                            mod.Update(statuses: modStatus);
                                        }

                                        collectedModInfo.Add(mod);

                                        allSteamIDandNames.Add(steamID, name);

                                        modsFound++;
                                        modsFoundThisPage++;

                                        Logger.UpdaterLog($"Mod found: [{ steamID }] { name }", Logger.debug);
                                    }

                                    // Add the author to the list; avoid duplicates
                                    if (!collectedAuthorInfo.Exists(a => a.ID == authorID))
                                    {
                                        collectedAuthorInfo.Add(new Author(authorID, authorIDIsProfile, authorName, DateTime.MinValue, retired: false));

                                        authorsFound++;

                                        Logger.UpdaterLog($"Author found: [{ authorID }] { authorName }", Logger.debug);
                                    }
                                
                                }
                                else if (line.Contains(ModSettings.SteamModListingNoMoreFind))
                                {
                                    // We reach a page without mods
                                    morePagesToDownload = false;

                                    // Decrease pageNumber to indicate this page was no good
                                    pageNumber--;

                                    break;     // Stop reading lines from this file 
                                }
                            }

                            if (modsFoundThisPage == 0)
                            {
                                // We reach a page without mods
                                morePagesToDownload = false;
                            }

                            if (morePagesToDownload)
                            {
                                Logger.UpdaterLog($"Found { modsFoundThisPage } mods on page { pageNumber }.");
                            }                        
                        }
                        catch (Exception ex2)
                        {
                            Logger.UpdaterLog($"Can't (fully) read or understand downloaded page { pageNumber }. { modsFoundThisPage } mods found on this page.", 
                                Logger.warning);

                            Logger.Exception(ex2, toUpdaterLog: true, duplicateToGameLog: false);
                        }
                    }
                }   // End of while (morePagesToDownload) loop
            }

            // Delete the temporary file
            Tools.DeleteFile(ModSettings.SteamWebpageFullPath);

            // Log the elapsed time
            timer.Stop();

            Logger.UpdaterLog($"Updater finished checking { pageNumber } Steam Workshop mod list pages in { (float)timer.ElapsedMilliseconds / 1000:F1} seconds. " + 
                $"{ modsFound } mods and { authorsFound } authors found.", duplicateToRegularLog: true);

            return (pageNumber > 0) && (modsFound > 0);
        }


        // Get mod information from the individual mod pages on the Steam Workshop
        private static bool GetDetailsAndUpdateCatalog()
        {
            // Initialize counters
            uint knownModsDownloaded = 0;
            uint newModsDownloaded = 0;
            uint failedDownloads = 0;

            // Time the total download and processing
            Stopwatch timer = Stopwatch.StartNew();

            Logger.UpdaterLog("Updater started checking individual Steam Workshop mod pages. This should take less than two minutes.", duplicateToRegularLog: true);
            
            // Check all SteamIDs from the mods we gathered
            foreach (ulong steamID in allSteamIDandNames.Keys)
            {
                // [Todo 0.2] What to do with known mods? Downloading 1600 pages takes about 10 to 15 minutes
                //            Just do the first 100 new mods and 100 known mods from a random starting number?

                // New mod or a known mod?
                bool newMod = !Catalog.Active.ModDictionary.ContainsKey(steamID);
                
                // Stop if we reached the maximum number of both types of mods, continue with the next Steam ID if we only reached the maximum for this type of mod
                if ((newModsDownloaded >= ModSettings.SteamMaxNewModDownloads) && (knownModsDownloaded >= ModSettings.SteamMaxKnownModDownloads))
                {
                    break;
                }
                else if ((newMod && (newModsDownloaded >= ModSettings.SteamMaxNewModDownloads)) 
                    || (!newMod && (knownModsDownloaded >= ModSettings.SteamMaxKnownModDownloads)))
                {
                    continue;
                }

                // Temporary vars to hold the found information until we can update the catalog info
                bool steamIDmatched = false;
                string compatibleGameVersionString = "";
                string publishedString = "";
                string updatedString = "";
                List<string> requiredDLCStrings = new List<string>();
                List<ulong> requiredMods = new List<ulong>();
                string sourceURL = "";
                bool noDescription = false;

                // Mod name is needed later on
                string modName = allSteamIDandNames[steamID];

                // Download the Steam Workshop mod page
                if (Tools.Download(Tools.GetWorkshopURL(steamID), ModSettings.SteamWebpageFullPath) != null)
                {
                    // Download error
                    failedDownloads++;

                    if (failedDownloads <= ModSettings.SteamMaxFailedPages)
                    {
                        // Download error might be mod specific. Go to the next mod.
                        Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for [Steam ID { steamID }]. Will continue with next mod.",
                            Logger.warning);

                        continue;
                    }
                    else
                    {
                        // Too many failed downloads. Stop downloading
                        Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for [Steam ID { steamID }]. Download process stopped.",
                            Logger.error);

                        break;
                    }
                }
                else
                {
                    // Page downloaded, increase the counter
                    if (newMod)
                    {
                        newModsDownloaded++;
                    }
                    else
                    {
                        knownModsDownloaded++;
                    }

                    // Read the page back from file
                    using (StreamReader reader = File.OpenText(ModSettings.SteamWebpageFullPath))
                    {
                        string line;

                        bool moreLinesToRead = true;

                        try
                        {
                            // Read all the lines until the end of the file
                            while (((line = reader.ReadLine()) != null) && moreLinesToRead)
                            {
                                // Check the Steam ID to make sure we're reading the correct page
                                steamIDmatched = steamIDmatched || line.Contains(ModSettings.SteamModPageSteamID + steamID.ToString());

                                // Compatible game version tag, if we haven't found it yet
                                if (string.IsNullOrEmpty(compatibleGameVersionString) && line.Contains(ModSettings.SteamModPageVersionTagFind))
                                {
                                    // Get the compatible game version as a string
                                    compatibleGameVersionString = Tools.MidString(line, ModSettings.SteamModPageVersionTagLeft, ModSettings.SteamModPageVersionTagRight);
                                }
                                // Publish and update dates, if we haven't found them yet
                                else if (string.IsNullOrEmpty(publishedString) && line.Contains(ModSettings.SteamModPageDatesFind))
                                {
                                    // Skip two lines
                                    line = reader.ReadLine();
                                    line = reader.ReadLine();

                                    // Get the publish date as a string
                                    publishedString = Tools.MidString(line, ModSettings.SteamModPageDatesLeft, ModSettings.SteamModPageDatesRight);

                                    // Skip another line
                                    line = reader.ReadLine();

                                    // Try to get the update date as a string; will be an empty string if it isn't found
                                    updatedString = Tools.MidString(line, ModSettings.SteamModPageDatesLeft, ModSettings.SteamModPageDatesRight);
                                }
                                // Required DLC; can appear more than once
                                else if (line.Contains(ModSettings.SteamModPageRequiredDLCFind))
                                {
                                    // Skip one line
                                    line = reader.ReadLine();

                                    // Try to get the required DLC
                                    string dlcString = Tools.MidString(line, ModSettings.SteamModPageRequiredDLCLeft, ModSettings.SteamModPageRequiredDLCRight);

                                    if (!string.IsNullOrEmpty(dlcString))
                                    {
                                        requiredDLCStrings.Add(dlcString);
                                    }
                                }
                                // Required mods; can appear more than once
                                else if (line.Contains(ModSettings.SteamModPageRequiredModFind))
                                {
                                    // Skip one line
                                    line = reader.ReadLine();

                                    // Try to get the required DLC
                                    string modString = Tools.MidString(line, ModSettings.SteamModPageRequiredModLeft, ModSettings.SteamModPageRequiredModRight);
                                    
                                    if (!string.IsNullOrEmpty(modString))
                                    {
                                        ulong modID = Convert.ToUInt64(modString);
                                        
                                        if (modID != 0)
                                        {
                                            requiredMods.Add(modID);
                                        }
                                        else
                                        {
                                            Logger.UpdaterLog($"Steam ID not recognized for required mod: { modString }.", Logger.warning);
                                        }
                                    }
                                }
                                // Description - check length and source url
                                else if (line.Contains(ModSettings.SteamModPageDescriptionFind))
                                {
                                    // Skip one line
                                    line = reader.ReadLine();

                                    // Get the description
                                    int index = line.IndexOf(ModSettings.SteamModPageDescriptionLeft) + ModSettings.SteamModPageDescriptionLeft.Length;
                                    string description = line.Substring(index, line.Length - index - ModSettings.SteamModPageDescriptionRight.Length);

                                    // Consider mod to have no description if the description is shorter than the mod name
                                    noDescription = description.Length <= modName.Length;

                                    // Get the source url
                                    if (line.Contains(ModSettings.SteamModPageSourceURLLeft))
                                    {
                                        sourceURL = Tools.MidString(line, ModSettings.SteamModPageSourceURLLeft, ModSettings.SteamModPageSourceURLRight);

                                        // If source url was found, complete the url again
                                        if (!string.IsNullOrEmpty(sourceURL))
                                        {
                                            sourceURL = "https://github.com/" + sourceURL;
                                        }

                                        // Try to find a second source url
                                        string searchString = line;
                                        string secondSourceURL;

                                        while (searchString.IndexOf(ModSettings.SteamModPageSourceURLLeft) != searchString.LastIndexOf(ModSettings.SteamModPageSourceURLLeft)) 
                                        {
                                            // Set the start the search string to just after the first occurrence
                                            index = searchString.IndexOf(ModSettings.SteamModPageSourceURLLeft);
                                            searchString = line.Substring(index + 1, line.Length - index - 1);

                                            // Get the second source url
                                            secondSourceURL = Tools.MidString(searchString, ModSettings.SteamModPageSourceURLLeft, ModSettings.SteamModPageSourceURLRight);

                                            if (!string.IsNullOrEmpty(secondSourceURL))
                                            {
                                                secondSourceURL = "https://github.com/" + secondSourceURL;

                                                string oldSource = sourceURL.ToLower();
                                                string newSource = secondSourceURL.ToLower();

                                                // If the previously found source url contains wiki or issue, and the new one doesn't, use the new one; otherwise the old one
                                                if ((oldSource.Contains("wiki") || oldSource.Contains("issue")) && !newSource.Contains("wiki") && !newSource.Contains("issue"))
                                                {
                                                    // Keep the newly found
                                                    Logger.UpdaterLog($"Found more than one source url for [{ steamID }]: " +
                                                        $"\"{ secondSourceURL }\" (kept) and \"{ sourceURL }\" (discarded)");

                                                    sourceURL = secondSourceURL;
                                                }
                                                // If the previously found source url contains a reference to Pardeike Harmony, discard that one
                                                else if (oldSource.Contains("https://github.com/pardeike/harmony")) 
                                                {
                                                    // Keep the newly found
                                                    Logger.UpdaterLog($"Found more than one source url for [{ steamID }]: " +
                                                        $"\"{ secondSourceURL }\" (kept) and \"{ sourceURL }\" (discarded)");

                                                    sourceURL = secondSourceURL;
                                                }
                                                else
                                                {
                                                    // Keep the previously found
                                                    Logger.UpdaterLog($"Found more than one source url for [{ steamID }]: " +
                                                        $"\"{ sourceURL }\" (kept) and \"{ secondSourceURL }\" (discarded)");
                                                }
                                            }
                                        }
                                    }

                                    // Description is the last info we need from the page, so go on to updating the catalog for this mod
                                    moreLinesToRead = false;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.UpdaterLog($"Can't (fully) read or understand the mod page for [Steam ID { steamID }]. Skipping this mod.", Logger.warning);

                            Logger.Exception(ex, toUpdaterLog: true, duplicateToGameLog: false);

                            // Don't process any information for this mod; continue with the next mod
                            continue;
                        }
                    }

                    // Skip to the next mod if the Steam ID was not found on the downloaded page
                    if (!steamIDmatched)
                    {
                        Logger.UpdaterLog($"Can't find the Steam ID on downloaded page for [Steam ID { steamID }]. Mod info not updated.", Logger.error);

                        continue;
                    }

                    // Convert the found date strings to real datetime
                    DateTime published = Tools.ConvertWorkshopDateTime(publishedString);
                    DateTime updated = Tools.ConvertWorkshopDateTime(updatedString);

                    // New list for the required DLC
                    List<Enums.DLC> requiredDLC = new List<Enums.DLC>();

                    foreach (string dlcString in requiredDLCStrings)
                    {
                        try
                        {
                            // Convert the dlc-number strings to enum
                            requiredDLC.Add((Enums.DLC) Convert.ToUInt32(dlcString));
                        }
                        catch
                        {
                            Logger.UpdaterLog($"Cannot convert \"{ dlcString }\" to DLC enum for [Steam ID { steamID }].", Logger.warning);
                        }
                    }

                    // Remove incorrect steam IDs from the required mods list
                    foreach (ulong modID in requiredMods)
                    {
                        // Remove the mod ID if we didn't find it on the Workshop, or if it accidentally points to itself
                        if (!allSteamIDandNames.ContainsKey(modID) || (modID == steamID))
                        {
                            requiredMods.Remove(modID);

                            Logger.UpdaterLog($"Required mod [Steam ID { modID }] can't be found for [Steam ID { steamID }].", Logger.warning);
                        }
                    }

                    // Convert compatible gameversion back and forth to ensure a correctly formatted string
                    compatibleGameVersionString = GameVersion.Formatted(Tools.ConvertToGameVersion(compatibleGameVersionString));

                    // [Todo 0.2]
                    // vars to be used: (Steam ID), basicinfomod.Name, basicinfomod.AuthorID, published, updated, requiredDLC, requiredMods (check groups), sourceURL,
                    //                              compatibleVersion, statuses - IncompatibleAccordingToWorkshop & noDescription
                    //                  AuthorID, IDisProfile, Authorname
                    //                  indirect:   status RemovedFromWorkshop (false for everything we find, true for unfound but in catalog)
                    //                              AutoReviewUpdated = now,
                    //                              SourceUnavailable (if sourceURL)
                    //                              NeededFor (only update if it is already used for a mod)
                    //                              Author isRetired (false for new and updated mods)
                    //                              Author lastseen (updatedate if > current lastseen)

                    // Get the mod info found earlier
                    Mod basicInfoMod = collectedModInfo.Find(x => x.SteamID == steamID);

                    // Get the mod from the active catalog or create a new object
                    Mod catalogMod;

                    if (newMod)
                    {
                        // Add a new mod to the catalog
                        catalogMod = Catalog.Active.AddMod(steamID, basicInfoMod.Name, basicInfoMod.AuthorID);
                    }
                    else
                    {
                        // Get a reference to the mod in the catalog
                        catalogMod = Catalog.Active.ModDictionary[steamID];
                    }

                    // [Todo 0.2] Update the info in the catalog
                    // ...

                    // Not updated:         ArchiveURL, SucceededBy, Alternatives, Note, most Statuses, Compatibilities, Mod Groups
                    // Only half updated:   Author last seen, Author retired, NeededFor
                }
            }

            // Delete the temporary file
            Tools.DeleteFile(ModSettings.SteamWebpageFullPath);

            // Log the elapsed time
            timer.Stop();

            Logger.UpdaterLog($"Updater finished checking { knownModsDownloaded + newModsDownloaded } individual Steam Workshop mod pages in " + 
                $"{ (float)timer.ElapsedMilliseconds / 1000:F1} seconds.", duplicateToRegularLog: true);

            return true;
        }
    }
}
