using System;
using System.Diagnostics;
using System.IO;
using ModChecker.DataTypes;
using ModChecker.Util;


namespace ModChecker
{
    internal static class ActiveCatalog
    {
        // Instance for the active catalog
        internal static Catalog Instance { get; private set; }

        // Did we download a catalog already this session
        private static bool downloadedValidCatalog;


        // Load and download catalogs and make the newest the active catalog
        internal static bool Init()
        {
            if (Instance == null)
            {
                // Load the catalog that was included with the mod
                Catalog bundledCatalog = LoadBundled();

                // Load the downloaded catalog, either a previously downloaded or a newly downloaded catalog, whichever is newest
                Catalog downloadedCatalog = Download();

                // The newest catalog becomes the active catalog; if both are the same version, use the bundled catalog
                Instance = Newest(bundledCatalog, downloadedCatalog);

                if (Instance != null)
                {
                    // Prepare the active catalog for searching
                    Instance.CreateIndex();
                }
            }

            return Instance != null;
        }


        // Close the active catalog
        internal static void Close()
        {
            // Nullify the active catalog
            Instance = null;

            Logger.Log("Catalog closed.");
        }


        // Load bundled catalog
        private static Catalog LoadBundled()
        {
            Catalog catalog = Catalog.Load(ModSettings.bundledCatalogFullPath);

            if (catalog == null)
            {
                Logger.Log($"Can't load bundled catalog. { ModSettings.pleaseReportText }", Logger.error, duplicateToGameLog: true);
            }
            else
            {
                Logger.Log($"Bundled catalog { catalog.VersionString() } loaded.");
            }

            return catalog;
        }


        // Check for a previously downloaded catalog, download a new catalog and activate the newest of the two
        private static Catalog Download()
        {
            // Catalog instance for a previously downloaded catalog
            Catalog previousCatalog = null;

            // Check if previously downloaded catalog exists
            if (!File.Exists(ModSettings.downloadedCatalogFullPath))
            {
                // Did not exist
                Logger.Log("No previously downloaded catalog exists. This is expected when the mod has never downloaded a new catalog.");
            }
            else
            {
                // Exists, try to load it
                previousCatalog = Catalog.Load(ModSettings.downloadedCatalogFullPath);

                if (previousCatalog != null)
                {
                    Logger.Log($"Previously downloaded catalog { previousCatalog.VersionString() } loaded.");
                }
                // Can't be loaded; try to delete it
                else if (Tools.DeleteFile(ModSettings.downloadedCatalogFullPath))
                {
                    Logger.Log("Coud not load previously downloaded catalog. It has been deleted.", Logger.warning);
                }
                else
                {
                    // Can't be deleted
                    Logger.Log("Can't load previously downloaded catalog and it can't be deleted either. " +
                        "This prevents saving a newly downloaded catalog for future sessions.", Logger.error);
                }
            }

            // If we already downloaded this session, exit returning the previously downloaded catalog (could be null if it was manually deleted)
            if (downloadedValidCatalog)
            {
                return previousCatalog;
            }

            // Temporary filename for the newly downloaded catalog
            string newCatalogTemporaryFullPath = ModSettings.downloadedCatalogFullPath + ".part";

            // Delete temporary catalog if it was left over from a previous session; exit if we can't delete it
            if (!Tools.DeleteFile(newCatalogTemporaryFullPath))
            {
                Logger.Log("Partially downloaded catalog still existed from a previous session and couldn't be deleted. This prevents a new download.", Logger.error);

                return previousCatalog;
            }

            // Download new catalog and time it
            Stopwatch timer = Stopwatch.StartNew();

            Exception exception = Tools.Download(ModSettings.catalogURL, newCatalogTemporaryFullPath);

            timer.Stop();

            if (exception == null)
            {
                Logger.Log($"Catalog downloaded in { timer.ElapsedMilliseconds / 1000:F1} seconds from { ModSettings.catalogURL }");
            }
            else
            {
                Logger.Log($"Can't download catalog from { ModSettings.catalogURL }", Logger.warning);

                // Check if the issue is TLS 1.2; only log regular exception if it isn't
                if (exception.ToString().Contains("Security.Protocol.Tls.TlsException: The authentication or decryption has failed"))
                {
                    Logger.Log("It looks like the webserver only supports TLS 1.2 or higher, while Cities: Skylines modding only supports TLS 1.1 and lower.");

                    Logger.Exception(exception, debugOnly: true, duplicateToGameLog: false);
                }
                else
                {
                    Logger.Exception(exception);
                }

                // Delete empty temporary file
                Tools.DeleteFile(newCatalogTemporaryFullPath);

                // Exit
                return previousCatalog;
            }

            // Load newly downloaded catalog
            Catalog newCatalog = Catalog.Load(newCatalogTemporaryFullPath);

            if (newCatalog == null)
            {
                Logger.Log("Could not load newly downloaded catalog.", Logger.error);
            }
            else
            {
                Logger.Log($"Downloaded catalog { newCatalog.VersionString() } loaded.");

                // Make newly downloaded valid catalog the previously downloaded catalog, if it is newer (only determinend by Version, independent of StructureVersion)
                if ((previousCatalog == null) || (previousCatalog.Version < newCatalog.Version))
                {
                    // Copy the temporary file over the previously downloaded catalog; indicate we downloaded a valid catalog, so we won't do that again this session
                    downloadedValidCatalog = Tools.CopyFile(newCatalogTemporaryFullPath, ModSettings.downloadedCatalogFullPath);
                }
            }

            // Delete temporary file
            Tools.DeleteFile(newCatalogTemporaryFullPath);

            // Return the newest catalog or null if both are null; if both are the same version, the previously downloaded will be returned
            return Newest(previousCatalog, newCatalog);
        }


        // Return the newest of two catalogs, or null if both are null; return catalog1 if both are the same version
        private static Catalog Newest(Catalog catalog1, Catalog catalog2)
        {
            if ((catalog1 != null) && (catalog2 != null))
            {
                // Age is only determinend by Version, independent of StructureVersion
                return (catalog1.Version >= catalog2.Version) ? catalog1 : catalog2;
            }
            else
            {
                // Return the catalog that is not null, or null if both are
                return catalog1 ?? catalog2;
            }
        }
    }
}
