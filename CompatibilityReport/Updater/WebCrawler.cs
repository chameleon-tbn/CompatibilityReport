using System;
using System.Diagnostics;
using System.IO;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

// WebCrawler gathers information from the Steam Workshop pages for all mods and updates the catalog with this. This process takes roughly 15 minutes.
// The following information is gathered:
// * Mod: name, author, publish and update dates, source URL (GitHub only), compatible game version (from tag), required DLCs, required mods, incompatible stability
//        statuses: removed from or unlisted in the Steam Workshop, no description
// * Author: name, Steam ID and Custom URL, last seen date (based on mod updates, not on comments), retired status (no mod update in x months; removed on new mod update)

namespace CompatibilityReport.Updater
{
    public static class WebCrawler
    {
        // Start the WebCrawler. Download Steam webpages for all mods and update the catalog with found information.
        public static void Start(Catalog catalog)
        {
            CatalogUpdater.SetReviewDate(DateTime.Now);

            if (GetBasicInfo(catalog))
            {
                GetDetails(catalog);
            }
        }
        

        // Get mod and author names and IDs from the Steam Workshop 'mod listing' pages. Return true if we found at least one mod.
        private static bool GetBasicInfo(Catalog catalog)
        {
            Logger.UpdaterLog("Updater started downloading Steam Workshop 'mod listing' pages. This should take less than 1 minute.");

            Stopwatch timer = Stopwatch.StartNew();
            int totalMods = 0;
            int totalPages = 0;
            
            // Go through the different mod listings: mods and camera scripts, both regular and incompatible.
            foreach (string steamURL in ModSettings.SteamModListingURLs)
            {
                Logger.UpdaterLog($"Starting downloads from { steamURL }");
                
                int pageNumber = 0;

                // Download and read pages until we find no more mods, or we reach a maximum number of pages, to avoid missing the mark and continuing for eternity.
                while (pageNumber < ModSettings.SteamMaxModListingPages)
                {
                    pageNumber++;
                    string url = $"{ steamURL }&p={ pageNumber }";

                    if (!Toolkit.Download(url, ModSettings.TempDownloadFullPath))
                    {
                        pageNumber--;

                        Logger.UpdaterLog($"Download process interrupted due to a permanent error while downloading { url }", Logger.Error);
                        break;
                    }

                    int modsFoundThisPage = ReadModListingPage(catalog, incompatibleMods: steamURL.Contains("incompatible"));

                    if (modsFoundThisPage == 0)
                    {
                        pageNumber--;

                        if (pageNumber == 0)
                        {
                            Logger.UpdaterLog("Found no mods on page 1.");
                        }

                        break;
                    }

                    totalMods += modsFoundThisPage;
                    Logger.UpdaterLog($"Found { modsFoundThisPage } mods on page { pageNumber }.");
                }

                totalPages += pageNumber;
            }

            Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);

            // Note: about 75% of the total time is downloading, the other 25% is processing.
            timer.Stop();
            Logger.UpdaterLog($"Updater finished downloading { totalPages } Steam Workshop 'mod listing' pages in " +
                $"{ Toolkit.TimeString(timer.ElapsedMilliseconds) }. { totalMods } mods found.");

            return totalMods > 0;
        }


