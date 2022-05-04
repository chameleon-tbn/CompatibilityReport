using System;
using System.IO;
using System.Linq;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    public static class DataDumper
    {
        /// <summary>Dumps specific catalog data to a text file to help with creating CSV files for the FileImporter.</summary>
        /// <remarks>It's very inefficient with all the duplicate foreach loops, but it doesn't run for regular users and I like to keeps the code simple.</remarks>
        public static void Start(Catalog catalog)
        {
            StringBuilder DataDump = new StringBuilder(512);

            DateTime creationTime = DateTime.Now;
            DataDump.AppendLine($"{ ModSettings.ModName } DataDump, created on { creationTime:D}, { creationTime:t}.");
            DataDump.AppendLine($"Version { ModSettings.FullVersion } with catalog { catalog.VersionString() }.\n");

            DataDump.AppendLine($"Catalog has { catalog.Mods.Count } mods, { catalog.Groups.Count } groups, { catalog.Compatibilities.Count } compatibilities, " +
                $"{ catalog.Authors.Count } authors, { catalog.MapThemes.Count } ignored map themes and { catalog.RequiredAssets.Count } ignored required assets.");

            // Checks for potential errors:

            // Suppressed warnings about unnamed mods and duplicate authors.
            DumpSuppressedWarnings(catalog, DataDump);

            // Authors with their Steam ID as name. This is often due to a Steam error, but a few authors really have their ID as name.
            DumpAuthorsWithIDName(catalog, DataDump);

            // Required assets and map themes that are actually mods.
            DumpFakeNonMods(catalog, DataDump);

            // Unused group and groups with less than 2 members, to see if we can clean up.
            DumpUnusedGroups(catalog, DataDump);
            DumpEmptyGroups(catalog, DataDump);


            // Checks for needed review updates:

            // Authors that retire soon, to check them for activity in comments.
            DumpAuthorsSoonRetired(catalog, DataDump, weeks: ModSettings.WeeksForSoonRetired);

            // Mods with a new update
            DumpModsWithNewUpdate(catalog, DataDump);

            // Mods without a review and with an old review.
            DumpModsWithoutReview(catalog, DataDump);
            DumpModsWithOldReview(catalog, DataDump, months: 6);

            // Mods used as required/successor/alternative/recommended that are broken or have a successor.
            DumpUsedModsWithIssues(catalog, DataDump);

            // Mods with a old update but without the 'abandoned' status.
            DumpOldModsNotAbandoned(catalog, DataDump, months: 12);


            // Checks for potential review refinement:

            // Temporary datadumps
            // DumpInterestingForNow(catalog, DataDump);

            // Broken mods without successor or alternative.
            DumpBrokenModsWithoutSuccessor(catalog, DataDump);

            // Required and recommended mods that are not in a group, to check for the need of additional groups.
            DumpRequiredUngroupedMods(catalog, DataDump);

            // One time only checks: Authors with multiple mods (check for different version of the same mod), authors without ID.
            DumpAuthorsWithMultipleMods(catalog, DataDump);
            DumpAuthorsWithoutID(catalog, DataDump);

            Toolkit.SaveToFile(DataDump.ToString(), Path.Combine(ModSettings.UpdaterPath, ModSettings.DataDumpFileName), createBackup: true);
            Logger.UpdaterLog($"Datadump created as { ModSettings.DataDumpFileName }.");
        }


        /// <summary>Dumps all non-incompatible mods that have no review.</summary>
        /// <remarks>It lists the mods Workshop URL and name.</remarks>
        private static void DumpModsWithoutReview(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Mods without a review:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalogMod.Stability <= Enums.Stability.NotReviewed)
                {
                    DataDump.AppendLine($"{ WorkshopUrl(catalogMod.SteamID) } : { catalogMod.Name }");
                }
            }
        }


        /// <summary>Dumps all non-incompatible mods that have not been reviewed in the last x months.</summary>
        /// <remarks>It lists the mods Workshop URL, last review date and name.</remarks>
        private static void DumpModsWithOldReview(Catalog catalog, StringBuilder DataDump, int months)
        {
            DataDump.AppendLine(Title($"Mods with an old review (> { months } months old):"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalogMod.ReviewDate != default && catalogMod.ReviewDate.AddMonths(months) < DateTime.Now &&
                    catalogMod.Stability != Enums.Stability.IncompatibleAccordingToWorkshop)
                {
                    DataDump.AppendLine($"{ WorkshopUrl(catalogMod.SteamID) } : [last review { Toolkit.DateString(catalogMod.ReviewDate) }] { catalogMod.Name }");
                }
            }
        }


        /// <summary>Dumps all mods with an old update that don't have an abandoned, deprecated or obsolete status.</summary>
        /// <remarks>It lists the mods Workshop URL, name and stability. Incompatible mods are not included.</remarks>
        private static void DumpOldModsNotAbandoned(Catalog catalog, StringBuilder DataDump, int months)
        {
            DataDump.AppendLine(Title($"Mods with an update older than { months } months but without an abandoned/deprecated/obsolete status:"));

            DateTime deadline = DateTime.Today.AddMonths(-months);

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalogMod.SteamID > ModSettings.HighestFakeID && catalogMod.Stability != Enums.Stability.IncompatibleAccordingToWorkshop && catalogMod.Stability != Enums.Stability.NotReviewed &&
                    catalogMod.Updated < deadline && !catalogMod.Statuses.Contains(Enums.Status.Abandoned) && !catalogMod.Statuses.Contains(Enums.Status.Deprecated) &&
                    !catalogMod.Statuses.Contains(Enums.Status.Obsolete))
                {
                    DataDump.AppendLine($"{ WorkshopUrl(catalogMod.SteamID) } : { catalogMod.Name } [{ catalogMod.Stability }]");
                }
            }
        }


        /// <summary>Dumps all reviewed mods with an update date equal to or newer than their review date.</summary>
        /// <remarks>It lists the mods Workshop URL and name.</remarks>
        private static void DumpModsWithNewUpdate(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Mods with an update newer than their review:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalogMod.Stability > Enums.Stability.NotReviewed && catalogMod.ReviewDate != default && catalogMod.Updated.Date >= catalogMod.ReviewDate.Date)
                {
                    DataDump.AppendLine($"{ WorkshopUrl(catalogMod.SteamID) } : { catalogMod.Name }");
                }
            }
        }


        /// <summary>Dumps all mods that are broken or worse, and don't have a successor or alternative.</summary>
        /// <remarks>It lists the mods Workshop URL, name and stability. It skips all 'ali213_mod_01' mods.</remarks>
        private static void DumpBrokenModsWithoutSuccessor(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Broken (or worse) mods without a successor or alternative:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalogMod.Stability >= Enums.Stability.Broken && !catalogMod.Successors.Any() && !catalogMod.Alternatives.Any() && catalogMod.Name != "ali213_mod_01")
                {
                    DataDump.AppendLine($"{ WorkshopUrl(catalogMod.SteamID) } : { catalogMod.Name } [{ catalogMod.Stability }]");
                }
            }
        }


        /// <summary>Dumps all mods used as required/successor/alternative/recommendation that have a successor themselves or are broken (or worse).</summary>
        /// <remarks>It lists the mods Workshop URL, name and stability.</remarks>
        private static void DumpUsedModsWithIssues(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("'Used' mods that have a successor or are broken:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if ((catalogMod.Stability >= Enums.Stability.Broken || catalogMod.Successors.Any()) && 
                    catalog.Mods.Find(x => x.RequiredMods.Contains(catalogMod.SteamID) || x.Successors.Contains(catalogMod.SteamID) || 
                        x.Alternatives.Contains(catalogMod.SteamID) || x.Recommendations.Contains(catalogMod.SteamID)) != default)
                {
                    DataDump.AppendLine($"{ WorkshopUrl(catalogMod.SteamID) } : { catalogMod.Name } " +
                        $"[{ catalogMod.Stability }{ (catalogMod.Successors.Any() ? ", has successor" : "") }]");
                }
            }
        }


        /// <summary>Dumps all required and recommended mods that are not in a group.</summary>
        /// <remarks>It lists the mods Workshop URL, name, stability and statuses.</remarks>
        private static void DumpRequiredUngroupedMods(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Required and recommended mods that are not in a group:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (!catalog.IsGroupMember(catalogMod.SteamID) && (catalog.Mods.Find(x => x.RequiredMods.Contains(catalogMod.SteamID)) != default || 
                    catalog.Mods.Find(x => x.Recommendations.Contains(catalogMod.SteamID)) != default))
                {
                    string statuses = "";

                    foreach (Enums.Status status in catalogMod.Statuses)
                    {
                        statuses += $", { status }";
                    }

                    DataDump.AppendLine($"{ WorkshopUrl(catalogMod.SteamID) } : { catalogMod.Name } [{ catalogMod.Stability }{ statuses }]");
                }
            }
        }


        /// <summary>Dumps all groups with less than two members.</summary>
        /// <remarks>It lists the groups ID, name and remaining groupmember.</remarks>
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


        /// <summary>Dumps all groups that are not used for required or recommended mods.</summary>
        /// <remarks>It lists the groups ID and name.</remarks>
        private static void DumpUnusedGroups(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Unused groups:"));

            foreach (Group catalogGroup in catalog.Groups)
            {
                bool groupIsUsed = false;

                foreach (ulong groupMember in catalogGroup.GroupMembers)
                {
                    groupIsUsed = groupIsUsed || catalog.Mods.Find(x => x.RequiredMods.Contains(groupMember)) != default ||
                        catalog.Mods.Find(x => x.Recommendations.Contains(groupMember)) != default;
                }

                if (!groupIsUsed)
                {
                    DataDump.AppendLine(catalogGroup.ToString());
                }
            }
        }


        /// <summary>Dumps all authors with more than one mod.</summary>
        /// <remarks>It lists the authors name, retired state and Workshop URL.</remarks>
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
                    DataDump.AppendLine($"{ catalogAuthor.Name }{ (catalogAuthor.Retired ? " [retired]" : "") } : " +
                        $"{ Toolkit.GetAuthorWorkshopUrl(catalogAuthor.SteamID, catalogAuthor.CustomUrl) }");
                }
            }
        }

        
        /// <summary>Dumps all authors that have their ID as name.</summary>
        /// <remarks>It lists the authors name and Workshop URL.</remarks>
        private static void DumpAuthorsWithIDName(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title($"Authors with their ID as name:"));

            foreach (Author catalogAuthor in catalog.Authors)
            {
                if (catalogAuthor.Name == catalogAuthor.SteamID.ToString())
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name } : { Toolkit.GetAuthorWorkshopUrl(catalogAuthor.SteamID, catalogAuthor.CustomUrl) }");
                }
            }
        }


        /// <summary>Dumps all authors that have no ID but only a custom URL.</summary>
        /// <remarks>It lists the authors name, custom URL and Workshop URL.</remarks>
        private static void DumpAuthorsWithoutID(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title($"Authors without Author ID:"));

            foreach (Author catalogAuthor in catalog.Authors)
            {
                if (catalogAuthor.SteamID == 0)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name } : { catalogAuthor.CustomUrl }, { Toolkit.GetAuthorWorkshopUrl(0, catalogAuthor.CustomUrl) }");
                }
            }
        }


        /// <summary>Dumps all authors that will get the retired status within x months.</summary>
        /// <remarks>It lists the authors name and Workshop URL.</remarks>
        private static void DumpAuthorsSoonRetired(Catalog catalog, StringBuilder DataDump, int weeks)
        {
            DataDump.AppendLine(Title($"Authors that will retire within { weeks } week{ (weeks > 1 ? "s" : "") }:"));

            foreach (Author catalogAuthor in catalog.Authors)
            {
                if (!catalogAuthor.Retired && catalogAuthor.LastSeen.AddDays(ModSettings.DaysOfInactivityToRetireAuthor - (weeks * 7)) < DateTime.Now)
                {
                    DataDump.AppendLine($"{ catalogAuthor.Name } : { Toolkit.GetAuthorWorkshopUrl(catalogAuthor.SteamID, catalogAuthor.CustomUrl) }");
                }
            }
        }


        /// <summary>Dumps the suppressed warnings about unnamed mods and duplicate authors.</summary>
        /// <remarks>It lists the Steam ID and name from the mods and authors.</remarks>
        private static void DumpSuppressedWarnings(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Suppressed warnings:"));

            foreach (ulong steamID in catalog.SuppressedWarnings)
            {
                Mod suppressedMod = catalog.GetMod(steamID);
                Author suppressedAuthor = catalog.GetAuthor(steamID, "");

                if (suppressedMod != null)
                {
                    DataDump.AppendLine(suppressedMod.ToString());
                }
                if (suppressedAuthor != null)
                {
                    DataDump.AppendLine(suppressedAuthor.ToString());
                }
                else if (suppressedMod == null && suppressedAuthor == null)
                {
                    DataDump.AppendLine($"Unknown mod or author { steamID }");
                }
            }
        }


        /// <summary>Dumps required assets and map themes that are actually existing mods.</summary>
        /// <remarks>It lists the mods Workshop URL and name.</remarks>
        private static void DumpFakeNonMods(Catalog catalog, StringBuilder DataDump)
        {
            DataDump.AppendLine(Title("Required assets and/or map themes that are actually mods:"));

            foreach (ulong steamID in catalog.RequiredAssets.Concat(catalog.MapThemes))
            {
                Mod actualMod = catalog.GetMod(steamID);

                if (actualMod != null)
                {
                    DataDump.AppendLine($"{ WorkshopUrl(actualMod.SteamID) } : { actualMod.Name }");
                }
            }
        }


        /// <summary>Dumps data that is interesting for now.</summary>
