using System;
using System.Collections.Generic;
using ModChecker.DataTypes;


namespace ModChecker.Util
{
    // For debugging and development
    class ExampleCatalog
    {
        internal static bool Create(string fullPath)
        {
            Catalog exampleCatalog = new Catalog(Convert.ToUInt32(ModSettings.build) + 1, DateTime.Now, "Example Catalog using fake data.");


            // Temporary authors to add
            ModAuthor finwickle = new ModAuthor("finwickle", "Finwickle", DateTime.Parse("2021-04-30"), retired: false);
            exampleCatalog.ModAuthors.Add(finwickle);

            ModAuthor aubergine18 = new ModAuthor("aubergine18", "aubergine18", retired: true);
            exampleCatalog.ModAuthors.Add(aubergine18);

            ModAuthor tim = new ModAuthor("76561198073436745", "Tim");
            exampleCatalog.ModAuthors.Add(tim);

            ModAuthor soda = new ModAuthor("76561197997507574", "Soda", retired: true);
            exampleCatalog.ModAuthors.Add(soda);


            // Temporary mods to add
            List<Enums.ModStatus> modStatus = new List<Enums.ModStatus> { };
            Mod mod = new Mod(1, "Hard Mode", statuses: modStatus);
            exampleCatalog.Mods.Add(mod);

            List<ulong> required = new List<ulong> { ModSettings.lowestModGroupID + 1 };
            mod = new Mod(2, "Unlimited Money", statuses: modStatus, reviewUpdated: DateTime.Now, modsRequired: required);
            exampleCatalog.Mods.Add(mod);

            List<ulong> needed = new List<ulong> { 1 };
            mod = new Mod(3, "Unlimited Oil And Ore", statuses: modStatus, onlyNeededFor: needed);
            exampleCatalog.Mods.Add(mod);

            mod = new Mod(576327847, "81 Tiles (Fixed for C:S 1.2+)", "bloody_penguin", reviewUpdated: DateTime.Now);
            exampleCatalog.Mods.Add(mod);

            modStatus = new List<Enums.ModStatus> { Enums.ModStatus.GameBreaking };
            mod = new Mod(421028969, "[ARIS] Skylines Overwatch", soda.Tag, statuses: modStatus);
            exampleCatalog.Mods.Add(mod);

            modStatus = new List<Enums.ModStatus> { Enums.ModStatus.Abandonned };
            List<ulong> modsRequired = new List<ulong> { 3, 4, 5 };
            mod = new Mod(2034713132, "Mod Compatibility Checker", aubergine18.Tag, otherAuthors: null, version: "", updated: DateTime.Parse("2020-05-16"), 
                published: DateTime.Parse("2020-03-25"), removed: false, archiveURL: "", sourceURL: "https://github.com/CitiesSkylinesMods/AutoRepair", 
                gameVersionCompatible: GameVersion.Patch_1_13_0_f8, dlcRequired: null, modsRequired: modsRequired, modsRecommended: null, onlyNeededFor: null, 
                note: "The predecessor of Mod Checker.", statuses: modStatus, reviewUpdated: DateTime.Now);
            exampleCatalog.Mods.Add(mod);

            
            List<ulong> modList = new List<ulong>() { 3, 1, 2, 5, 4 };
            ModGroup modGroup = new ModGroup(ModSettings.lowestModGroupID, modList, "Builtin Mods");
            exampleCatalog.ModGroups.Add(modGroup);

            modList = new List<ulong>() { 2034713132, 1, 421028969 };
            modGroup = new ModGroup(ModSettings.lowestModGroupID + 1, modList, "Test group");
            exampleCatalog.ModGroups.Add(modGroup);


            List<Enums.CompatibilityStatus> compStatus = new List<Enums.CompatibilityStatus>() { Enums.CompatibilityStatus.IncompatibleAccordingToAuthor };
            ModCompatibility modCompatibility = new ModCompatibility(2, ModSettings.lowestModGroupID + 1, compStatus);
            exampleCatalog.ModCompatibilities.Add(modCompatibility);


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
