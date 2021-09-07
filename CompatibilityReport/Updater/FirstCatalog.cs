using System;
using System.Collections.Generic;
using System.IO;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;


namespace CompatibilityReport.Updater
{
    // Only needed to (re)create the catalog from scratch; catalog 1 is only builtin mods
    internal static class FirstCatalog
    {
        internal static void Create()
        {
            DateTime updateDate = DateTime.Now;

            // Create a new catalog
            Catalog firstCatalog = new Catalog();

            firstCatalog.NewVersion(updateDate);

            firstCatalog.Update(Toolkit.CurrentGameVersion, ModSettings.firstCatalogNote, ModSettings.defaultHeaderText, ModSettings.defaultFooterText);

            // The filename for the catalog and change notes
            string partialPath = Path.Combine(ModSettings.updaterPath, $"{ ModSettings.internalName }_Catalog_v{ firstCatalog.VersionString() }");

            // Exit if the first catalog already exists
            if (File.Exists(partialPath + ".xml"))
            {
                return;
            }

            DateTime gameRelease = DateTime.Parse("2015-03-10");

            string changeNotes = "";

            // Add author "Colossal Order" with fake ID and high LastSeen date to avoid retirement (but lower than max to avoid out-of-range errors at retirement check)
            Author colossalOrder = firstCatalog.AddAuthor(ModSettings.fakeAuthorIDforColossalOrder, authorUrl: "", name: "Colossal Order");
            
            colossalOrder.Update(lastSeen: gameRelease.AddYears(1000));

            // Add builtin mods with the correct fixed fake Steam ID, and with a high ReviewDate to avoid the mods review being considered out-of-date
            foreach (KeyValuePair<string, ulong> modVP in ModSettings.BuiltinMods)
            {
                Mod mod = firstCatalog.AddMod(steamID: modVP.Value);
                
                mod.Update(name: modVP.Key, published: gameRelease, authorID: colossalOrder.SteamID, stability: Enums.Stability.Stable, 
                    reviewDate: gameRelease.AddYears(1000), autoReviewDate: updateDate);

                mod.Statuses.Add(Enums.Status.SourceBundled);
                mod.AddChangeNote($"{ Toolkit.DateString(updateDate) }: added");

                changeNotes += $"New mod { mod.ToString() }\n";
            }

            // Save the catalog as 'CompatibilityReport_Catalog_v1.0001.xml' and save the change notes in the same folder
            if (firstCatalog.Save(partialPath + ".xml"))
            {
                Toolkit.SaveToFile($"Change Notes for Catalog { firstCatalog.VersionString() }\n" +
                    "-------------------------------\n" +
                    $"{ updateDate:D}, { updateDate:t}\n" + 
                    "These change notes were automatically created by the updater process.\n" +
                    "\n" +
                    "\n" +
                    "*** ADDED: ***\n" +
                    changeNotes, 
                    partialPath + "_ChangeNotes.txt");
            }
        }
    }
}
