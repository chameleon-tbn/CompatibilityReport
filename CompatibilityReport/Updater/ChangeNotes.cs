using System.Collections.Generic;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    public class ChangeNotes
    {
        public StringBuilder CatalogChanges { get; private set; } = new StringBuilder();
        public StringBuilder NewMods { get; private set; } = new StringBuilder();
        public StringBuilder NewGroups { get; private set; } = new StringBuilder();
        public StringBuilder NewCompatibilities { get; private set; } = new StringBuilder();
        public StringBuilder NewAuthors { get; private set; } = new StringBuilder();
        public Dictionary<ulong, string> UpdatedMods { get; private set; } = new Dictionary<ulong, string>();
        public Dictionary<ulong, string> UpdatedAuthorsByID { get; private set; } = new Dictionary<ulong, string>();
        public Dictionary<string, string> UpdatedAuthorsByUrl { get; private set; } = new Dictionary<string, string>();
        public StringBuilder RemovedMods { get; private set; } = new StringBuilder();
        public StringBuilder RemovedGroups { get; private set; } = new StringBuilder();
        public StringBuilder RemovedCompatibilities { get; private set; } = new StringBuilder();


        // Check if we have any change notes, ignoring generic catalog change notes.
        public bool Any()
        {
            return NewMods.Length + NewGroups.Length + NewCompatibilities.Length + NewAuthors.Length + UpdatedMods.Count + UpdatedAuthorsByID.Count +
                UpdatedAuthorsByUrl.Count + RemovedMods.Length + RemovedGroups.Length + RemovedCompatibilities.Length > 0;
        }


        // Add a change note for an updated mod.
        public void ModUpdate(ulong steamID, string extraChangeNote)
        {
            if (string.IsNullOrEmpty(extraChangeNote))
            {
                return;
            }

            if (UpdatedMods.ContainsKey(steamID))
            {
                if (!UpdatedMods[steamID].Contains(extraChangeNote))
                {
                    extraChangeNote = (extraChangeNote[0] == ',') ? extraChangeNote : ", " + extraChangeNote;
                    UpdatedMods[steamID] += $", { extraChangeNote }";
                }
            }
            else
            {
                UpdatedMods.Add(steamID, extraChangeNote);
            }
        }


        // Add a change note for an updated author.
        public void AuthorUpdate(Author catalogAuthor, string extraChangeNote)
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


        // Return the combined change notes.
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
                (UpdatedMods.Count + UpdatedAuthorsByID.Count + UpdatedAuthorsByUrl.Count == 0 ? "" :
                    "*** UPDATED: ***\n" +
                    CombineUpdatedMods(catalog).ToString() +
                    CombineUpdatedAuthors(catalog).ToString() +
                    "\n") +
                (RemovedMods.Length + RemovedGroups.Length + RemovedCompatibilities.Length == 0 ? "" :
                    "*** REMOVED: ***\n" +
                    RemovedMods.ToString() +
                    RemovedGroups.ToString() +
                    RemovedCompatibilities.ToString());
        }


        // Return the combined notes for updated mods. Also updates the related mod change notes.
        private StringBuilder CombineUpdatedMods(Catalog catalog)
        {
            StringBuilder combined = new StringBuilder();
            string todayDateString = Toolkit.DateString(catalog.UpdateDate);

            foreach (ulong steamID in UpdatedMods.Keys)
            {
                if (!string.IsNullOrEmpty(UpdatedMods[steamID]))
                {
                    catalog.GetMod(steamID).AddChangeNote($"{ todayDateString }: { UpdatedMods[steamID] }");
                    combined.AppendLine($"Updated mod { catalog.GetMod(steamID).ToString() }: { UpdatedMods[steamID] }");
                }
            }

            return combined;
        }


        // Return the combined notes for updated authors. Also updates the related author change notes.
        private StringBuilder CombineUpdatedAuthors(Catalog catalog)
        {
            StringBuilder combined = new StringBuilder();
            string todayDateString = Toolkit.DateString(catalog.UpdateDate);

            foreach (ulong authorID in UpdatedAuthorsByID.Keys)
            {
                catalog.GetAuthor(authorID, "").AddChangeNote($"{ todayDateString }: { UpdatedAuthorsByID[authorID] }");
                combined.AppendLine($"Updated author { catalog.GetAuthor(authorID, "").ToString() }: { UpdatedAuthorsByID[authorID] }");
            }

            foreach (string authorUrl in UpdatedAuthorsByUrl.Keys)
            {
                catalog.GetAuthor(0, authorUrl).AddChangeNote($"{ todayDateString }: { UpdatedAuthorsByUrl[authorUrl] }");
                combined.AppendLine($"Updated author { catalog.GetAuthor(0, authorUrl).ToString() }: { UpdatedAuthorsByUrl[authorUrl] }");
            }

            return combined;
        }
    }
}
