using System.Collections.Generic;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    public class ChangeNotes
    {
        private readonly StringBuilder CatalogChanges = new StringBuilder();
        private readonly StringBuilder NewMods = new StringBuilder();
        private readonly StringBuilder NewGroups = new StringBuilder();
        private readonly StringBuilder NewCompatibilities = new StringBuilder();
        private readonly StringBuilder NewAuthors = new StringBuilder();
        private readonly Dictionary<ulong, string> UpdatedModsByID = new Dictionary<ulong, string>();
        private readonly StringBuilder UpdatedMods = new StringBuilder();
        private readonly Dictionary<ulong, string> UpdatedAuthorsByID = new Dictionary<ulong, string>();
        private readonly Dictionary<string, string> UpdatedAuthorsByUrl = new Dictionary<string, string>();
        private readonly StringBuilder UpdatedAuthors = new StringBuilder();
        private readonly StringBuilder RemovedMods = new StringBuilder();
        private readonly StringBuilder RemovedGroups = new StringBuilder();
        private readonly StringBuilder RemovedCompatibilities = new StringBuilder();


        /// <summary>Adds a change note for a catalog change.</summary>
        public void AppendCatalogChange(string text)
        {
            CatalogChanges.AppendLine(text);
        }


        /// <summary>Adds a change note for a new mod.</summary>
        public void AppendNewMod(string text)
        {
            NewMods.AppendLine(text);
        }


        /// <summary>Adds a change note for a new group.</summary>
        public void AppendNewGroup(string text)
        {
            NewGroups.AppendLine(text);
        }


        /// <summary>Adds a change note for a new compatibility.</summary>
        public void AppendNewCompatibility(string text)
        {
            NewCompatibilities.AppendLine(text);
        }


        /// <summary>Adds a change note for a new author.</summary>
        public void AppendNewAuthor(string text)
        {
            NewAuthors.AppendLine(text);
        }


        /// <summary>Adds a change note for an updated mod.</summary>
        /// <remarks>Duplicate change notes will mostly be prevented.</remarks>
        public void AddUpdatedMod(ulong steamID, string extraChangeNote)
        {
            if (string.IsNullOrEmpty(extraChangeNote))
            {
                return;
            }

            if (UpdatedModsByID.ContainsKey(steamID))
            {
                if (!UpdatedModsByID[steamID].Contains(extraChangeNote))
                {
                    UpdatedModsByID[steamID] += $", { extraChangeNote }";
                }
            }
            else
            {
                UpdatedModsByID.Add(steamID, extraChangeNote);
            }
        }


        /// <summary>Adds a change note for an updated author.</summary>
        /// <remarks>Duplicate change notes will mostly be prevented.</remarks>
        public void AddUpdatedAuthor(Author catalogAuthor, string extraChangeNote)
        {
            if (string.IsNullOrEmpty(extraChangeNote))
            {
                return;
            }

            if (catalogAuthor.SteamID != 0)
            {
                if (UpdatedAuthorsByID.ContainsKey(catalogAuthor.SteamID))
                {
                    if (!UpdatedAuthorsByID[catalogAuthor.SteamID].Contains(extraChangeNote))
                    {
                        UpdatedAuthorsByID[catalogAuthor.SteamID] += $", { extraChangeNote }";
                    }
                }
                else
                {
                    UpdatedAuthorsByID.Add(catalogAuthor.SteamID, extraChangeNote);
                }
            }
            else
            {
                if (UpdatedAuthorsByUrl.ContainsKey(catalogAuthor.CustomUrl))
                {
                    if (!UpdatedAuthorsByUrl[catalogAuthor.CustomUrl].Contains(extraChangeNote))
                    {
                        UpdatedAuthorsByUrl[catalogAuthor.CustomUrl] += $", { extraChangeNote }";
                    }
                }
                else
                {
                    UpdatedAuthorsByUrl.Add(catalogAuthor.CustomUrl, extraChangeNote);
                }
            }
        }


        /// <summary>Adds a change note for a removed mod.</summary>
        public void AppendRemovedMod(string text)
        {
            RemovedMods.AppendLine(text);
        }


        /// <summary>Adds a change note for a removed group.</summary>
        public void AppendRemovedGroup(string text)
        {
            RemovedGroups.AppendLine(text);
        }


        /// <summary>Adds a change note for a removed compatibility.</summary>
        public void AppendRemovedCompatibility(string text)
        {
            RemovedCompatibilities.AppendLine(text);
        }


        /// <summary>Checks if we have any change notes, ignoring generic catalog change notes.</summary>
        /// <returns>True if have any change notes, false otherwise.</returns>
        public bool Any()
        {
            return NewMods.Length + NewGroups.Length + NewCompatibilities.Length + NewAuthors.Length + UpdatedMods.Length + UpdatedAuthors.Length +
                RemovedMods.Length + RemovedGroups.Length + RemovedCompatibilities.Length > 0;
        }


        /// <summary>Converts the change notes dictionaries for updated mods and authors to StringBuilders. 
        ///          Also writes the related change notes to the mods and authors.</summary>
        public void ConvertUpdated(Catalog catalog)
        {
            string todayDateString = Toolkit.DateString(catalog.UpdateDate);

            List<ulong> steamIDs = new List<ulong>(UpdatedModsByID.Keys);
            steamIDs.Sort();
            steamIDs.Reverse();

            foreach (ulong steamID in steamIDs)
            {
                if (!string.IsNullOrEmpty(UpdatedModsByID[steamID]))
                {
                    catalog.GetMod(steamID).AddChangeNote($"{ todayDateString }: { UpdatedModsByID[steamID] }");
                    UpdatedMods.AppendLine($"Updated mod { catalog.GetMod(steamID).ToString() }: { UpdatedModsByID[steamID] }");
                }
            }

            List<ulong>authorIDs = new List<ulong>(UpdatedAuthorsByID.Keys);
            authorIDs.Sort();
            authorIDs.Reverse();

            foreach (ulong authorID in authorIDs)
            {
                catalog.GetAuthor(authorID, "").AddChangeNote($"{ todayDateString }: { UpdatedAuthorsByID[authorID] }");
                UpdatedAuthors.AppendLine($"Updated author { catalog.GetAuthor(authorID, "").ToString() }: { UpdatedAuthorsByID[authorID] }");
            }

            foreach (string authorUrl in UpdatedAuthorsByUrl.Keys)
            {
                catalog.GetAuthor(0, authorUrl).AddChangeNote($"{ todayDateString }: { UpdatedAuthorsByUrl[authorUrl] }");
                UpdatedAuthors.AppendLine($"Updated author { catalog.GetAuthor(0, authorUrl).ToString() }: { UpdatedAuthorsByUrl[authorUrl] }");
            }
        }
        
        
        /// <summary>Combine the change notes.</summary>
        /// <returns>The combined change notes as string.</returns>
        public string Combined(Catalog catalog)
        {
            return $"Change Notes for Catalog { catalog.VersionString() }\n" +
                "-------------------------------\n" +
                $"{ catalog.UpdateDate:D}, { catalog.UpdateDate:t}\n" +
                "These change notes were automatically created by the updater process.\n" +
                "\n" +
                (CatalogChanges.Length == 0 ? "" :
                    "*** CATALOG CHANGES: ***\n" +
                    CatalogChanges.ToString() +
                    "\n") +
                (NewMods.Length + NewGroups.Length + NewAuthors.Length == 0 ? "" :
                    "*** ADDED: ***\n" +
                    NewMods.ToString() +
                    NewGroups.ToString() +
                    NewCompatibilities.ToString() +
                    NewAuthors.ToString() +
                    "\n") +
                (UpdatedMods.Length + UpdatedAuthors.Length == 0 ? "" :
                    "*** UPDATED: ***\n" +
                    UpdatedMods.ToString() +
                    UpdatedAuthors.ToString() +
                    "\n") +
                (RemovedMods.Length + RemovedGroups.Length + RemovedCompatibilities.Length == 0 ? "" :
                    "*** REMOVED: ***\n" +
                    RemovedMods.ToString() +
                    RemovedGroups.ToString() +
                    RemovedCompatibilities.ToString());
        }
    }
}
