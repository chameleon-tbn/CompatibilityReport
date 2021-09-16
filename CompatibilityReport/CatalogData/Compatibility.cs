using System;

namespace CompatibilityReport.CatalogData
{
    [Serializable] 
    public class Compatibility
    {
        // The mod names are only for catalog readability, they are not used anywhere else.
        public ulong FirstSteamID { get; private set; }
        public string FirstModName { get; private set; }

        public ulong SecondSteamID { get; private set; }
        public string SecondModName { get; private set; }

        // The compatibility status is from the perspective of the first mod.
        public Enums.CompatibilityStatus Status { get; private set; }
        public string Note { get; private set; }


        /// <summary>Default constructor for deserialization.</summary>
        private Compatibility()
        {
            // Nothing to do here.
        }


        /// <summary>Constructor for compatibility creation.</summary>
        public Compatibility(ulong firstModSteamID, string firstModName, ulong secondModSteamID, string secondModName, Enums.CompatibilityStatus status, string note)
        {
            FirstSteamID = firstModSteamID;
            FirstModName = firstModName ?? "";

            SecondSteamID = secondModSteamID;
            SecondModName = secondModName ?? "";

            Status = status;
            Note = note ?? "";
        }


        /// <summary>Updates mod names.</summary>
        /// <remarks>The mod names are only for catalog readability, they are not used anywhere else.</remarks>
        public void UpdateModNames(string firstModName, string secondModName)
        {
            FirstModName = firstModName ?? "";
            SecondModName = secondModName ?? "";
        }
    }
}
