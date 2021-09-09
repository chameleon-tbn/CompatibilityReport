using System;
using System.Collections.Generic;
using System.IO;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    // Create catalog version 1 from scratch with only builtin mods.
    public static class FirstCatalog
    {
        public static void Create()
        {
            DateTime updateDate = DateTime.Now;
            Catalog firstCatalog = new Catalog();

            firstCatalog.NewVersion(updateDate);
            firstCatalog.Update(Toolkit.CurrentGameVersion(), ModSettings.FirstCatalogNote, ModSettings.DefaultHeaderText, ModSettings.DefaultFooterText);

            string partialPath = Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_Catalog_v{ firstCatalog.VersionString() }");

            if (File.Exists(partialPath + ".xml"))
            {
                return;
            }

            DateTime gameRelease = DateTime.Parse("2015-03-10");
            string changeNotes = "";

            // Add fake author "Colossal Order" with high LastSeen date to avoid retirement (but not max. value to avoid out-of-range errors at retirement check).
            Author colossalOrder = firstCatalog.AddAuthor(ModSettings.FakeAuthorIDforColossalOrder, authorUrl: "", name: "Colossal Order");
            colossalOrder.Update(lastSeen: gameRelease.AddYears(1000));

            // Add builtin mods with the correct fixed fake Steam ID, and with a high ReviewDate to avoid the review being considered out-of-date.
            foreach (KeyValuePair<string, ulong> modVP in ModSettings.BuiltinMods)
            {
                Mod mod = firstCatalog.AddMod(steamID: modVP.Value);
                
                mod.Update(name: modVP.Key, published: gameRelease, authorID: colossalOrder.SteamID, stability: Enums.Stability.Stable, 
                    reviewDate: gameRelease.AddYears(1000), autoReviewDate: updateDate);

                mod.Statuses.Add(Enums.Status.SourceBundled);
                mod.AddChangeNote($"{ Toolkit.DateString(updateDate) }: added");

                changeNotes += $"New mod { mod.ToString() }\n";
            }

            if (firstCatalog.Save(partialPath + ".xml"))
            {
                Toolkit.SaveToFile($"Change Notes for Catalog { firstCatalog.VersionString() }\n" +
                    "-------------------------------\n" +
                    $"{ updateDate:D}, { updateDate:t}\n" + 
                    "These change notes were automatically created by the updater process.\n" +
                    "\n" +
                    "*** ADDED: ***\n" +
                    changeNotes, 
                    partialPath + "_ChangeNotes.txt");
            }
        }
    }
}
