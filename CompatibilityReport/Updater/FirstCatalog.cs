using System;
using System.Collections.Generic;
using System.IO;
using CompatibilityReport.DataTypes;
using CompatibilityReport.Util;


namespace CompatibilityReport.Updater
{
    // Only needed to (re)create the catalog from scratch; catalog 1 is only builtin mods
    internal static class FirstCatalog
    {
        internal static Catalog Create()
        {
            DateTime updateDate = DateTime.Now;

            // Create a new catalog
            Catalog firstCatalog = new Catalog(1, updateDate, ModSettings.firstCatalogNote);

            // The filename for the catalog and change notes
            string partialPath = Path.Combine(ModSettings.updaterPath, $"{ ModSettings.internalName }Catalog_v{ firstCatalog.VersionString() }");

            // Exit if the first catalog already exists
            if (File.Exists(partialPath + ".xml"))
            {
                return null;
            }

            // Add builtin mods with the correct fixed fake Steam ID
            List<Enums.ModStatus> sourceBundled = new List<Enums.ModStatus> { Enums.ModStatus.SourceBundled };

            DateTime gameRelease = DateTime.Parse("2015-03-10");

            string modNotes = $"{ Toolkit.DateString(updateDate) }: added";

            string changeNotes = "";

            foreach (KeyValuePair<string, ulong> modVP in ModSettings.BuiltinMods)
            {
                Mod mod = firstCatalog.AddOrUpdateMod(steamID: modVP.Value, name: modVP.Key, published: gameRelease, stability: Enums.ModStability.Stable,
                    statuses: sourceBundled,reviewUpdated: updateDate, autoReviewUpdated: updateDate, extraChangeNote: modNotes);

                changeNotes += $"New mod { mod.ToString() }\n";
            }

            // Save the catalog as 'CompatibilityReportCatalog_v1.0001.xml' and save the change notes in the same folder
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

            return firstCatalog;
        }
    }
}
