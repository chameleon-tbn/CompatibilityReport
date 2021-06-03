using System;

namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class UpdateExclusion
    {
        // Mod for which to make an exclusion
        public ulong SteamID { get; private set; }

        // Exclusion category
        public string Category { get; private set; }

        // Info about the exclusion, for the updater
        public string Info { get; private set; }


        // Default constructor
        public UpdateExclusion()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        internal UpdateExclusion(ulong steamID,
                                 string category,
                                 string info)
        {
            SteamID = steamID;

            Category = category;

            Info = info;
        }
    }
}
