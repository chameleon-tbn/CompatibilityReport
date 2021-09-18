using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    public static class DataDumper
    {
        /// <summary>Dumps specific catalog data to a text file to help with creating CSV files for the FileImporter.</summary>
        /// <remarks>It's inefficient with duplicate foreach loops, but the 100 ms it takes don't count up to the 12 to 15 minutes from the WebCrawler, 
        ///          and this keeps the code simple.</remarks>
        public static void Start(Catalog catalog)
        {
            Stopwatch timer = Stopwatch.StartNew();
            StringBuilder DataDump = new StringBuilder(512);

            DataDump.AppendLine($"{ ModSettings.ModName } DataDump, created on { DateTime.Now:D}, { DateTime.Now:t}.");
            DataDump.AppendLine($"Version { ModSettings.FullVersion } with catalog { catalog.VersionString() }.");

            // Groups with less than 2 members, to see if we can clean up.
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

            Toolkit.SaveToFile(DataDump.ToString(), Path.Combine(ModSettings.UpdaterPath, ModSettings.DataDumpFileName), createBackup: true);

            timer.Stop();
            Logger.UpdaterLog($"Datadump created in { Toolkit.TimeString(timer.ElapsedMilliseconds) }, as { ModSettings.DataDumpFileName }.");
        }


        /// <summary>Dumps info about all non-incompatible mods that have not been reviewed yet</summary>
        /// <remarks>It dumps the mod name and Workshop URL.</remarks>
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


        /// <summary>Dump info about all non-incompatible mods that have not been reviewed in the last x months.</summary>
        /// <remarks>It dumps the mods last review date, name and Workshop URL.</remarks>
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


        /// <summary>Dumps info about all required mods that are not in a group.</summary>
        /// <remarks>It dumps the mod name, stability, statuses and Workshop URL.</remarks>
        private static void DumpRequiredUngroupedMods(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Required mods that are not in a group:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (!catalog.IsGroupMember(catalogMod.SteamID) && catalog.Mods.Find(x => x.RequiredMods.Contains(catalogMod.SteamID)) != default) 
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


        /// <summary>Dumps info about all groups that are not used for required or recommended mods.</summary>
        /// <remarks>It dumps the group ID and name.</remarks>
        private static void DumpUnusedGroups(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Unused groups:"));

            foreach (Group catalogGroup in catalog.Groups)
            {
                bool groupIsUsed = false;

                foreach (ulong groupMember in catalogGroup.GroupMembers)
                {
                    groupIsUsed = groupIsUsed && (catalog.Mods.Find(x => x.RequiredMods.Contains(groupMember)) != default ||
                        catalog.Mods.Find(x => x.Recommendations.Contains(groupMember)) != default);
                }

                if (!groupIsUsed)
                {
                    DataDump.AppendLine(catalogGroup.ToString());
                }
            }
        }


        /// <summary>Dumps info about all groups with less than two members.</summary>
        /// <remarks>It dumps the group ID, name and remaining groupmember.</remarks>
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
                    DataDump.AppendLine($"{ catalogGroup.ToString() }: only member is { catalog.GetMod(catalogGroup.GroupMembers[0]).ToString() }");
                }
            }
        }


        /// <summary>Dump info about all authors with more than one mod.</summary>
        /// <remarks>It dumps the authors name, retired state and Workshop URL.</remarks>
        private static void DumpAuthorsWithMultipleMods(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Authors with more than one mod:"));

            foreach (Author catalogAuthor in catalog.Authors)
            {
                if (catalogAuthor.SteamID == ModSettings.FakeAuthorIDforColossalOrder)
                {
                    continue;
                }

                int modCount = (catalogAuthor.SteamID != 0) ? catalog.Mods.FindAll(x => x.AuthorID == catalogAuthor.SteamID).Count : 
                    catalog.Mods.FindAll(x => x.AuthorUrl == catalogAuthor.CustomUrl).Count;

                if (modCount > 1)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name }{ (catalogAuthor.Retired ? " [retired]" : "") }, " +
                        $"{ Toolkit.GetAuthorWorkshopUrl(catalogAuthor.SteamID, catalogAuthor.CustomUrl) }");
                }
            }
        }


        /// <summary>Dump info about all authors with the retired status.</summary>
        /// <remarks>It dumps the authors name and Workshop URL.</remarks>
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


        /// <summary>Dump info about all authors that will get the retired status within x months.</summary>
        /// <remarks>It dumps the authors name and Workshop URL.</remarks>
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


        /// <summary>Formats a title.</summary>
        /// <returns>A formatted title with blank lines and separators.</returns>
        private static string Title(string title)
        {
            string separator = new string('=', title.Length);

            return $"\n\n{ separator }\n{ title }\n{ separator }\n";
        }
    }
}
