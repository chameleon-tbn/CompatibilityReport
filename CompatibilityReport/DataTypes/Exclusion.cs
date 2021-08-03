using System;

namespace CompatibilityReport.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class Exclusion
    {
        // Mod for which to make an exclusion
        public ulong SteamID { get; private set; }

        // Exclusion category
        public Enums.ExclusionCategory Category { get; private set; }

        // Subitem for which this exclusion is, if any; mod Steam ID, DLC appid (number corresponding to DLC enum), or status number corresponding to enum
        public ulong SubItem { get; private set; }


        // Default constructor
        public Exclusion()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        internal Exclusion(ulong steamID, Enums.ExclusionCategory category, ulong subItem = 0)
        {
            SteamID = steamID;

            Category = category;

            SubItem = subItem;
        }
    }
}
