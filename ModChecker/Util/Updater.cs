using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ModChecker.DataTypes;
using static ModChecker.Util.ModSettings;

namespace ModChecker.Util
{
    internal static class Updater       // Unfinished: Keep this here or in a separate out-of-game program?
    {
        // Catalog and list to collect info from the Steam Workshop
        private static readonly Catalog collectedWorkshopInfo = new Catalog(0, DateTime.Now, "Basic mod and author information from the Steam Workshop.");

        private static readonly List<ulong> AllSteamIDs = new List<ulong>();


        // Start the updater
        internal static bool Start()
        {
            bool success = false;

            // Only if have a valid active catalog
            if (Catalog.Active?.IsValid == true)
            {
                // Get basic mod and author information from the Steam Workshop mods listing pages
                if (GetBasicModAndAuthorInfo())
                {
                    // Get more detailed information from the individual mod pages
                    if (GetDetailedModInfo())
                    {
                        // Update active catalog
                        success = UpdateActiveCatalog();
                    }
                }
            }

            return success;
        }
        

        // Get mod and author names and IDs from the Steam Workshop mod list pages
        private static bool GetBasicModAndAuthorInfo()
        {
            // Initialize the updater logfile
            Logger.InitUpdaterLog();

            // Initialize counters
            bool morePagesToDownload = true;
            uint pageNumber = 0;
            uint modsFound = 0;
            uint authorsFound = 0;

            Logger.UpdaterLog("Updater started downloading Steam Workshop mod list pages. This should take about 30 to 40 seconds. See separate logfile for details.", 
                regularLog: true);

            // Time the total download and processing
            Stopwatch timer = Stopwatch.StartNew();

            // Download and read pages until we find no more pages, or we reach the set maximum number of pages (to avoid missing the mark and continuing for eternity)
            while (morePagesToDownload && (pageNumber < SteamMaxPages))
            {
                // Increase the pagenumber
                pageNumber++;

                // Download a page
                Exception ex = Tools.Download(SteamModsListingURL + $"&p={ pageNumber }", SteamWebpageFullPath, SteamDownloadRetries);

                if (ex != null)
                {
                    Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page { pageNumber }. Download process stopped.", Logger.error);

                    Logger.Exception(ex, toUpdaterLog: true);

                    // Lower the pageNumber to the last succesful page
                    pageNumber--;
                        
                    // Stop downloading and continue with already downloaded pages (if any)
                    break;
                }

                // Keep track of mods this page
                uint modsFoundThisPage = 0;

                using (StreamReader reader = File.OpenText(SteamWebpageFullPath))
                {
                    string line;
                    
                    try     // Limited error handling because the Updater is not for regular users
                    {
                        // Read all the lines until the end of the file or until we found all the mods we can find on one page
                        // Note: 95% of process time is download; skipping the first 65KB (900+ lines) with 'reader.Basestream.Seek' does nothing for speed
                        while ((line = reader.ReadLine()) != null)
                        {
                            // Search for the identifying string
                            if (line.Contains(SteamHtmlSearchMod))
                            {
                                // Get the Steam ID
                                string SteamIDString = Tools.MidString(line, SteamHtmlBeforeModID, SteamHtmlAfterModID);

                                if (SteamIDString == "")
                                {
                                    Logger.UpdaterLog("Steam ID not recognized on HTML line: " + line, Logger.warning);

                                    continue;   // To the next ReadLine
                                }

                                ulong steamID = Convert.ToUInt64(SteamIDString);

                                // Get the mod name
                                string name = Tools.MidString(line, SteamHtmlBeforeModName, SteamHtmlAfterModName);

                                // The author is on the next line
                                line = reader.ReadLine();

                                // Try to get the author ID
                                bool authorIDIsProfile = false;

                                string authorID = Tools.MidString(line, SteamHtmlBeforeAuthorID, SteamHtmlAfterAuthorID);

                                if (authorID == "")
                                {
                                    // Author ID not found, get the profile number instead (as a string)
                                    authorID = Tools.MidString(line, SteamHtmlBeforeAuthorProfile, SteamHtmlAfterAuthorID);

                                    authorIDIsProfile = true;
                                }

                                // Get the author name
                                string authorName = Tools.MidString(line, SteamHtmlBeforeAuthorName, SteamHtmlAfterAuthorName);

                                // Create a mod and author object from the information gathered
                                Mod mod = new Mod(steamID, name, authorID, removed: false);
                                ModAuthor author = new ModAuthor(authorID, authorIDIsProfile, authorName);

                                // Add the newly found mod to the collection and list; duplicates could in theory happen if a new mod is published while downloading
                                collectedWorkshopInfo.Mods.Add(mod);
                                
                                AllSteamIDs.Add(steamID);

                                modsFound++;
                                modsFoundThisPage++;
                                    
                                Logger.UpdaterLog($"Mod found: { mod.ToString() }", Logger.debug);
                                
                                // Add the newly found author to the collection; avoid duplicates
                                if (!collectedWorkshopInfo.ModAuthors.Exists(i => i.ID == authorID))
                                {
                                    collectedWorkshopInfo.ModAuthors.Add(author);

                                    authorsFound++;

                                    Logger.UpdaterLog($"Author found: [{ author.ID }] { authorName }", Logger.debug);
                                }
                            }
                            else if (line.Contains(SteamHtmlNoMoreFound))
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
                        Logger.UpdaterLog($"Couldn't (fully) read or understand downloaded page { pageNumber }. { modsFoundThisPage } mods found on this page.", 
                            Logger.error);

                        Logger.Exception(ex2, toUpdaterLog: true, duplicateToGameLog: false);
                    }
                }
            }   // End of while (morePagesToDownload) loop

            // Delete the temporary file
            Tools.DeleteFile(SteamWebpageFullPath);

            // Log the elapsed time
            timer.Stop();

            Logger.UpdaterLog($"Updater finished downloading Steam Workshop mod list pages in { (float)timer.ElapsedMilliseconds / 1000:F1} seconds. " + 
                $"{ modsFound } mods and { authorsFound } authors found.", regularLog: true);

            // Unfinished: temporary save the catalog for testing/debugging
            collectedWorkshopInfo.Save(SteamWebpageFullPath + ".xml");

            return (pageNumber > 0) && (modsFound > 0);
        }


        // Get mod information from the individual mod pages on the Steam Workshop
        private static bool GetDetailedModInfo()
        {
            // Initialize counters
            uint knownModsChecked = 0;
            uint newModsChecked = 0;
            uint failedModDownloads = 0;

            Logger.UpdaterLog("Updater started downloading individual Steam Workshop mod pages. This should take less than two minutes.", regularLog: true);
            
            // Time the total download and processing
            Stopwatch timer = Stopwatch.StartNew();

            // Check all SteamIDs from the mods we gathered
            foreach (ulong steamID in AllSteamIDs)
            {
                // Check if mod is already in the active catalog
                if (Catalog.Active.ModDictionary.ContainsKey(steamID))
                {
                    // Known mod; continue to next if we reached the maximum known mods to check
                    if (knownModsChecked >= SteamMaxPages)
                    {
                        continue;
                    }

                    // Unfinished: What to do? Downloading 1600 pages takes about 10 to 15 minutes; just do 100 and save which we haven't done yet?

                    // Download the Steam Workshop mod page
                    if (Tools.Download(Tools.GetWorkshopURL(steamID), SteamWebpageFullPath, SteamDownloadRetries) == null)
                    {
                        // Unfinished: get ...; author last seen = mod update date (if newer); split into new and updated mods; max number of new mods; ...
                        // Unfinished: automatic catalog change notes

                        // Increase the counter
                        knownModsChecked++;

                        Logger.UpdaterLog($"Downloaded details page for known mod [Steam ID { steamID }]", Logger.debug);
                    }
                    else
                    {
                        // Download error
                        failedModDownloads++;

                        if (failedModDownloads <= 5)
                        {
                            // Download error might be mod specific. Go to the next mod.
                            Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for [Steam ID { steamID }]. Will continue with next mod.",
                                Logger.error);
                        }
                    }
                }
                else
                {
                    // New mod; continue to next if we reached the maximum new mods to check
                    if (newModsChecked >= SteamMaxPages)
                    {
                        continue;
                    }
                    
                    // Download the Steam Workshop mod page
                    if (Tools.Download(Tools.GetWorkshopURL(steamID), SteamWebpageFullPath, SteamDownloadRetries) == null)
                    {
                        // Unfinished: get ...; author last seen = mod update date (if newer); split into new and updated mods; max number of new mods; ...
                        // Unfinished: automatic catalog change notes

                        // Increase the counter
                        newModsChecked++;

                        Logger.UpdaterLog($"Downloaded details page for new mod [Steam ID { steamID }]", Logger.debug);
                    }
                    else
                    {
                        // Download error
                        failedModDownloads++;

                        if (failedModDownloads <= 5)
                        {
                            // Download error might be mod specific. Go to the next mod.
                            Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for [Steam ID { steamID }]. Will continue with next mod.", 
                                Logger.error);
                        }
                    }
                }

                if (failedModDownloads > 5)
                {
                    // Too many failed downloads. Stop downloading
                    Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for [Steam ID { steamID }]. Download process stopped.",
                        Logger.error);

                    break;
                }
            }

            // Delete the temporary file
            Tools.DeleteFile(SteamWebpageFullPath);

            // Log the elapsed time
            timer.Stop();

            Logger.UpdaterLog($"Updater finished downloading { knownModsChecked + newModsChecked } individual Steam Workshop mod pages in " + 
                $"{ (float)timer.ElapsedMilliseconds / 1000:F1} seconds.", regularLog: true);

            return true;
        }


        // 
        internal static bool UpdateActiveCatalog()
        {
            // Unfinished: update active catalog and save it with automatic change notes; automatic version++; detect mod and author name change

            return true;
        }
    }
}
