using System;
using System.Collections.Generic;
using System.IO;
using ModChecker.DataTypes;
using ModChecker.Util;


namespace ModChecker.Updater
{
    // Only needed to (re)create the catalog from scratch
    internal static class FirstCatalog
    {
        internal static void Create()
        {
            // Exit if the updater is not enabled in settings
            if (!ModSettings.UpdaterEnabled)
            {
                return;
            }

            DateTime now = DateTime.Now;

            // Create a new catalog
            Catalog firstCatalog = new Catalog(1, now, "This first catalog only contains the builtin mods.");

            // The filename for the catalog and change notes
            string partialPath = Path.Combine(ModSettings.updaterPath, $"{ ModSettings.internalName }Catalog_v{ firstCatalog.VersionString() }");

            // Exit if the first catalog already exists
            if (File.Exists(partialPath + ".xml"))
            {
                return;
            }

            // Add builtin mods with the correct fixed fake Steam ID
            List<Enums.ModStatus> sourceBundled = new List<Enums.ModStatus> { Enums.ModStatus.SourceBundled };

            DateTime firstGameRelease = DateTime.Parse("2015-03-10");

            string remark = $"Added at { now.ToShortDateString() }.";

            string changeNotes = "";

            foreach (KeyValuePair<string, ulong> modVP in ModSettings.BuiltinMods)
            {
                Mod mod = firstCatalog.AddMod(modVP.Value, modVP.Key, published: firstGameRelease, statuses: sourceBundled, reviewUpdated: now, catalogRemark: remark);

                changeNotes += $"New mod: { mod.ToString() }\n";
            }

            // Add author
            Author author = firstCatalog.AddAuthor(76561198031001669, "finwickle", "Finwickle", lastSeen: now, retired: false, catalogRemark: remark);

            changeNotes += $"New author: { author.Name }\n";

            // Save the catalog as ModCheckerCatalog_v1.0001.xml and save the change notes in the same folder
            if (firstCatalog.Save(partialPath + ".xml"))
            {
                Tools.SaveToFile($"Change Notes for Catalog { firstCatalog.VersionString() }\n" +
                    "-------------------------------\n" +
                    $"{ now:D}, { now:t}\n\n" +
                    "*** ADDED: ***\n" +
                    changeNotes,
                    partialPath + "_ChangeNotes.txt"); ; ;
            }
        }
    }
}