        // Extract mod info from the downloaded mod listing page and add new mods to the catalog. Returns the number of mods found on this page.
        private static int ReadModListingPage(Catalog catalog, bool incompatibleMods)
        {
            int modsFoundThisPage = 0;
            string line;

            using (StreamReader reader = File.OpenText(ModSettings.TempDownloadFullPath))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    // Search for the identifying string for the next mod; continue with next line if not found.
                    if (!line.Contains(ModSettings.SearchModStart))
                    {
                        continue;
                    }

                    ulong steamID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.SearchSteamIDLeft, ModSettings.SearchSteamIDRight));

                    if (steamID == 0) 
                    {
                        Logger.UpdaterLog("Steam ID not recognized on HTML line: " + line, Logger.Error);
                        continue;
                    }

                    modsFoundThisPage++;

                    string modName = Toolkit.CleanHtml(Toolkit.MidString(line, ModSettings.SearchListingModNameLeft, ModSettings.SearchListingModNameRight));

                    Mod catalogMod = catalog.GetMod(steamID) ?? CatalogUpdater.AddMod(catalog, steamID, modName, incompatibleMods);

                    CatalogUpdater.RemoveStatus(catalogMod, Enums.Status.RemovedFromWorkshop, updatedByWebCrawler: true);
                    CatalogUpdater.RemoveStatus(catalogMod, Enums.Status.UnlistedInWorkshop, updatedByWebCrawler: true);

                    if (incompatibleMods && catalogMod.Stability != Enums.Stability.IncompatibleAccordingToWorkshop)
                    {
                        CatalogUpdater.UpdateMod(catalog, catalogMod, stability: Enums.Stability.IncompatibleAccordingToWorkshop, stabilityNote: "", 
                            updatedByWebCrawler: true);
                    }
                    else if (!incompatibleMods && catalogMod.Stability == Enums.Stability.IncompatibleAccordingToWorkshop)
                    {
                        CatalogUpdater.UpdateMod(catalog, catalogMod, stability: Enums.Stability.NotReviewed, stabilityNote: "", updatedByWebCrawler: true);
                    }

                    CatalogUpdater.UpdateMod(catalog, catalogMod, modName, alwaysUpdateReviewDate: true, updatedByWebCrawler: true);

                    // Author info can be found on the next line, but skip it here and get it later on the mod page.
                }
            }

            return modsFoundThisPage;
        }


        // Get mod information from the individual mod pages on the Steam Workshop.
        private static void GetDetails(Catalog catalog)
        {
            Stopwatch timer = Stopwatch.StartNew();

            int numberOfMods = catalog.Mods.Count - ModSettings.BuiltinMods.Count;
            long estimate = 500 * numberOfMods;

            Logger.UpdaterLog($"Updater started downloading { numberOfMods } individual Steam Workshop mod pages. Estimated time: { Toolkit.TimeString(estimate) }.");

            int modsDownloaded = 0;
            int failedDownloads = 0;

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (!catalog.IsValidID(catalogMod.SteamID, allowBuiltin: false))
                {
                    // Skip builtin mods.
                    continue;
                }

                if (!Toolkit.Download(Toolkit.GetWorkshopUrl(catalogMod.SteamID), ModSettings.TempDownloadFullPath))
                {
                    failedDownloads++;

                    if (failedDownloads <= ModSettings.SteamMaxFailedPages)
                    {
                        Logger.UpdaterLog("Permanent error while downloading Steam Workshop page for { catalogMod.ToString() }. Will continue with the next mod.", 
                            Logger.Error);

                        continue;
                    }
                    else
                    {
                        Logger.UpdaterLog("Permanent error while downloading Steam Workshop page for { catalogMod.ToString() }. Download process stopped.", 
                            Logger.Error);

                        break;
                    }
                }

                modsDownloaded++;

                if (modsDownloaded % 100 == 0)
                {
                    Logger.UpdaterLog($"{ modsDownloaded }/{ numberOfMods } mod pages downloaded.");
                }

                if (!ReadModPage(catalog, catalogMod))
                {
                    // Redownload and try one more time, to work around cut-off downloads.
                    Toolkit.Download(Toolkit.GetWorkshopUrl(catalogMod.SteamID), ModSettings.TempDownloadFullPath);

                    ReadModPage(catalog, catalogMod);
                }
            }

            Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);

            // Note: about 90% of the total time is downloading, the other 10% is processing.
            timer.Stop();
            Logger.UpdaterLog($"Updater finished downloading { modsDownloaded } individual Steam Workshop mod pages in " + 
                $"{ Toolkit.TimeString(timer.ElapsedMilliseconds, alwaysShowSeconds: true) }.");

            Logger.Log($"Updater processed { modsDownloaded } Steam Workshop mod pages.");
        }


        // Extract detailed mod information from the downloaded mod page and update the catalog. Return false if there was an error with the mod page.
        private static bool ReadModPage(Catalog catalog, Mod catalogMod)
        {
            bool steamIDmatched = false;
            string line;

            using (StreamReader reader = File.OpenText(ModSettings.TempDownloadFullPath))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    // Only continue when we have found the correct Steam ID.
                    if (!steamIDmatched)
                    {
                        steamIDmatched = line.Contains($"{ ModSettings.SearchSteamID }{catalogMod.SteamID}");

                        if (steamIDmatched)
                        {
                            CatalogUpdater.RemoveStatus(catalogMod, Enums.Status.RemovedFromWorkshop, updatedByWebCrawler: true);

                            if (!catalogMod.UpdatedThisSession)
                            {
                                CatalogUpdater.AddStatus(catalogMod, Enums.Status.UnlistedInWorkshop, updatedByWebCrawler: true);
                                CatalogUpdater.UpdateMod(catalog, catalogMod, alwaysUpdateReviewDate: true, updatedByWebCrawler: true);
                            }
                        }

                        else if (line.Contains(ModSettings.SearchItemNotFound))
                        {
                            if (catalogMod.UpdatedThisSession)
                            {
                                Logger.UpdaterLog($"We found this mod, but can't read the Steam page for { catalogMod.ToString() }. Mod info not updated.", Logger.Error);
                                return false;
                            }
                            else
                            {
                                CatalogUpdater.AddStatus(catalogMod, Enums.Status.RemovedFromWorkshop, updatedByWebCrawler: true);
                                return true;
                            }
                        }

                        continue;
                    }

                    // Author Steam ID, Custom URL and author name.
                    // Todo 0.4 Add a check for author URL changes, to prevent creating a new author.
                    if (line.Contains(ModSettings.SearchAuthorLeft))
                    {
                        // Only get the author URL if the author ID was not found, to prevent updating the author URL to an empty string.
                        ulong authorID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.SearchAuthorLeft + "profiles/", ModSettings.SearchAuthorMid));
                        string authorUrl = authorID != 0 ? null : Toolkit.MidString(line, ModSettings.SearchAuthorLeft + "id/", ModSettings.SearchAuthorMid);
                        string authorName = Toolkit.CleanHtml(Toolkit.MidString(line, ModSettings.SearchAuthorMid, ModSettings.SearchAuthorRight));

                        Author catalogAuthor = catalog.GetAuthor(authorID, authorUrl) ?? CatalogUpdater.AddAuthor(catalog, authorID, authorUrl, authorName);
                        CatalogUpdater.UpdateAuthor(catalogAuthor, name: authorName);

                        CatalogUpdater.UpdateMod(catalog, catalogMod, authorID: catalogAuthor.SteamID, authorUrl: catalogAuthor.CustomUrl, updatedByWebCrawler: true);
                    }

                    // Mod name.
                    else if (line.Contains(ModSettings.SearchModNameLeft))
                    {
                        string modName = Toolkit.CleanHtml(Toolkit.MidString(line, ModSettings.SearchModNameLeft, ModSettings.SearchModNameRight)); 

                        CatalogUpdater.UpdateMod(catalog, catalogMod, modName, updatedByWebCrawler: true);
                    }

                    // Compatible game version tag
                    else if (line.Contains(ModSettings.SearchVersionTag))
                    {
                        // Convert the found tag to a game version and back to a formatted game version string, so we have a consistently formatted string.
                        string gameVersionString = Toolkit.MidString(line, ModSettings.SearchVersionTagLeft, ModSettings.SearchVersionTagRight);
                        Version gameVersion = Toolkit.ConvertToGameVersion(gameVersionString);
                        gameVersionString = Toolkit.ConvertGameVersionToString(gameVersion);

                        if (!catalogMod.ExclusionForGameVersion || gameVersion >= catalogMod.CompatibleGameVersion())
                        {
                            CatalogUpdater.UpdateMod(catalog, catalogMod, compatibleGameVersionString: gameVersionString, updatedByWebCrawler: true);
                            catalogMod.UpdateExclusions(exclusionForGameVersion: false);
                        }
                    }

                    // Publish and update dates.
                    else if (line.Contains(ModSettings.SearchDates))
                    {
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        DateTime published = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.SearchDatesLeft, ModSettings.SearchDatesRight));

                        line = reader.ReadLine();
                        DateTime updated = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.SearchDatesLeft, ModSettings.SearchDatesRight));

                        CatalogUpdater.UpdateMod(catalog, catalogMod, published: published, updated: updated, updatedByWebCrawler: true);
                    }

                    // Required DLC. This line can be found multiple times.
                    // Todo 0.4 Remove DLCs no longer required.
                    else if (line.Contains(ModSettings.SearchRequiredDLC))
                    {
                        line = reader.ReadLine();
                        Enums.Dlc dlc = Toolkit.ConvertToEnum<Enums.Dlc>(Toolkit.MidString(line, ModSettings.SearchRequiredDLCLeft, ModSettings.SearchRequiredDLCRight));

                        if (!catalogMod.ExclusionForRequiredDlc.Contains(dlc))
                        {
                            CatalogUpdater.AddRequiredDLC(catalogMod, dlc);
                        }
                    }

                    // Required mods and assets. The search string is a container with all required items on the next lines.
                    // Todo 0.4 Remove mods no longer required.
                    else if (line.Contains(ModSettings.SearchRequiredMod))
                    {
                        // Get all required items from the next lines, until we find no more. Max. 50 times to avoid an infinite loop.
                        for (var i = 1; i <= 50; i++)
                        {
                            line = reader.ReadLine();
                            ulong requiredID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.SearchRequiredModLeft, ModSettings.SearchRequiredModRight));

                            if (requiredID == 0)
                            {
                                break;
                            }

                            if (!catalogMod.ExclusionForRequiredMods.Contains(requiredID))
                            {
                                CatalogUpdater.AddRequiredMod(catalog, catalogMod, requiredID, updatedByWebCrawler: true);
                            }

                            line = reader.ReadLine();
                            line = reader.ReadLine();
                            line = reader.ReadLine();
                        }
                    }

                    // Description for 'no description' status and for source URL.
                    else if (line.Contains(ModSettings.SearchDescription))
                    {
                        line = reader.ReadLine();

                        // The complete description is on one line.
                        string description = Toolkit.MidString(line, ModSettings.SearchDescriptionLeft, ModSettings.SearchDescriptionRight);

                        // A 'no description' status is when the description is not at least a few characters longer than the mod name.
                        if (description.Length <= catalogMod.Name.Length + 3 && !catalogMod.ExclusionForNoDescription)
                        {
                            CatalogUpdater.AddStatus(catalogMod, Enums.Status.NoDescription, updatedByWebCrawler: true);
                        }
                        else if (description.Length > catalogMod.Name.Length + 3 && !catalogMod.ExclusionForNoDescription)
                        {
                            CatalogUpdater.RemoveStatus(catalogMod, Enums.Status.NoDescription, updatedByWebCrawler: true);
                        }

                        if (description.Contains(ModSettings.SearchSourceURLLeft) && !catalogMod.ExclusionForSourceUrl)
                        {
                            CatalogUpdater.UpdateMod(catalog, catalogMod, sourceURL: GetSourceURL(description, catalogMod), updatedByWebCrawler: true);
                        }

                        // Description is the last info we need from the page, so break out of the while loop.
                        break;
                    }

                    // Todo 0.4 Can we get the NoCommentSection status automatically?
                }
            }

            if (!steamIDmatched && !catalogMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
            {
                // We didn't find a Steam ID on the page, but no error page either. Must be a download issue or another Steam error.
                Logger.UpdaterLog($"Can't find the Steam ID on downloaded page for { catalogMod.ToString() }. Mod info not updated.", Logger.Error);
                return false;
            }

            return true;
        }


        // Get the source URL. If more than one is found, pick the most likely, which is far from perfect and might need a CSV update to set it right.
        private static string GetSourceURL(string modDescription, Mod catalogMod)
        {
            string sourceURL = "https://github.com/" + Toolkit.MidString(modDescription, ModSettings.SearchSourceURLLeft, ModSettings.SearchSourceURLRight);
            string currentLower = sourceURL.ToLower();

            if (sourceURL == "https://github.com/")
            {
                return null;
            }

            // Some commonly listed source URLs to always ignore: Pardeike's Harmony and Sschoener's detour
            const string pardeike  = "https://github.com/pardeike";
            const string sschoener = "https://github.com/sschoener/cities-skylines-detour";

            string discardedURLs = "";
            int tries = 0;

            // Keep comparing source URLs until we find no more. Max. 50 times to avoid infinite loops.
            while (modDescription.IndexOf(ModSettings.SearchSourceURLLeft) != modDescription.LastIndexOf(ModSettings.SearchSourceURLLeft) && tries < 50)
            {
                tries++;

                int index = modDescription.IndexOf(ModSettings.SearchSourceURLLeft) + 1;
                modDescription = modDescription.Substring(index);

                string nextSourceURL = "https://github.com/" + Toolkit.MidString(modDescription, ModSettings.SearchSourceURLLeft, ModSettings.SearchSourceURLRight);
                string nextLower = nextSourceURL.ToLower();

                // Decide on which source URL to use. Silently discard any source URL containing Pardeike or Sschoener, as well as empty and duplicate URLs.
                if (nextLower == "https://github.com/" || nextLower.Contains(pardeike) || nextLower.Contains(sschoener) || nextLower == currentLower)
                {
                    // Nothing to do here.
                }
                else if (currentLower.Contains(pardeike) || currentLower.Contains(sschoener))
                {
                    sourceURL = nextSourceURL;
                }
                else if (currentLower.Contains("issue") || currentLower.Contains("wiki") || currentLower.Contains("documentation") 
                    || currentLower.Contains("readme") || currentLower.Contains("guide") || currentLower.Contains("translation"))
                {
                    discardedURLs += "\n                      Discarded: " + sourceURL;
                    sourceURL = nextSourceURL;
                }
                else
                {
                    discardedURLs += "\n                      Discarded: " + nextSourceURL;
                }

                currentLower = sourceURL.ToLower();
            }

            // Discard the selected source URL if it is Pardeike or Sschoener. This happens when that is the only GitHub link in the description.
            if (currentLower.Contains(pardeike) || currentLower.Contains(sschoener))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(discardedURLs) && sourceURL != catalogMod.SourceUrl)
            {
                Logger.UpdaterLog($"Found multiple source URLs for { catalogMod.ToString() }\n                      Selected:  { sourceURL }{ discardedURLs }");
            }

            return sourceURL;
        }
    }
}
