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
            // Exit if the updater is not enabled
            if (!ModSettings.UpdaterEnabled)
            {
                return;
            }

            // Exit if we don't have and can't get an active catalog
            if (!ActiveCatalog.Init()) 
            {
                return; 
            }

            // Log name, versions and date
            Logger.DataDump($"{ ModSettings.modName } { ModSettings.fullVersion }, catalog { ActiveCatalog.Instance.VersionString() }. " +
                $"DataDump, created on { DateTime.Now:D}, { DateTime.Now:t}.");

            // Unused groups, to see if we can clean up
            DumpUnusedGroups();

            // Required mods, to check for the need of groups
            DumpRequiredMods();

            // Authors that retire within 2 months, to check for activity in comments
            DumpAuthorsSoonRetired();

            // Retired authors, for a one time check at the start of this mod for activity in comments
            DumpRetiredAuthors();

            // Authors with multiple mods, for a check of different version of the same mods (test vs stable)
            DumpAuthorsWithMultipleMods();

            // Mods without a review, so I know which to review yet
            DumpModsWithoutReview();

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
                if (mod.ReviewUpdated == default && !mod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    Logger.DataDump($"{ mod.Name }, { Toolkit.GetWorkshopURL(mod.SteamID) }");
                }
            }
        }


        // Dump name, statuses and workshop url for all required mods, and all mods in groups; gives false positives for groups that are not used
        private static void DumpRequiredMods()
        {
            DumpTitle("All required mods:");

            foreach (Mod mod in ActiveCatalog.Instance.Mods)
            {
                // List mods that are a required mod directly or by group membership
                if (ActiveCatalog.Instance.Mods.Find(x => x.RequiredMods.Contains(mod.SteamID)) != default || ActiveCatalog.Instance.IsGroupMember(mod.SteamID)) 
                {
                    // Get group membership and statuses
                    string statuses = ActiveCatalog.Instance.IsGroupMember(mod.SteamID) ? ", group member" : "";

                    foreach (Enums.ModStatus status in mod.Statuses)
                    {
                        statuses += ", " + status.ToString();
                    }

                    if (statuses != "")
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
        private static void DumpAuthorsSoonRetired()
        {
            DumpTitle("Authors that will retire within 2 months:");

            foreach (Author author in ActiveCatalog.Instance.Authors)
            {
                if (!author.Retired && author.LastSeen.AddMonths(ModSettings.monthsOfInactivityToRetireAuthor - 2) < DateTime.Now)
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
