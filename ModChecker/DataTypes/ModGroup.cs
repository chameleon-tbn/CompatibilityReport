using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using ModChecker.Util;


// Mod groups are only used for Required Mods in the Mod class, in a way that one of the mods from a group is a requirement (not all together)
// NOTE: A mod can only be a member of one group, and that group is automatically used instead of a required mod when found by the updater


// N O T E !!!! - The updater will replace required mods with the group they're a member of. Make sure this is always appropriate! (or use an Update-Exclusion)


namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class ModGroup
    {
        // Group ID, which is used instead of a Steam ID in a required mods list
        public ulong GroupID { get; private set; }

        // A name for this group, for catalog maintenance and logging only; not shown in reports
        public string Name { get; private set; }

        // Steam IDs of mods in this group; nesting group IDs is not supported
        [XmlArrayItem("SteamID")] public List<ulong> SteamIDs { get; private set; } = new List<ulong>();


        // Default constructor
        public ModGroup()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        internal ModGroup(ulong groupID, string name, List<ulong> steamIDs)
        {
            GroupID = groupID;

            Name = name ?? "";

            SteamIDs = steamIDs ?? new List<ulong>();

            if ((GroupID < ModSettings.lowestModGroupID) || (GroupID > ModSettings.highestModGroupID))
            {
                Logger.Log($"ModGroup ID out range: { this.ToString() }. This might give weird results in the report.", Logger.error);
            }

            if (SteamIDs.Count < 2)
            {
                Logger.Log($"Found ModGroup with less than 2 members: { this.ToString() }.", Logger.warning);
            }
        }


        // Return a formatted string with the group ID and name
        internal new string ToString()
        {
            return $"[Group { GroupID }] { Name }";
        }


        // Copy all fields from a mod group to a new mod group
        internal static ModGroup Copy(ModGroup originalGroup)
        {
            // Copy the value types directly, and the list as a new list
            return new ModGroup(originalGroup.GroupID, originalGroup.Name, new List<ulong>(originalGroup.SteamIDs));
        }

    }
}
