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
        static StringBuilder DataDump;

        internal static void Start(Catalog ActiveCatalog)
        {
            if (!ModSettings.UpdaterEnabled || ActiveCatalog == null) 
            {
                return; 
            }

            Stopwatch timer = Stopwatch.StartNew();

            DataDump = new StringBuilder(512);

            DataDump.AppendLine($"{ ModSettings.modName } DataDump, created on { DateTime.Now:D}, { DateTime.Now:t}.");

            DataDump.AppendLine($"Version { ModSettings.fullVersion } with catalog { ActiveCatalog.VersionString() }.");

            // Unused groups, to see if we can clean up
            DumpUnusedGroups(ActiveCatalog);

            // Groups with less than 2 members
            DumpEmptyGroups(ActiveCatalog);

            // Required mods that are not in a group, to check for the need of additional groups
            DumpRequiredUngroupedMods(ActiveCatalog);

            // Authors that retire soon, to check them for activity in comments
            DumpAuthorsSoonRetired(ActiveCatalog, months: 2);

            // Authors with multiple mods, for a check of different version of the same mods (test vs stable)
            DumpAuthorsWithMultipleMods(ActiveCatalog);

            // Mods with an old review, to know which to review again
            DumpModsWithOldReview(ActiveCatalog, months: 2);

            // Mods without a review, to know which to review yet
            DumpModsWithoutReview(ActiveCatalog);

            // Retired authors, for a one time check at the start of this mod for activity in comments
            DumpRetiredAuthors(ActiveCatalog);

            Toolkit.SaveToFile(DataDump.ToString(), ModSettings.dataDumpFullPath, createBackup: true);

            DataDump = new StringBuilder();

            timer.Stop();

            Logger.UpdaterLog($"Datadump created in { Toolkit.ElapsedTime(timer.ElapsedMilliseconds, alwaysShowSeconds: true) }, as " +
                $"{ Toolkit.GetFileName(ModSettings.dataDumpFullPath) }.");
        }


        // Dump name and workshop url for all non-incompatible mods that have not been reviewed yet
        private static void DumpModsWithoutReview(Catalog ActiveCatalog)
        {
            DumpTitle("Mods without a review:");

            foreach (Mod catalogMod in ActiveCatalog.Mods)
            {
                if (catalogMod.ReviewDate == default && catalogMod.Stability != Enums.ModStability.IncompatibleAccordingToWorkshop)
                {
                    DataDump.AppendLine($"{ catalogMod.Name }, { Toolkit.GetWorkshopURL(catalogMod.SteamID) }");
                }
            }
        }


        // Dump last review date, name and workshop url for all non-incompatible mods that have not been reviewed in the last x months
        private static void DumpModsWithOldReview(Catalog ActiveCatalog, int months)
        {
            DumpTitle($"Mods with a old review (> { months } months old):");

            foreach (Mod catalogMod in ActiveCatalog.Mods)
            {
                if (catalogMod.ReviewDate != default && catalogMod.ReviewDate.AddMonths(months) < DateTime.Now && 
                    catalogMod.Stability != Enums.ModStability.IncompatibleAccordingToWorkshop)
                {
                    DataDump.AppendLine($"last review { Toolkit.DateString(catalogMod.ReviewDate) }: { catalogMod.Name }, " +
                        Toolkit.GetWorkshopURL(catalogMod.SteamID));
                }
            }
        }


        // Dump name, statuses and workshop url for all required mods that are not in a group
        private static void DumpRequiredUngroupedMods(Catalog ActiveCatalog)
        {
            DumpTitle("All required mods that are not in a group:");

            foreach (Mod catalogMod in ActiveCatalog.Mods)
            {
                if (ActiveCatalog.IsGroupMember(catalogMod.SteamID))
                {
                    continue;
                }

                // Find a mod that require this mod
                if (ActiveCatalog.Mods.Find(x => x.RequiredMods.Contains(catalogMod.SteamID)) != default) 
                {
                    // Get statuses
                    string statuses = "";

                    foreach (Enums.ModStatus status in catalogMod.Statuses)
                    {
                        statuses += ", " + status.ToString();
                    }

                    if (!string.IsNullOrEmpty(statuses))
                    {
                        statuses = " [" + statuses.Substring(2) + "]";
                    }

                    DataDump.AppendLine($"{ catalogMod.Name }{ statuses }, { Toolkit.GetWorkshopURL(catalogMod.SteamID) }");
                }
            }
        }


        // Dump id and name for all groups that are not used for required mods
        private static void DumpUnusedGroups(Catalog ActiveCatalog)
        {
            DumpTitle("Unused groups:");

            foreach (Group catalogGroup in ActiveCatalog.Groups)
            {
                // List groups that are not used as a required mod
                if (ActiveCatalog.Mods.Find(x => x.RequiredMods.Contains(catalogGroup.GroupID)) == default)
                {
                    DataDump.AppendLine(catalogGroup.ToString());
                }
            }
        }


        // Dump group ID and name, and remaining groupmember, for all groups with less than two members
        private static void DumpEmptyGroups(Catalog ActiveCatalog)
        {
            DumpTitle("Groups with less than 2 members:");

            foreach (Group catalogGroup in ActiveCatalog.Groups)
            {
                if (catalogGroup.GroupMembers.Count == 0)
                {
                    DataDump.AppendLine(catalogGroup.ToString() + ": no members");
                }
                else if (catalogGroup.GroupMembers.Count == 1)
                {
                    DataDump.AppendLine(catalogGroup.ToString() + ": only member is " +
                        ActiveCatalog.ModDictionary[catalogGroup.GroupMembers[0]].ToString());
                }
            }
        }


        // Dump name and workshop url for all authors with more than one mod; gives false positives for mods that contain both author ID and URL
        private static void DumpAuthorsWithMultipleMods(Catalog ActiveCatalog)
        {
            DumpTitle("Authors with more than one mod:");

            foreach (Author catalogAuthor in ActiveCatalog.Authors)
            {
                // List authors that have at least two mods
                if ((catalogAuthor.ProfileID != 0 ? ActiveCatalog.Mods.FindAll(x => x.AuthorID == catalogAuthor.ProfileID).Count : 0) +
                    (!string.IsNullOrEmpty(catalogAuthor.CustomURL) ? ActiveCatalog.Mods.FindAll(x => x.AuthorURL == catalogAuthor.CustomURL).Count : 0) > 1)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name }{ (catalogAuthor.Retired ? " [retired]" : "") }, " +
                        $"{ Toolkit.GetAuthorWorkshop(catalogAuthor.ProfileID, catalogAuthor.CustomURL) }");
                }
            }
        }


        // Dump name and workshop url for all authors with the retired status
        private static void DumpRetiredAuthors(Catalog ActiveCatalog)
        {
            DumpTitle("Retired authors:");

            foreach (Author catalogAuthor in ActiveCatalog.Authors)
            {
                if (catalogAuthor.Retired)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name }, { Toolkit.GetAuthorWorkshop(catalogAuthor.ProfileID, catalogAuthor.CustomURL) }");
                }
            }
        }


        // Dump name and workshop url for all authors that will get the retired status within x months
        private static void DumpAuthorsSoonRetired(Catalog ActiveCatalog, int months)
        {
            DumpTitle($"Authors that will retire within { months } months:");

            foreach (Author catalogAuthor in ActiveCatalog.Authors)
            {
                if (!catalogAuthor.Retired && catalogAuthor.LastSeen.AddMonths(ModSettings.monthsOfInactivityToRetireAuthor - months) < DateTime.Now)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name }, { Toolkit.GetAuthorWorkshop(catalogAuthor.ProfileID, catalogAuthor.CustomURL) }");
                }
            }
        }


        private static void DumpTitle(string title)
        {
            string separator = new string('=', title.Length);

            DataDump.AppendLine("\n\n" + separator + "\n" + title + "\n" + separator + "\n");
        }
    }
}
