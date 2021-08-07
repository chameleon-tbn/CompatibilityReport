using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CompatibilityReport.DataTypes;
using CompatibilityReport.Util;


// This dumps specific catalog data to a text file, to help with manual updating


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

            // Dump all relevant data
            DumpRequiredMods();

            DumpAuthorsWithMultipleMods();

            DumpAuthorsNotSeenLastYear();

            DumpModsWithoutReview();

            DumpAllMods();
        }


        // Dump name and workshop url for all mods
        private static void DumpAllMods()
        {
            Logger.DataDump("========================");
            Logger.DataDump("All mods in the catalog:");
            Logger.DataDump("========================");
            Logger.DataDump(" ");

            foreach (Mod mod in ActiveCatalog.Instance.Mods)
            {
                Logger.DataDump($"{ mod.Name }, { Toolkit.GetWorkshopURL(mod.SteamID) }");
            }

            Logger.DataDump(" ");
        }


        // Dump name and workshop url for all mods that have not been reviewed yet
        private static void DumpModsWithoutReview()
        {
            Logger.DataDump("======================");
            Logger.DataDump("Mods without a review:");
            Logger.DataDump("======================");
            Logger.DataDump(" ");

            foreach (Mod mod in ActiveCatalog.Instance.Mods)
            {
                if (mod.ReviewUpdated == default)
                {
                    Logger.DataDump($"{ mod.Name }, { Toolkit.GetWorkshopURL(mod.SteamID) }");
                }
            }

            Logger.DataDump(" ");
        }


        // Dump name, statuses and workshop url for all required mods; gives false positives for groups that are not used
        private static void DumpRequiredMods()
        {
            Logger.DataDump("==================");
            Logger.DataDump("All required mods:");
            Logger.DataDump("==================");
            Logger.DataDump(" ");

            foreach (Mod mod in ActiveCatalog.Instance.Mods)
            {
                // List mods that are a required mod directly or by group membership
                if (ActiveCatalog.Instance.Mods.Find(x => x.RequiredMods.Contains(mod.SteamID)) != default || ActiveCatalog.Instance.IsGroupMember(mod.SteamID)) 
                {
                    // Convert statuses to a string
                    string statuses = "";

                    foreach (Enums.ModStatus status in mod.Statuses)
                    {
                        statuses += ", " + status.ToString();
                    }

                    Logger.DataDump($"{ mod.Name }{ statuses }, { Toolkit.GetWorkshopURL(mod.SteamID) }");
                }
            }

            Logger.DataDump(" ");

        }


        // Dump name and workshop url for all authors with more than one mod; gives false positives for mods that contain both author ID and URLwwwwwww
        private static void DumpAuthorsWithMultipleMods()
        {
            Logger.DataDump("===============================");
            Logger.DataDump("Authors with more than one mod:");
            Logger.DataDump("===============================");
            Logger.DataDump(" ");

            foreach (Author author in ActiveCatalog.Instance.Authors)
            {
                // List authors that have at least two mods
                if ((author.ProfileID != 0 ? ActiveCatalog.Instance.Mods.FindAll(x => x.AuthorID == author.ProfileID).Count : 0) +
                    (!string.IsNullOrEmpty(author.CustomURL) ? ActiveCatalog.Instance.Mods.FindAll(x => x.AuthorURL == author.CustomURL).Count : 0) > 1)
                {
                    Logger.DataDump($"{ author.Name }, { Toolkit.GetAuthorWorkshop(author.ProfileID, author.CustomURL, modsOnly: true) }");
                }
            }

            Logger.DataDump(" ");

        }


        // Dump name and workshop url for all authors that were last seen more than a year ago
        private static void DumpAuthorsNotSeenLastYear()
        {
            Logger.DataDump("=================================");
            Logger.DataDump("Authors not seen for over a year:");
            Logger.DataDump("=================================");
            Logger.DataDump(" ");

            foreach (Author author in ActiveCatalog.Instance.Authors)
            {
                if (author.LastSeen.AddYears(1) < DateTime.Now)
                {
                    Logger.DataDump($"{ author.Name }, { Toolkit.GetAuthorWorkshop(author.ProfileID, author.CustomURL, modsOnly: true) }");
                }
            }

            Logger.DataDump(" ");

        }
    }
}
