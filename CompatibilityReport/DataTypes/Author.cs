using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using CompatibilityReport.Util;


// An author on Steam can be identified by a Steam ID, here called profile ID, and optionally (but often) a custom URL text
// Converting from custom URL to author profile number can be done manually on some websites or automated through Steam API


namespace CompatibilityReport.DataTypes
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

        // Is the author retired. Based on mod updates or author announcement. No mod updates for over a year means retired
        public bool Retired { get; private set; }

        // Exclusion for retired, to allow setting an author to retired even with recent mod updates
        public bool ExclusionForRetired { get; private set; }

        // Change notes, automatically filled by the updater; not displayed in report or log, but visible in the catalog
        [XmlArrayItem("ChangeNote")] public List<string> ChangeNotes { get; private set; } = new List<string>();

        // Indicate if the author was updated by the ManualUpdater. Only used by the updater, does not appear in the catalog.
        [XmlIgnore] internal bool ManuallyUpdated { get; private set; }


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
                        List<string> changeNotes = null)
        {
            ProfileID = profileID;

            CustomURL = customURL ?? "";

            Name = name ?? "";

            LastSeen = lastSeen;
            
            Retired = retired;

            ExclusionForRetired = false;

            ChangeNotes = changeNotes ?? new List<string>();

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
                             bool? exclusionForRetired = null,
                             string extraChangeNote = null,
                             bool? manuallyUpdated = null)
        {
            // Only update supplied fields, so ignore every null value; make sure strings are set to empty strings instead of null

            // Update profile ID only if it was zero
            ProfileID = ProfileID == 0 ? profileID : ProfileID;

            CustomURL = customURL ?? CustomURL;

            // Avoid an empty name or the profile ID as name; Steam sometimes incorrectly puts the profile ID in the name field in mod listing HTML pages
            Name = string.IsNullOrEmpty(name) || name == ProfileID.ToString() ? Name : name;

            LastSeen = lastSeen ?? LastSeen;

            Retired = retired ?? Retired;

            ExclusionForRetired = exclusionForRetired ?? ExclusionForRetired;

            // Add a change note
            if (!string.IsNullOrEmpty(extraChangeNote))
            {
                ChangeNotes.Add(extraChangeNote);
            }

            ManuallyUpdated = manuallyUpdated ?? ManuallyUpdated;

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


        // Copy all fields, except 'ManuallyUpdated', from an author to a new author.
        internal static Author Copy(Author originalAuthor)
        {
            // Copy all value types directly
            return new Author(originalAuthor.ProfileID, originalAuthor.CustomURL, originalAuthor.Name, 
                originalAuthor.LastSeen, originalAuthor.Retired, originalAuthor.ChangeNotes);
        }
    }
}