#pragma warning disable IDE0051 // Remove unused private members
        private static void DumpInterestingForNow(Catalog catalog, StringBuilder DataDump)
#pragma warning restore IDE0051
        {
            DataDump.AppendLine(Title("All issues and compatibilities with BloodyPenguin's mods:"));

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (catalogMod.AuthorUrl == "bloody_penguin")
                {
                    DataDump.AppendLine($"{ catalogMod.Name } - { WorkshopUrl(catalogMod.SteamID) }");

                    DataDump.AppendLine($"Stability      : { catalogMod.Stability }");
                    if (!string.IsNullOrEmpty(catalogMod.StabilityNote))
                    {
                        DataDump.AppendLine($"Stability note : { catalogMod.StabilityNote.Replace("\n", "\n                 ") }");
                    }

                    if (catalogMod.Statuses.Any())
                    {
                        string statuses = "";
                        foreach (Enums.Status status in catalogMod.Statuses)
                        {
                            statuses += (statuses == "" ? "" : ", ") + status.ToString();
                        }
                        DataDump.AppendLine($"Statuses       : { statuses }");
                    }

                    if (!string.IsNullOrEmpty(catalogMod.Note))
                    {
                        DataDump.AppendLine($"Generic Note   : { catalogMod.Note.Replace("\n", "\n                 ") }");
                    }

                    if (catalogMod.Successors.Any())
                    {
                        string successors = "";
                        foreach (ulong successor in catalogMod.Successors)
                        {
                            successors += (successors == "" ? "" : "\n                 ") + catalog.GetMod(successor).ToString();
                        }
                        DataDump.AppendLine($"Successor(s)   : { successors }");
                    }

                    if (catalogMod.Alternatives.Any())
                    {
                        string alternatives = "";
                        foreach (ulong alternative in catalogMod.Alternatives)
                        {
                            alternatives += (alternatives == "" ? "" : "\n                 ") + catalog.GetMod(alternative).ToString();
                        }
                        DataDump.AppendLine($"Alternative(s) : { alternatives }");
                    }

                    string compatibilities = "";
                    foreach (Compatibility compatibility in catalog.Compatibilities)
                    {
                        if (compatibility.Status == Enums.CompatibilityStatus.CompatibleAccordingToAuthor)
                        {
                            continue;
                        }

                        if (compatibility.FirstModID == catalogMod.SteamID && compatibility.SecondModID != 1372431101 && compatibility.SecondModID != 1386697922)
                        {
                            compatibilities += $"  * { compatibility.SecondModName } [{ compatibility.SecondModID }]: { compatibility.Status }\n";
                            if (!string.IsNullOrEmpty(compatibility.Note)) 
                            {
                                compatibilities += $"    Note: { compatibility.Note }\n";
                            }
                        }
                        else if (compatibility.SecondModID == catalogMod.SteamID && compatibility.FirstModID != 1372431101 && compatibility.FirstModID != 1386697922)
                        {
                            compatibilities += $"  * { compatibility.FirstModName } [{ compatibility.FirstModID }]: { compatibility.Status }\n";
                            if (!string.IsNullOrEmpty(compatibility.Note))
                            {
                                compatibilities += $"    Note: { compatibility.Note }\n";
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(compatibilities))
                    {
                        DataDump.AppendLine("Compatibilities:");
                        DataDump.AppendLine(compatibilities);
                    }
                    else
                    {
                        DataDump.AppendLine("\n");
                    }

                    DataDump.AppendLine("\n");
                }
            }
        }

                
        /// <summary>Formats a title.</summary>
        /// <returns>A formatted title with blank lines and separators.</returns>
        private static string Title(string title)
        {
            string separator = new string('=', title.Length);

            return $"\n\n{ separator }\n{ title }\n{ separator }";
        }


        /// <summary>Gets the Steam Workshop URL for a mod.</summary>
        /// <remarks>An extra space is added on smaller Steam IDs to make every workshop URL equal width.</remarks>
        /// <returns>A string with the workshop URL.</returns>
        private static string WorkshopUrl(ulong steamID)
        {
            return $"{ Toolkit.GetWorkshopUrl(steamID) }{ (steamID < 1000000000 ? " " : "") }";
        }
    }
}
