using System;

namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class Exclusion
    {
        // Mod for which to make an exclusion
        public ulong SteamID { get; private set; }

        // Exclusion category
        public string Category { get; private set; }

        // Steam ID for a subitem for which this exclusion is, if any
        public ulong SubItemSteamID { get; private set; }


        // Default constructor
        public Exclusion()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        internal Exclusion(ulong steamID, string category, ulong subItemSteamID = 0)
        {
            SteamID = steamID;

            Category = category ?? "";

            SubItemSteamID = subItemSteamID;
        }
    }
}
