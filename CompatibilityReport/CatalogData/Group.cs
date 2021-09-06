using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace CompatibilityReport.CatalogData
{
    [Serializable] 
    public class Group
    {
        // Groups are only used as required mods and recommended mods, where one (not all) of the mods from a group is a requirement or recommendation.
        // A mod can only be a member of one group, and that group is automatically added as required mod everywhere the group member is a required mod.
        // A group as recommendation has to be added and removed manually through the FileImporter.
        public ulong GroupID { get; private set; }
        public string Name { get; private set; }
        [XmlArrayItem("SteamID")] public List<ulong> GroupMembers { get; private set; } = new List<ulong>();


        // Default constructor for deserialization.
        private Group()
        {
            // Nothing to do here
        }


        // Constructor for group creation.
        public Group(ulong groupID, string name)
        {
            GroupID = groupID;
            Name = name ?? "";
        }


        // Return a formatted string with the group ID and name.
        public new string ToString()
        {
            return $"[Group { GroupID }] { Name }";
        }
    }
}
