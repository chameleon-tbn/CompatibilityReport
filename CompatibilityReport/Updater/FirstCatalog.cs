using System;
using System.Collections.Generic;
using System.IO;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    public static class FirstCatalog
    {
        /// <summary>Creates catalog version 1 from scratch with only built-in mods.</summary>
        /// <remarks>It uses a mixture of CatalogUpdater methods and direct object updates to get relevant change notes.</remarks>
        public static void Create()
        {
            Toolkit.DeleteFile(Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName));

            Catalog firstCatalog = new Catalog();
            firstCatalog.NewVersion(DateTime.Now);
            firstCatalog.Update(Toolkit.CurrentGameVersion(), ModSettings.FirstCatalogNote, ModSettings.DefaultHeaderText, ModSettings.DefaultFooterText);

            if (File.Exists(Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog_v{ firstCatalog.VersionString() }.xml")))
            {
                return;
            }

            // Use a high review date for built-in mods, to avoid the review being considered out-of-date at some point.
            DateTime gameRelease = Toolkit.ConvertDate($"2015-03-10");
            CatalogUpdater.SetReviewDate(gameRelease.AddYears(1000));

            // Add fake author "Colossal Order" with high LastSeen date to avoid retirement, but not max. value to avoid out-of-range errors at retirement check.
            Author colossalOrder = CatalogUpdater.AddAuthor(firstCatalog, ModSettings.FakeAuthorIDforColossalOrder, authorUrl: "", name: "Colossal Order");
            colossalOrder.Update(lastSeen: gameRelease.AddYears(1000));

            foreach (string modName in ModSettings.BuiltinMods.Keys)
            {
                Mod builtinMod = CatalogUpdater.AddMod(firstCatalog, steamID: ModSettings.BuiltinMods[modName], modName);
                builtinMod.Update(stability: Enums.Stability.Stable);
                builtinMod.Statuses.Add(Enums.Status.SourceBundled);

                // 'UpdatedByWebcrawler' assures an auto review date filled and compact change notes
                CatalogUpdater.UpdateMod(firstCatalog, builtinMod, published: gameRelease, authorID: colossalOrder.SteamID, updatedByWebCrawler: true);

                // Add a review date to avoid the review being considered out-of-date at some point
                CatalogUpdater.UpdateMod(firstCatalog, builtinMod, updatedByWebCrawler: false);
            }

            // Import a SuppressedWarnings.csv file if it exists.
            string suppressedWarningFullPath = Path.Combine(ModSettings.UpdaterPath, "SuppressedWarnings.csv");
            if (File.Exists(suppressedWarningFullPath))
            {
                FileImporter.ReadCsv(firstCatalog, suppressedWarningFullPath);
            }

            CatalogUpdater.SaveCatalog(firstCatalog);
        }
    }
}
