using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using ModChecker.Util;


// Mod groups are only used for Required Mods in the Mod class, and mean that any one of the mods from a group is a requirement (not all together)

namespace ModChecker.DataTypes
{
    [Serializable]
    public class ModGroup                           // Needs to be public for XML serialization
    {
        public ulong GroupID { get; private set; }

        [XmlArrayItem("SteamID")] public List<ulong> SteamIDs { get; private set; } = new List<ulong>();

        public string Description { get; private set; }


        // Default constructor
        public ModGroup()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        public ModGroup(ulong groupID,
                        List<ulong> steamIDs,
                        string description)
        {
            if ((groupID < ModSettings.lowestModGroupID) || (groupID > ModSettings.highestModGroupID))
            {
                Logger.Log($"ModGroup ID out range: [{ groupID }] { description }. This might give weird results in the report.", Logger.error);
            }

            GroupID = groupID;

            Description = description;

            SteamIDs = steamIDs;

            if (SteamIDs == null)
            {
                Logger.Log($"Found 'null' ModGroup: [{ groupID }] { description }.", Logger.error);
            }
            else if (SteamIDs.Count < 2)
            {
                Logger.Log($"Found ModGroup with less than 2 members: [{ groupID }] { description }.", Logger.warning);
            }
        }
    }
}
