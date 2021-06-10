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
        internal static bool Create()
        {
            // Exit if the updater is not enabled in settings
            if (!ModSettings.UpdaterEnabled)
            {
                return false;
            }

            DateTime now = DateTime.Now;

            string remark = $"Added at { now.ToShortDateString() }.";

            string changeNotes = "";

            // Create a new catalog
            Catalog firstCatalog = new Catalog(1, now, "This first catalog only contains the builtin mods.");

            // Add builtin mods with the correct fixed fake Steam ID
            List<Enums.ModStatus> modStatus = new List<Enums.ModStatus> { Enums.ModStatus.SourceBundled };

            DateTime firstRelease = DateTime.Parse("2015-03-10");

            foreach (KeyValuePair<string, ulong> modVP in ModSettings.BuiltinMods)
            {
                Mod mod = firstCatalog.AddMod(modVP.Value, modVP.Key, published: firstRelease, statuses: modStatus, reviewUpdated: now, catalogRemark: remark);

                changeNotes += $"New mod: { mod.ToString() }\n";
            }

            // Add author
            Author author = firstCatalog.AddAuthor(76561198031001669, "finwickle", "Finwickle", lastSeen: now, retired: false, catalogRemark: remark);

            changeNotes += $"New author: { author.Name }\n";

            // The filename for the catalog and change notes
            string partialPath = Path.Combine(ModSettings.updaterPath, $"{ ModSettings.internalName }Catalog_v{ firstCatalog.VersionString() }");

            // Save the catalog as ModCheckerCatalog_v1.0001.xml
            bool success = firstCatalog.Save(partialPath + ".xml");

            // Save change notes, in the same folder as the new catalog
            if (success)
            {
                Tools.SaveToFile($"Change Notes for Catalog { firstCatalog.VersionString() }\n" +
                    "-------------------------------\n" +
                    $"{ now:D}, { now:t}\n\n" +
                    "*** ADDED: ***\n" +
                    changeNotes,
                    partialPath + "_ChangeNotes.txt"); ; ;
            }

            return success;
        }
    }
}
