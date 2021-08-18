using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CompatibilityReport.DataTypes;
using CompatibilityReport.Util;


// This dumps specific catalog data to a text file, to help with creating CSV files for the import


namespace CompatibilityReport.Updater
{
    internal static class DataDumper
    {
        internal static void Start()
        {
            if (!ModSettings.UpdaterEnabled || !ActiveCatalog.Init()) 
            {
                return; 
            }

            Logger.DataDump($"{ ModSettings.modName } { ModSettings.fullVersion }, catalog { ActiveCatalog.Instance.VersionString() }. " +
                $"DataDump, created on { DateTime.Now:D}, { DateTime.Now:t}.");

            // Unused groups, to see if we can clean up
            DumpUnusedGroups();

            // Groups with less than 2 members
            DumpEmptyGroups();

            // Required mods that are not in a group, to check for the need of additional groups
            DumpRequiredUngroupedMods();

            // Authors that retire soon, to check them for activity in comments
            DumpAuthorsSoonRetired(months: 2);

            // Retired authors, for a one time check at the start of this mod for activity in comments
            DumpRetiredAuthors();

            // Authors with multiple mods, for a check of different version of the same mods (test vs stable)
            DumpAuthorsWithMultipleMods();

            // Mods without a review, to know which to review yet
            DumpModsWithoutReview();

            // Mods with an old review, to know which to review again
            DumpModsWithOldReview(weeks: 6);

            // All mods, to have easy access to all workshop URLs
            // DumpAllMods();

            Logger.UpdaterLog($"Datadump created as \"{ Toolkit.GetFileName(ModSettings.dataDumpFullPath) }\".");
        }


        // Dump name and workshop url for all mods
        private static void DumpAllMods()
        {
            DumpTitle("All mods in the catalog:");

            foreach (Mod mod in ActiveCatalog.Instance.Mods)
            {
                Logger.DataDump($"{ mod.Name }, { Toolkit.GetWorkshopURL(mod.SteamID) }");
            }
        }


        // Dump name and workshop url for all non-incompatible mods that have not been reviewed yet
        private static void DumpModsWithoutReview()
        {
            DumpTitle("Mods without a review:");

            foreach (Mod mod in ActiveCatalog.Instance.Mods)
            {
                if (mod.ReviewDate == default && mod.Stability != Enums.ModStability.IncompatibleAccordingToWorkshop)
                {
                    Logger.DataDump($"{ mod.Name }, { Toolkit.GetWorkshopURL(mod.SteamID) }");
                }
            }
        }


        // Dump name and workshop url for all non-incompatible mods that have not been reviewed in the last month
        private static void DumpModsWithOldReview(int weeks)
        {
            DumpTitle($"Mods wit a old review (> { weeks } weeks):");

            foreach (Mod mod in ActiveCatalog.Instance.Mods)
            {
                if (mod.ReviewDate.AddDays(weeks * 7) < DateTime.Now && mod.Stability != Enums.ModStability.IncompatibleAccordingToWorkshop)
                {
                    Logger.DataDump($"last review { Toolkit.DateString(mod.ReviewDate) }: { mod.Name }, { Toolkit.GetWorkshopURL(mod.SteamID) }");
                }
            }
        }


        // Dump name, statuses and workshop url for all required mods that are not in a group
        private static void DumpRequiredUngroupedMods()
        {
            DumpTitle("All required mods:");

            foreach (Mod mod in ActiveCatalog.Instance.Mods)
            {
                if (ActiveCatalog.Instance.IsGroupMember(mod.SteamID))
                {
                    continue;
                }

                // Find a mod that require this mod
                if (ActiveCatalog.Instance.Mods.Find(x => x.RequiredMods.Contains(mod.SteamID)) != default) 
                {
                    // Get statuses
                    string statuses = "";

                    foreach (Enums.ModStatus status in mod.Statuses)
                    {
                        statuses += ", " + status.ToString();
                    }

                    if (!string.IsNullOrEmpty(statuses))
                    {
                        statuses = " [" + statuses.Substring(2) + "]";
                    }

                    Logger.DataDump($"{ mod.Name }{ statuses }, { Toolkit.GetWorkshopURL(mod.SteamID) }");
                }
            }
        }


        // Dump id and name for all groups that are not used for required mods
        private static void DumpUnusedGroups()
        {
            DumpTitle("Unused groups:");

            foreach (Group group in ActiveCatalog.Instance.Groups)
            {
                // List groups that are not used as a required mod
                if (ActiveCatalog.Instance.Mods.Find(x => x.RequiredMods.Contains(group.GroupID)) == default)
                {
                    Logger.DataDump(group.ToString());
                }
            }
        }


        private static void DumpEmptyGroups()
        {
            DumpTitle("Groups with less than 2 members:");

            foreach (Group group in ActiveCatalog.Instance.Groups)
            {
                if (group.GroupMembers.Count == 0)
                {
                    Logger.DataDump(group.ToString() + ": no members");
                }
                else if (group.GroupMembers.Count == 1)
                {
                    Logger.DataDump(group.ToString() + $": only member is { ActiveCatalog.Instance.ModDictionary[group.GroupMembers[0]].ToString(cutOff: false) }");
                }
            }
        }


        // Dump name and workshop url for all authors with more than one mod; gives false positives for mods that contain both author ID and URLwwwwwww
        private static void DumpAuthorsWithMultipleMods()
        {
            DumpTitle("Authors with more than one mod:");

            foreach (Author author in ActiveCatalog.Instance.Authors)
            {
                // List authors that have at least two mods
                if ((author.ProfileID != 0 ? ActiveCatalog.Instance.Mods.FindAll(x => x.AuthorID == author.ProfileID).Count : 0) +
                    (!string.IsNullOrEmpty(author.CustomURL) ? ActiveCatalog.Instance.Mods.FindAll(x => x.AuthorURL == author.CustomURL).Count : 0) > 1)
                {
                    Logger.DataDump($"{ author.Name }{ (author.Retired ? " [retired]" : "") }, { Toolkit.GetAuthorWorkshop(author.ProfileID, author.CustomURL) }");
                }
            }
        }


        // Dump name and workshop url for all authors with the retired status
        private static void DumpRetiredAuthors()
        {
            DumpTitle("Retired authors:");

            foreach (Author author in ActiveCatalog.Instance.Authors)
            {
                if (author.Retired)
                {
                    Logger.DataDump($"{ author.Name }, { Toolkit.GetAuthorWorkshop(author.ProfileID, author.CustomURL) }");
                }
            }
        }


        // Dump name and workshop url for all authors that will get the retired status within 2 months
        private static void DumpAuthorsSoonRetired(int months)
        {
            DumpTitle($"Authors that will retire within { months } months:");

            foreach (Author author in ActiveCatalog.Instance.Authors)
            {
                if (!author.Retired && author.LastSeen.AddMonths(ModSettings.monthsOfInactivityToRetireAuthor - months) < DateTime.Now)
                {
                    Logger.DataDump($"{ author.Name }, { Toolkit.GetAuthorWorkshop(author.ProfileID, author.CustomURL) }");
                }
            }
        }


        private static void DumpTitle(string title)
        {
            string separator = new string('=', title.Length);

            Logger.DataDump("\n\n" + separator + "\n" + title + "\n" + separator + "\n");
        }
    }
}
