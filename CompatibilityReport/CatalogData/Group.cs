using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace CompatibilityReport.CatalogData
{
    /// <summary>Groups are used for required and recommended mods. If a required/recommended mod is not subscribed but is a group member, 
    ///          the reporter checks if another member is subscribed. The original required/recommended mod will not be reported missing if so.</summary>
    /// <remarks>A mod can only be a member of one group.</remarks>
    [Serializable] 
    public class Group
    {
        public ulong GroupID { get; private set; }
        public string Name { get; private set; }
        [XmlArrayItem("SteamID")] public List<ulong> GroupMembers { get; private set; } = new List<ulong>();


        /// <summary>Default constructor for deserialization.</summary>
        private Group()
        {
            // Nothing to do here
        }


        /// <summary>Constructor for group creation.</summary>
        public Group(ulong groupID, string name)
        {
            GroupID = groupID;
            Name = name ?? "";
        }


        /// <summary>Converts the group to a string containing the group ID and name.</summary>
        /// <returns>A string representing the group.</returns>
        public new string ToString()
        {
            return $"[Group { GroupID }] { Name }";
        }
    }
}
