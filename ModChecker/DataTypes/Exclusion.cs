using System;

namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class Exclusion
    {
        // Name of the exclusion, for logging and catalog maintenance
        public string Name { get; private set; }

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
        internal Exclusion(string name, ulong steamID, string category, ulong subItemSteamID = 0)
        {
            Name = name ?? "";

            SteamID = steamID;

            Category = category ?? "";

            SubItemSteamID = subItemSteamID;
        }
    }
}
