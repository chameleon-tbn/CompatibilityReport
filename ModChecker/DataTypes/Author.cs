using System;
using ModChecker.Util;


// An author on Steam can be identified by a Steam ID, here called profile ID, and optionally (but often) a custom URL text
// Converting from custom URL to author profile number can be done manually on some websites or automated through Steam API


namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class Author
    {
        // Profile ID and custom URL as seen on the Steam Workshop
        public ulong ProfileID { get; private set; }
        public string CustomURL { get; private set; }

        // Author name
        public string Name { get; private set; }

        // Date the author was last seen / heard from
        public DateTime LastSeen { get; private set; }

        // Is the author retired; based on long absence or author announcement
        public bool Retired { get; private set; }

        // Change notes, automatically filled by the updater; not displayed in report or log, but visible in the catalog
        public string ChangeNotes { get; private set; }


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
                        string changeNotes = "")
        {
            ProfileID = profileID;

            CustomURL = customURL ?? "";

            Name = name ?? "";

            LastSeen = lastSeen;
            
            Retired = retired;

            ChangeNotes = changeNotes;

            // Debug messages
            if (ProfileID == 0 && string.IsNullOrEmpty(CustomURL))
            {
                Logger.Log($"Author created without profile ID and custom URL: { Name }", Logger.debug);
            }

            if (string.IsNullOrEmpty(name))
            {
                Logger.Log($"Author created with an empty name (profile ID { profileID } | custom URL: { customURL }).", Logger.debug);
            }
            else if (name == ProfileID.ToString())
            {
                Logger.Log($"Author created with profile ID as name ({ profileID }).", Logger.debug);
            }
        }


        // Update an author with new info; all fields are optional, only supplied fields are updated; profile ID can only be changed once, if it was zero
        internal void Update(ulong profileID = 0,
                             string customURL = null,
                             string name = null,
                             DateTime? lastSeen = null,
                             bool? retired = null,
                             string changeNotes = null)
        {
            // Only update supplied fields, so ignore every null value; make sure strings are set to empty strings instead of null

            // Update profile ID only if it was zero
            ProfileID = ProfileID == 0 ? profileID : ProfileID;

            CustomURL = customURL ?? CustomURL;

            // Avoid an empty name or the profile ID as name; Steam sometimes incorrectly puts the profile ID in the name field in mod listing HTML pages
            Name = string.IsNullOrEmpty(name) || name == ProfileID.ToString() ? Name : name;

            LastSeen = lastSeen ?? LastSeen;

            Retired = retired ?? Retired;

            // Add a change note (on a new line) instead of replacing it
            ChangeNotes += string.IsNullOrEmpty(changeNotes) ? "" : (string.IsNullOrEmpty(ChangeNotes) ? "" : "\n") + changeNotes;

            // Debug message
            if ((ProfileID == 0) && string.IsNullOrEmpty(CustomURL))
            {
                Logger.Log($"Updated author left without profile ID and custom URL: { Name }", Logger.debug);
            }

            if (name == ProfileID.ToString())
            {
                Logger.Log($"Author updated with profile ID as name ({ name }). This change was discarded, old name is still used.", Logger.debug);
            }
        }


        // Return a formatted string with the author profile or url, and the name
        internal new string ToString()
        {
            return $"[{ (ProfileID != 0 ? ProfileID.ToString() : CustomURL) }] { Name }";
        }
    }
}
