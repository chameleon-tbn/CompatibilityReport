using System;
using System.Diagnostics;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;


// This dumps specific catalog data to a text file, to help with creating CSV files for the FileImporter
// It's inefficient with all the foreach loops, but 90ms don't count up to the 12 to 15 minutes from the WebCrawler, and I like to keep the code simple


namespace CompatibilityReport.Updater
{
    internal static class DataDumper
    {
        internal static void Start(Catalog catalog)
        {
            if (!ModSettings.UpdaterEnabled || catalog == null) 
            {
                return; 
            }

            Stopwatch timer = Stopwatch.StartNew();

            StringBuilder DataDump = new StringBuilder(512);

            DataDump.AppendLine($"{ ModSettings.ModName } DataDump, created on { DateTime.Now:D}, { DateTime.Now:t}.");

            DataDump.AppendLine($"Version { ModSettings.FullVersion } with catalog { catalog.VersionString() }.");

            // Unused groups, to see if we can clean up
            DumpUnusedGroups(catalog, DataDump);

            // Groups with less than 2 members
            DumpEmptyGroups(catalog, DataDump);

            // Required mods that are not in a group, to check for the need of additional groups
            DumpRequiredUngroupedMods(catalog, DataDump);

            // Authors that retire soon, to check them for activity in comments
            DumpAuthorsSoonRetired(catalog, DataDump, months: 2);

            // Authors with multiple mods, for a check of different version of the same mods (test vs stable)
            DumpAuthorsWithMultipleMods(catalog, DataDump);

            // Mods with an old review, to know which to review again
            DumpModsWithOldReview(catalog, DataDump, months: 2);

            // Mods without a review, to know which to review yet
            DumpModsWithoutReview(catalog, DataDump);

            // Retired authors, for a one time check at the start of this mod for activity in comments
            DumpRetiredAuthors(catalog, DataDump);

            Toolkit.SaveToFile(DataDump.ToString(), ModSettings.DataDumpFullPath, createBackup: true);

            timer.Stop();

            Logger.UpdaterLog($"Datadump created in { Toolkit.TimeString(timer.ElapsedMilliseconds) }, as { Toolkit.GetFileName(ModSettings.DataDumpFullPath) }.");
        }


        // Dump name and workshop url for all non-incompatible mods that have not been reviewed yet
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


        // Dump last review date, name and workshop url for all non-incompatible mods that have not been reviewed in the last x months
        private static void DumpModsWithOldReview(Catalog catalog, StringBuilder DataDump, int months)
        {
            DataDump.AppendLine(Title($"Mods with a old review (> { months } months old):"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalogMod.ReviewDate != default && catalogMod.ReviewDate.AddMonths(months) < DateTime.Now && 
                    catalogMod.Stability != Enums.Stability.IncompatibleAccordingToWorkshop)
                {
                    DataDump.AppendLine($"last review { Toolkit.DateString(catalogMod.ReviewDate) }: { catalogMod.Name }, " +
                        Toolkit.GetWorkshopUrl(catalogMod.SteamID));
                }
            }
        }


        // Dump name, statuses and workshop url for all required mods that are not in a group
        private static void DumpRequiredUngroupedMods(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("All required mods that are not in a group:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalog.IsGroupMember(catalogMod.SteamID))
                {
                    continue;
                }

                // Find a mod that require this mod
                if (catalog.Mods.Find(x => x.RequiredMods.Contains(catalogMod.SteamID)) != default) 
                {
                    // Get statuses
                    string statuses = "";

                    foreach (Enums.Status status in catalogMod.Statuses)
                    {
                        statuses += ", " + status.ToString();
                    }

                    if (!string.IsNullOrEmpty(statuses))
                    {
                        statuses = " [" + statuses.Substring(2) + "]";
                    }

                    DataDump.AppendLine($"{ catalogMod.Name }{ statuses }, { Toolkit.GetWorkshopUrl(catalogMod.SteamID) }");
                }
            }
        }


        // Dump id and name for all groups that are not used for required or recommended mods.
        private static void DumpUnusedGroups(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Unused groups:"));

            foreach (Group catalogGroup in catalog.Groups)
            {
                // List groups that are not used as a required or recommended mod
                if (catalog.Mods.Find(x => x.RequiredMods.Contains(catalogGroup.GroupID)) == default && 
                    catalog.Mods.Find(x => x.Recommendations.Contains(catalogGroup.GroupID)) == default)
                {
                    DataDump.AppendLine(catalogGroup.ToString());
                }
            }
        }


        // Dump group ID and name, and remaining groupmember, for all groups with less than two members
        private static void DumpEmptyGroups(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Groups with less than 2 members:"));

            foreach (Group catalogGroup in catalog.Groups)
            {
                if (catalogGroup.GroupMembers.Count == 0)
                {
                    DataDump.AppendLine(catalogGroup.ToString() + ": no members");
                }
                else if (catalogGroup.GroupMembers.Count == 1)
                {
                    DataDump.AppendLine(catalogGroup.ToString() + ": only member is " +
                        catalog.ModIndex[catalogGroup.GroupMembers[0]].ToString());
                }
            }
        }


        // Dump name and workshop url for all authors with more than one mod; gives false positives for mods that contain both author ID and URL
        private static void DumpAuthorsWithMultipleMods(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Authors with more than one mod:"));

            foreach (Author catalogAuthor in catalog.Authors)
            {
                // List authors that have at least two mods
                if ((catalogAuthor.SteamID != 0 ? catalog.Mods.FindAll(x => x.AuthorID == catalogAuthor.SteamID).Count : 0) +
                    (!string.IsNullOrEmpty(catalogAuthor.CustomUrl) ? catalog.Mods.FindAll(x => x.AuthorUrl == catalogAuthor.CustomUrl).Count : 0) > 1)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name }{ (catalogAuthor.Retired ? " [retired]" : "") }, " +
                        $"{ Toolkit.GetAuthorWorkshopUrl(catalogAuthor.SteamID, catalogAuthor.CustomUrl) }");
                }
            }
        }


        // Dump name and workshop url for all authors with the retired status
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


        // Dump name and workshop url for all authors that will get the retired status within x months
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


        private static string Title(string title)
        {
            string separator = new string('=', title.Length);

            return "\n\n" + separator + "\n" + title + "\n" + separator + "\n";
        }
    }
}
