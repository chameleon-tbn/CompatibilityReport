using System;
using System.Diagnostics;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

// This dumps specific catalog data to a text file, to help with creating CSV files for the FileImporter.
// It's inefficient with all the foreach loops, but 90ms don't count up to the 12 to 15 minutes from the WebCrawler, and I like to keep the code simple.

namespace CompatibilityReport.Updater
{
    public static class DataDumper
    {
        public static void Start(Catalog catalog)
        {
            Stopwatch timer = Stopwatch.StartNew();
            StringBuilder DataDump = new StringBuilder(512);

            DataDump.AppendLine($"{ ModSettings.ModName } DataDump, created on { DateTime.Now:D}, { DateTime.Now:t}.");
            DataDump.AppendLine($"Version { ModSettings.FullVersion } with catalog { catalog.VersionString() }.");

            // Unused groups and groups with less than 2 members, to see if we can clean up.
            DumpUnusedGroups(catalog, DataDump);
            DumpEmptyGroups(catalog, DataDump);

            // Required mods that are not in a group, to check for the need of additional groups.
            DumpRequiredUngroupedMods(catalog, DataDump);

            // Authors that retire soon, to check them for activity in comments.
            DumpAuthorsSoonRetired(catalog, DataDump, months: 2);

            // Authors with multiple mods, for a check of different version of the same mods (test vs stable).
            DumpAuthorsWithMultipleMods(catalog, DataDump);

            // Mods with an old review or without a review, to know which to review (again).
            DumpModsWithOldReview(catalog, DataDump, months: 2);
            DumpModsWithoutReview(catalog, DataDump);

            // Retired authors, for a one time check at the start of this mod for activity in comments.
            DumpRetiredAuthors(catalog, DataDump);

            Toolkit.SaveToFile(DataDump.ToString(), ModSettings.DataDumpFullPath, createBackup: true);

            timer.Stop();
            Logger.UpdaterLog($"Datadump created in { Toolkit.TimeString(timer.ElapsedMilliseconds) }, as { Toolkit.GetFileName(ModSettings.DataDumpFullPath) }.");
        }


        // Dump info about all non-incompatible mods that have not been reviewed yet: name and Workshop URL.
        private static void DumpModsWithoutReview(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Mods without a review:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalogMod.ReviewDate == default && catalogMod.Stability != Enums.Stability.IncompatibleAccordingToWorkshop)
                {
                    DataDump.AppendLine($"{ catalogMod.Name }, { Toolkit.GetWorkshopUrl(catalogMod.SteamID) }");
                }
            }
        }


        // Dump info about all non-incompatible mods that have not been reviewed in the last x months: last review date, name and Workshop URL.
        private static void DumpModsWithOldReview(Catalog catalog, StringBuilder DataDump, int months)
        {
            DataDump.AppendLine(Title($"Mods with an old review (> { months } months old):"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalogMod.ReviewDate != default && catalogMod.ReviewDate.AddMonths(months) < DateTime.Now && 
                    catalogMod.Stability != Enums.Stability.IncompatibleAccordingToWorkshop)
                {
                    DataDump.AppendLine($"last review { Toolkit.DateString(catalogMod.ReviewDate) }: { catalogMod.Name }, { Toolkit.GetWorkshopUrl(catalogMod.SteamID) }");
                }
            }
        }


        // Dump info about all required mods that are not in a group: name, stability, statuses and Workshop URL.
        private static void DumpRequiredUngroupedMods(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Required mods that are not in a group:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalog.IsGroupMember(catalogMod.SteamID) || catalog.Mods.Find(x => x.RequiredMods.Contains(catalogMod.SteamID)) != default) 
                {
                    string statuses = "";

                    foreach (Enums.Status status in catalogMod.Statuses)
                    {
                        statuses += $", { status }";
                    }

                    DataDump.AppendLine($"{ catalogMod.Name } [{ catalogMod.Stability }{ statuses }], { Toolkit.GetWorkshopUrl(catalogMod.SteamID) }");
                }
            }
        }


        // Dump info about all groups that are not used for required or recommended mods: ID and name.
        private static void DumpUnusedGroups(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Unused groups:"));

            foreach (Group catalogGroup in catalog.Groups)
            {
                if (catalog.Mods.Find(x => x.RequiredMods.Contains(catalogGroup.GroupID)) == default && 
                    catalog.Mods.Find(x => x.Recommendations.Contains(catalogGroup.GroupID)) == default)
                {
                    DataDump.AppendLine(catalogGroup.ToString());
                }
            }
        }


        // Dump info about all groups with less than two members: ID, name and remaining groupmember.
        private static void DumpEmptyGroups(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Groups with less than 2 members:"));

            foreach (Group catalogGroup in catalog.Groups)
            {
                if (catalogGroup.GroupMembers.Count == 0)
                {
                    DataDump.AppendLine($"{ catalogGroup.ToString() }: no members");
                }
                else if (catalogGroup.GroupMembers.Count == 1)
                {
                    DataDump.AppendLine($"{ catalogGroup.ToString() }: only member is { catalog.ModIndex[catalogGroup.GroupMembers[0]].ToString() }");
                }
            }
        }


        // Dump info about all authors with more than one mod: name, retired state and Workshop URL.
        // Todo 0.4 This will give false positives if newly found author IDs and URLs are not propagated to all mods.
        private static void DumpAuthorsWithMultipleMods(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Authors with more than one mod:"));

            foreach (Author catalogAuthor in catalog.Authors)
            {
                int modCount = (catalogAuthor.SteamID != 0) ? catalog.Mods.FindAll(x => x.AuthorID == catalogAuthor.SteamID).Count : 
                    catalog.Mods.FindAll(x => x.AuthorUrl == catalogAuthor.CustomUrl).Count;

                if (modCount > 1)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name }{ (catalogAuthor.Retired ? " [retired]" : "") }, " +
                        $"{ Toolkit.GetAuthorWorkshopUrl(catalogAuthor.SteamID, catalogAuthor.CustomUrl) }");
                }
            }
        }


        // Dump info about all authors with the retired status: name and Workshop URL.
        private static void DumpRetiredAuthors(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Retired authors:"));

            foreach (Author catalogAuthor in catalog.Authors)
            {
                if (catalogAuthor.Retired)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name }, { Toolkit.GetAuthorWorkshopUrl(catalogAuthor.SteamID, catalogAuthor.CustomUrl) }");
                }
            }
        }


        // Dump info about all authors that will get the retired status within x months: name and Workshop URL.
        private static void DumpAuthorsSoonRetired(Catalog catalog, StringBuilder DataDump, int months)
        {
            DataDump.AppendLine(Title($"Authors that will retire within { months } months:"));

            foreach (Author catalogAuthor in catalog.Authors)
            {
                if (!catalogAuthor.Retired && catalogAuthor.LastSeen.AddMonths(ModSettings.MonthsOfInactivityToRetireAuthor - months) < DateTime.Now)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name }, { Toolkit.GetAuthorWorkshopUrl(catalogAuthor.SteamID, catalogAuthor.CustomUrl) }");
                }
            }
        }


        // Return the title with blank lines and separators above and below.
        private static string Title(string title)
        {
            string separator = new string('=', title.Length);

            return $"\n\n{ separator }\n{ title }\n{ separator }\n";
        }
    }
}
