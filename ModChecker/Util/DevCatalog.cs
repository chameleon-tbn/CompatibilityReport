using System;
using System.Collections.Generic;
using ModChecker.DataTypes;


namespace ModChecker.Util
{
    // [Todo 0.4] Can be removed later
    // For development only
    internal static class DevCatalog
    {
        internal static bool Create(string fullPath)
        {
            Catalog devCatalog = new Catalog(1, DateTime.Now, "This first catalog only contains builtin mods.");

            DateTime now = DateTime.Now;

            // Builtin mods
            List<Enums.ModStatus> modStatus = new List<Enums.ModStatus> { Enums.ModStatus.SourceBundled };
            DateTime firstRelease = DateTime.Parse("2015-03-10");

            devCatalog.AddMod(1, "Hard Mode", published: firstRelease, statuses: modStatus, reviewUpdated: now);
            devCatalog.AddMod(2, "Unlimited Money", published: firstRelease, statuses: modStatus, reviewUpdated: now);
            devCatalog.AddMod(3, "Unlimited Oil And Ore", published: firstRelease, statuses: modStatus, reviewUpdated: now);
            devCatalog.AddMod(4, "Unlimited Soil", published: firstRelease, statuses: modStatus, reviewUpdated: now);
            devCatalog.AddMod(5, "Unlock All", published: firstRelease, statuses: modStatus, reviewUpdated: now);

            // Author
            devCatalog.AddAuthor("finwickle", idIsProfile: false, "Finwickle", now);

            // Save the catalog
            return devCatalog.Save(fullPath);
        }
    }
}
