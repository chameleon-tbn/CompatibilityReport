using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using ModChecker.DataTypes;
using static ModChecker.Util.ModSettings;

namespace ModChecker.Util
{
    internal static class Updater
    {        
        internal static bool GetAllModsFromSteam()
        {
            // Temporary catalog to use for collecting info from the Steam Workshop
            Catalog collectedWorkshopInfo = new Catalog();

            // Initialize counters
            bool morePagesToDownload = true;
            int pageNumber = 0;
            uint errorCount = 0;
            uint modsFound = 0;
            uint authorsFound = 0;

            Logger.Log("Updater started downloading Steam Workshop mod list pages.");

            // Time the total download and processing
            Stopwatch timer = Stopwatch.StartNew();

            // Activate TLS callback
            ServicePointManager.ServerCertificateValidationCallback += TLSCallback;

            // Download and read pages until we find no more pages, or we reach the set maximum number of pages (to avoid missing the mark and continuing for eternity)
            while (morePagesToDownload && pageNumber < SteamMaxPagesToDownload)
            {
                // Download a page
                using (WebClient webclient = new WebClient())
                {
                    try
                    {
                        // Increase the pagenumber and download the page
                        pageNumber++;

                        webclient.DownloadFile(SteamNewModsURL + $"&p={ pageNumber }", SteamNewModsFullPath);

                        // Page downloaded succesful; reset the errorcount
                        errorCount = 0;                        
                    }
                    catch (Exception ex)
                    {
                        // Download failed; lower the pageNumber to indicate this page didn't succeed
                        pageNumber--;

                        // Retry the same page a few times
                        errorCount++;

                        if (errorCount <= SteamMaxErrorCount)
                        {
                            Logger.Log($"Downloading Steam Workshop page { pageNumber } failed, will retry.");

                            Logger.Exception(ex, debugOnly: true);

                            // Retry downloading the same page
                            continue;
                        }
                        else
                        {
                            Logger.Log($"Permanent error while downloading Steam Workshop page { pageNumber }. Download process stopped.", Logger.error);

                            Logger.Exception(ex);

                            // Stop downloading and continue with already downloaded pages (if any)
                            break;
                        }
                    }
                }

                // Keep track of mods this page
                uint modsFoundThisPage = 0;

                using (StreamReader reader = File.OpenText(SteamNewModsFullPath))
                {
                    string line;
                    
                    try     // Limited error handling because the Updater is for me only
                    {
                        while ((line = reader.ReadLine()) != null)
                        {
                            // Search for the identifying string
                            if (line.Contains(HtmlSearchMod))
                            {
                                // Get the Steam ID
                                string SteamIDString = MidString(line, HtmlSearchBeforeID, HtmlSearchAfterID);

                                if (SteamIDString == "")
                                {
                                    Logger.Log("Steam ID not recognized on HTML line: " + line, Logger.debug);

                                    continue;   // To the next ReadLine
                                }

                                ulong steamID = Convert.ToUInt64(SteamIDString);

                                // Get the mod name
                                string name = MidString(line, HtmlSearchBeforeName, HtmlSearchAfterName);

                                // The author is on the next line, so read that
                                line = reader.ReadLine();

                                // Try to get the author ID
                                bool authorIDIsProfile = false;

                                string authorID = MidString(line, HtmlSearchBeforeAuthorID, HtmlSearchAfterAuthorID);

                                if (authorID == "")
                                {
                                    // Author ID not found, get the profile number instead
                                    authorID = MidString(line, HtmlSearchBeforeAuthorProfile, HtmlSearchAfterAuthorID);

                                    authorIDIsProfile = true;
                                }

                                // Get the author name
                                string authorName = MidString(line, HtmlSearchBeforeAuthorName, HtmlSearchAfterAuthorName);

                                // Create a mod and author object from the information gathered
                                Mod mod = new Mod(steamID, name, authorID, removed: false);
                                ModAuthor author = new ModAuthor(authorID, authorIDIsProfile, authorName);

                                // Add the newly found mod to the collection; duplicates could in theory happen when a new mod is published in our 30 seconds of downloading
                                collectedWorkshopInfo.Mods.Add(mod);

                                modsFound++;
                                modsFoundThisPage++;
                                    
                                Logger.Log($"Mod found: { mod.ToString() }", Logger.debug);
                                
                                // Add the newly found author to the collection; avoid duplicates
                                if (!collectedWorkshopInfo.ModAuthors.Exists(i => i.ID == authorID))
                                {
                                    collectedWorkshopInfo.ModAuthors.Add(author);

                                    authorsFound++;

                                    Logger.Log($"Author found: [{ author.ID }] { authorName }", Logger.debug);
                                }                                
                            }
                            else if (line.Contains(HtmlSearchNoMoreFound))
                            {
                                Logger.Log($"Page { pageNumber } didn't contain any mods. No more pages to download.", Logger.debug);

                                // We reach a page without mods
                                morePagesToDownload = false;

                                // Decrease pageNumber to indicate this page was no good
                                pageNumber--;
                            }
                        }

                        if (morePagesToDownload)
                        {
                            Logger.Log($"Found { modsFoundThisPage } mods on page { pageNumber }.");
                        }                        
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Couldn't (fully) read or understand downloaded page { pageNumber }. { modsFoundThisPage } mods found on this page.", Logger.error);

                        Logger.Exception(ex);
                    }
                }
            }   // End of while (morePagesToDownload) loop

            // Deactivate TLS callback
            ServicePointManager.ServerCertificateValidationCallback -= TLSCallback;

            // Delete the temporary file
            if (File.Exists(SteamNewModsFullPath))
            {
                try
                {
                    File.Delete(SteamNewModsFullPath);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Could not delete temporary file \"{ SteamNewModsFullPath }\".", Logger.debug);

                    Logger.Exception(ex, debugOnly: true, gameLog: false);
                }
            }

            // Log the elapsed time
            timer.Stop();

            Logger.Log($"Updater finished downloading Steam Workshop mod list pages in { timer.ElapsedMilliseconds / 1000:F1} seconds. { modsFound } mods and { authorsFound } authors found.");

            return (pageNumber > 0) && (modsFound > 0);
        }


        // Get the substring between two search-string in a string
        private static string MidString(string original, string leftBoundary, string rightBoundary)
        {
            // Get the position of the left boundary string
            int indexLeft = original.IndexOf(leftBoundary);

            if (indexLeft < 1)
            {
                // Left boundary string not found
                return "";
            }

            // Increase the left boundary index to the end of the left boundary string
            indexLeft += leftBoundary.Length;

            // Get the position of the right boundary string
            int indexRight = original.IndexOf(rightBoundary, indexLeft);

            if (indexRight < indexLeft)
            {
                // Right boundary string not found
                return "";
            }

            return original.Substring(indexLeft, indexRight - indexLeft);
        }


        // Unfinished: Download each individual mod page
        // Info to find: ...
        // Info to deduct: mod update date = author last seen date if its more recent than current last seen date
    }
}
