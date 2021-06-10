using System;
using ModChecker.Util;


// An author on Steam can be identified by a Steam ID or, here called profile ID, and optionally (but often) a custom URL text
// Converting from custom URL to author profile number can be done manually on some websites or automated through Steam API


namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class Author
    {
        // Author ID (as string) and custom URL as seen on the Steam Workshop
        public ulong ProfileID { get; private set; }
        public string CustomURL { get; private set; }

        // Author name
        public string Name { get; private set; }

        // Date the author was last seen / heard from
        public DateTime LastSeen { get; private set; }

        // Is the author retired?
        public bool Retired { get; private set; }

        // Remark for ourselves, not displayed in report or log (but publicly viewable in the catalog)
        public string CatalogRemark { get; private set; }


        // Default constructor
        public Author()
        {
            // Nothing to do here
        }


        // Constructor with 3 to all parameters
        internal Author(ulong profileID,
                        string customURL,
                        string name,
                        DateTime lastSeen = default,
                        bool retired = false,
                        string catalogRemark = "")
        {
            ProfileID = profileID;

            CustomURL = customURL ?? "";

            // If name is empty, use the profile ID or custom url text
            Name = !string.IsNullOrEmpty(name) ? name : ProfileID != 0 ? ProfileID.ToString() : CustomURL ?? "";

            LastSeen = lastSeen;
            
            Retired = retired;

            CatalogRemark = catalogRemark;

            if ((ProfileID == 0) && string.IsNullOrEmpty(CustomURL))
            {
                Logger.Log($"Author created with both empty profile and custom URL: { Name }", Logger.debug);
            }
        }


        // Update an author with new info; all fields are optional, only supplied fields are updated
        internal void Update(ulong profileID = 0,
                             string customURL = null,
                             string name = null,
                             DateTime? lastSeen = null,
                             bool? retired = null,
                             string catalogRemark = null)
        {
            ProfileID = profileID == 0 ? ProfileID : profileID;

            CustomURL = customURL ?? CustomURL;

            Name = string.IsNullOrEmpty(name) ? Name : name;

            LastSeen = lastSeen ?? LastSeen;

            Retired = retired ?? Retired;

            // Add a new catalog remark (on a new line) instead of replacing it
            CatalogRemark += string.IsNullOrEmpty(catalogRemark) ? "" : (string.IsNullOrEmpty(CatalogRemark) ? "" : "\n") + catalogRemark;

            if ((ProfileID == 0) && string.IsNullOrEmpty(CustomURL))
            {
                Logger.Log($"Author updated with both empty profile ID and custom URL: { Name }", Logger.debug);
            }
        }
    }
}
