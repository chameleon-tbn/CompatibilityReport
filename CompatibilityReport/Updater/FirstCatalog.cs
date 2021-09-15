using System;
using System.Collections.Generic;
using System.IO;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    public static class FirstCatalog
    {
        // Create catalog version 1 from scratch with only builtin mods. It uses a mixture of CatalogUpdater methods and direct object updates to get relevant change notes.
        public static void Create()
        {
            Catalog firstCatalog = new Catalog();
            firstCatalog.NewVersion(DateTime.Now);
            firstCatalog.Update(Toolkit.CurrentGameVersion(), ModSettings.FirstCatalogNote, ModSettings.DefaultHeaderText, ModSettings.DefaultFooterText);

            if (File.Exists(Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog_v{ firstCatalog.VersionString() }.xml")))
            {
                return;
            }

            // Use a high review date for builtin mods, to avoid the review being considered out-of-date at some point.
            DateTime gameRelease = DateTime.Parse("2015-03-10");
            CatalogUpdater.SetReviewDate(gameRelease.AddYears(1000));

            // Add fake author "Colossal Order" with high LastSeen date to avoid retirement, but not max. value to avoid out-of-range errors at retirement check.
            Author colossalOrder = CatalogUpdater.AddAuthor(firstCatalog, ModSettings.FakeAuthorIDforColossalOrder, authorUrl: "", authorName: "Colossal Order");
            colossalOrder.Update(lastSeen: gameRelease.AddYears(1000));

            foreach (string modName in ModSettings.BuiltinMods.Keys)
            {
                Mod builtinMod = CatalogUpdater.AddMod(firstCatalog, steamID: ModSettings.BuiltinMods[modName], modName);
                builtinMod.Update(stability: Enums.Stability.Stable);
                builtinMod.Statuses.Add(Enums.Status.SourceBundled);
                CatalogUpdater.UpdateMod(firstCatalog, builtinMod, published: gameRelease, authorID: colossalOrder.SteamID, alwaysUpdateReviewDate: true);
            }

            CatalogUpdater.SaveCatalog(firstCatalog);
        }
    }
}
