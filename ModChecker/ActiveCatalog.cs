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
            // Skip if we already have an active catalog
            if (Instance != null)
            {
                return true;
            }

            // Load the catalog that was included with the mod
            Catalog bundledCatalog = LoadBundled();

            // Load the previously downloaded catalog
            Catalog previousCatalog = LoadPreviouslyDownloaded();

            // Download a new catalog
            Catalog downloadedCatalog = Download(previousCatalog == null ? 0 : previousCatalog.Version);

            // Download the latest updated catalog
            Catalog updatedCatalog = LoadUpdater();

            // The newest catalog becomes the active catalog
            Instance = Newest(bundledCatalog, previousCatalog, downloadedCatalog, updatedCatalog);

            if (Instance != null)
            {
                // Prepare the active catalog for searching
                Instance.CreateIndex();
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
                Logger.Log($"Bundled catalog is version { catalog.VersionString() }.");
            }

            return catalog;
        }


        // Load the previously downloaded catalog if it exists
        private static Catalog LoadPreviouslyDownloaded()
        {
            // Check if previously downloaded catalog exists
            if (!File.Exists(ModSettings.downloadedCatalogFullPath))
            {
                Logger.Log("No previously downloaded catalog exists. This is expected when the mod has never downloaded a new catalog.");

                return null;
            }

            // Try to load it
            Catalog previousCatalog = Catalog.Load(ModSettings.downloadedCatalogFullPath);

            if (previousCatalog != null)
            {
                Logger.Log($"Previously downloaded catalog is version { previousCatalog.VersionString() }.");
            }
            // Can't be loaded; try to delete it
            else if (Toolkit.DeleteFile(ModSettings.downloadedCatalogFullPath))
            {
                Logger.Log("Coud not load previously downloaded catalog. It has been deleted.", Logger.warning);
            }
            else
            {
                Logger.Log("Can't load previously downloaded catalog and it can't be deleted either. " +
                    "This prevents saving a newly downloaded catalog for future sessions.", Logger.error);
            }

            return previousCatalog;
        }


        // Download a new catalog
        private static Catalog Download(uint previousVersion)
        {
            // Exit if we already downloaded this session
            if (downloadedValidCatalog)
            {
                return null;
            }

            // Temporary filename for the newly downloaded catalog
            string newCatalogTemporaryFullPath = ModSettings.downloadedCatalogFullPath + ".part";

            // Delete temporary catalog if it was left over from a previous session; exit if we can't delete it
            if (!Toolkit.DeleteFile(newCatalogTemporaryFullPath))
            {
                Logger.Log("Partially downloaded catalog still exists from a previous session and can't be deleted. This prevents a new download.", Logger.error);

                return null;
            }

            // Download new catalog and time it
            Stopwatch timer = Stopwatch.StartNew();

            Exception exception = Toolkit.Download(ModSettings.catalogURL, newCatalogTemporaryFullPath);

            if (exception != null)
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

                // Delete empty temporary file and exit
                Toolkit.DeleteFile(newCatalogTemporaryFullPath);

                return null;
            }

            // Log elapsed time
            timer.Stop();

            Logger.Log($"Catalog downloaded in { Toolkit.ElapsedTime(timer.ElapsedMilliseconds, showDecimal: true) } from { ModSettings.catalogURL }");

            // Load newly downloaded catalog
            Catalog newCatalog = Catalog.Load(newCatalogTemporaryFullPath);

            if (newCatalog == null)
            {
                Logger.Log("Could not load newly downloaded catalog.", Logger.error);
            }
            else
            {
                Logger.Log($"Downloaded catalog is version { newCatalog.VersionString() }.");

                // Indicate we downloaded a valid catalog, so we won't do that again this session
                downloadedValidCatalog = true;

                // Copy the temporary file over the previously downloaded catalog if it's newer
                if (newCatalog.Version > previousVersion)
                {
                    downloadedValidCatalog = Toolkit.CopyFile(newCatalogTemporaryFullPath, ModSettings.downloadedCatalogFullPath);
                }
            }

            // Delete temporary file
            Toolkit.DeleteFile(newCatalogTemporaryFullPath);

            return newCatalog;
        }


        // Load updated catalog, if the updater is enabled
        private static Catalog LoadUpdater()
        {
            if (!ModSettings.UpdaterEnabled)
            {
                return null;
            }

            // Get all catalog filenames
            string[] files = Directory.GetFiles(ModSettings.updaterPath, $"{ ModSettings.internalName }Catalog*.xml");

            if (files.Length == 0)
            {
                return null;
            }

            // Sort the filenames
            Array.Sort(files);

            // Load the last updated catalog
            Catalog catalog = Catalog.Load(files[files.Length - 1]);

            if (catalog == null)
            {
                Logger.Log($"Can't load updater catalog.", Logger.warning);
            }
            else
            {
                Logger.Log($"Updater catalog is version { catalog.VersionString() }.");
            }

            return catalog;
        }


        // Return the newest of four catalogs
        private static Catalog Newest(Catalog catalog1, Catalog catalog2, Catalog catalog3, Catalog catalog4)
        {
            return (NewestOfTwo(NewestOfTwo(catalog1, catalog2), NewestOfTwo(catalog3, catalog4)));
        }


        // Return the newest of two catalogs; return catalog1 if both are the same version
        private static Catalog NewestOfTwo(Catalog catalog1, Catalog catalog2)
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
