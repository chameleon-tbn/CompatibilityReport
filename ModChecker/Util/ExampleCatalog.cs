using System;
using System.Collections.Generic;
using System.Linq;
using ModChecker.DataTypes;
using static ModChecker.DataTypes.Enums;
using static ModChecker.DataTypes.Enums.CompatibilityStatus;
using static ModChecker.DataTypes.Enums.ModStatus;


namespace ModChecker.Util
{
    // For debugging and development
    internal static class ExampleCatalog
    {
        internal static bool Create(string fullPath)
        {
            Catalog exampleCatalog = new Catalog(1, DateTime.Now, "This first catalog only contains builtin mods.");

            DateTime now = DateTime.Now;
            List<ModStatus> modStatus;

            // Builtin mods
            modStatus = new List<ModStatus> { SourceBundled };
            
            exampleCatalog.Mods.Add(new Mod(1, "Hard Mode", statuses: modStatus, reviewUpdated: now));
            exampleCatalog.Mods.Add(new Mod(2, "Unlimited Money", statuses: modStatus, reviewUpdated: now));
            exampleCatalog.Mods.Add(new Mod(3, "Unlimited Oil And Ore", statuses: modStatus, reviewUpdated: now));
            exampleCatalog.Mods.Add(new Mod(4, "Unlimited Soil", statuses: modStatus, reviewUpdated: now));
            exampleCatalog.Mods.Add(new Mod(5, "Unlock All", statuses: modStatus, reviewUpdated: now));

            // Author
            exampleCatalog.ModAuthors.Add(new ModAuthor("finwickle", idIsProfile: false, "Finwickle", DateTime.Now));

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
