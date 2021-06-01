using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using ModChecker.Util;


// Mod groups are only used for Required Mods in the Mod class, and mean that one of the mods from a group is a requirement (not all together)

namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class ModGroup
    {
        // Group ID, which is used instead of a Steam ID in a required mods list
        public ulong GroupID { get; private set; }

        // Steam IDs of mods in this group; nesting group IDs in this is not supported
        [XmlArrayItem("SteamID")] public List<ulong> SteamIDs { get; private set; } = new List<ulong>();

        // A description of this group, for catalog maintenance only (not shown in reports)
        public string Description { get; private set; }


        // Default constructor
        public ModGroup()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        internal ModGroup(ulong groupID,
                          List<ulong> steamIDs,
                          string description)
        {
            GroupID = groupID;

            SteamIDs = steamIDs ?? new List<ulong>();

            Description = description ?? "";

            if ((GroupID < ModSettings.lowestModGroupID) || (GroupID > ModSettings.highestModGroupID))
            {
                Logger.Log($"ModGroup ID out range: [{ GroupID }] { Description }. This might give weird results in the report.", Logger.error);
            }

            if (SteamIDs.Count < 2)
            {
                Logger.Log($"Found ModGroup with less than 2 members: [{ GroupID }] { Description }.", Logger.warning);
            }
        }
    }
}
