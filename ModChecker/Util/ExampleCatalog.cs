using System;
using System.Collections.Generic;
using System.Linq;
using ModChecker.DataTypes;
using static ModChecker.DataTypes.Enums.CompatibilityStatus;
using static ModChecker.DataTypes.Enums.ModStatus;


namespace ModChecker.Util
{
    // For debugging and development
    class ExampleCatalog
    {
        internal static bool Create(string fullPath)
        {
            Catalog exampleCatalog = new Catalog(Convert.ToUInt32(ModSettings.build) + 1, DateTime.Now, 
                "This Catalog uses fake data and is for testing only. DON'T TRUST THIS INFO!");

            DateTime now = DateTime.Now;
            List<Enums.ModStatus> modStatus;
            List<ulong> modList;
            List<Enums.CompatibilityStatus> compStatus;


            // Builtin mods
            modStatus = new List<Enums.ModStatus> { SourceBundled };
            
            exampleCatalog.Mods.Add(new Mod(1, "Hard Mode", statuses: modStatus, reviewUpdated: now));
            exampleCatalog.Mods.Add(new Mod(2, "Unlimited Money", statuses: modStatus, reviewUpdated: now));
            exampleCatalog.Mods.Add(new Mod(3, "Unlimited Oil And Ore", statuses: modStatus, reviewUpdated: now));
            exampleCatalog.Mods.Add(new Mod(4, "Unlimited Soil", statuses: modStatus, reviewUpdated: now));
            exampleCatalog.Mods.Add(new Mod(5, "Unlock All", statuses: modStatus, reviewUpdated: now));

            // Some authors
            exampleCatalog.ModAuthors.Add(new ModAuthor("finwickle", "Finwickle", DateTime.Parse("2021-04-30")));
            exampleCatalog.ModAuthors.Add(new ModAuthor("76561198073436745", "Tim"));

            ModAuthor aubergine18 = new ModAuthor("aubergine18", "aubergine18", retired: true);
            exampleCatalog.ModAuthors.Add(aubergine18);
            
            ModAuthor soda = new ModAuthor("76561197997507574", "Soda", retired: true);
            exampleCatalog.ModAuthors.Add(soda);


            // Fake mod info
            exampleCatalog.Mods.Add(new Mod(576327847, "81 Tiles (Fixed for C:S 1.2+)", "bloody_penguin", reviewUpdated: DateTime.Now));

            modStatus = new List<Enums.ModStatus> { GameBreaking };
            exampleCatalog.Mods.Add(new Mod(421028969, "[ARIS] Skylines Overwatch", soda.Tag, statuses: modStatus));

            modStatus = new List<Enums.ModStatus> { Abandoned };
            List<ulong> modsRequired = new List<ulong> { 3, 4, 5 };
            exampleCatalog.Mods.Add(
                new Mod(2034713132, "Mod Compatibility Checker", aubergine18.Tag, otherAuthors: null, version: "", published: DateTime.Parse("2020-03-25"),
                updated: DateTime.Parse("2020-05-16"), removed: false, archiveURL: "", "https://github.com/CitiesSkylinesMods/AutoRepair",
                GameVersion.Patch_1_13_0_f8.ToString(), dlcRequired: null, modsRequired, modsRecommended: null, onlyNeededFor: null,
                note: "The predecessor of Mod Checker.", modStatus, reviewUpdated: DateTime.Now));
            

            // Useless mod groups
            modList = new List<ulong>() { 3, 1, 2, 5, 4 };
            exampleCatalog.AddGroup(modList, "Builtin Mods");

            modList = new List<ulong>() { 2034713132, 1, 421028969 };
            ulong id = exampleCatalog.AddGroup(modList, "Test group");


            // Fake compatibility
            compStatus = new List<Enums.CompatibilityStatus>() { IncompatibleAccordingToAuthor };
            exampleCatalog.ModCompatibilities.Add(new ModCompatibility(2, id, compStatus));


            // Validate catalog before writing to file
            if (!exampleCatalog.Validate())
            {
                Logger.Log("Example database did not validate.", Logger.error);

                return false;
            }

            return exampleCatalog.Save(fullPath);
        }
    }
}
