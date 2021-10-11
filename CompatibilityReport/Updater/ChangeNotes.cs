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
        private readonly Dictionary<ulong, string> UpdatedModsDict = new Dictionary<ulong, string>();
        private readonly StringBuilder UpdatedMods = new StringBuilder();
        private readonly Dictionary<Author, string> UpdatedAuthorsDict = new Dictionary<Author, string>();
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

            if (UpdatedModsDict.ContainsKey(steamID))
            {
                if (!UpdatedModsDict[steamID].Contains(extraChangeNote))
                {
                    UpdatedModsDict[steamID] += $", { extraChangeNote }";
                }
            }
            else
            {
                UpdatedModsDict.Add(steamID, extraChangeNote);
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

            if (UpdatedAuthorsDict.ContainsKey(catalogAuthor))
            {
                if (!UpdatedAuthorsDict[catalogAuthor].Contains(extraChangeNote))
                {
                    UpdatedAuthorsDict[catalogAuthor] += $", { extraChangeNote }";
                }
            }
            else
            {
                UpdatedAuthorsDict.Add(catalogAuthor, extraChangeNote);
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
            return NewMods.Length + NewGroups.Length + NewCompatibilities.Length + NewAuthors.Length + UpdatedMods.Length + UpdatedModsDict.Count + 
                UpdatedAuthors.Length + UpdatedAuthorsDict.Count + RemovedMods.Length + RemovedGroups.Length + RemovedCompatibilities.Length > 0;
        }


        /// <summary>Converts the change notes dictionaries for updated mods and authors to StringBuilders. 
        ///          Also writes the related change notes to the mods and authors.</summary>
        public void ConvertUpdated(Catalog catalog)
        {
            string todayDateString = Toolkit.DateString(catalog.Updated);

            List<ulong> steamIDs = new List<ulong>(UpdatedModsDict.Keys);
            steamIDs.Sort();
            steamIDs.Reverse();

            foreach (ulong steamID in steamIDs)
            {
                if (!string.IsNullOrEmpty(UpdatedModsDict[steamID]))
                {
                    catalog.GetMod(steamID).AddChangeNote($"{ todayDateString }: { UpdatedModsDict[steamID] }");
                    UpdatedMods.AppendLine($"Updated mod { Toolkit.CutOff(catalog.GetMod(steamID).ToString(), 55), -55 }: { UpdatedModsDict[steamID] }");
                }
            }

            foreach (Author catalogAuthor in UpdatedAuthorsDict.Keys)
            {
                catalogAuthor.AddChangeNote($"{ todayDateString }: { UpdatedAuthorsDict[catalogAuthor] }");
                UpdatedAuthors.AppendLine($"Updated author { Toolkit.CutOff(catalogAuthor.ToString(), 52), -52 }: { UpdatedAuthorsDict[catalogAuthor] }");
            }
        }
        
        
        /// <summary>Combine the change notes.</summary>
        /// <returns>The combined change notes as string.</returns>
        public string Combined(Catalog catalog)
        {
            return $"Change Notes for Catalog { catalog.VersionString() }\n" +
                "-------------------------------\n" +
                $"{ catalog.Updated:D}, { catalog.Updated:t}\n" +
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
