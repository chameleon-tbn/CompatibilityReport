using System;


namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class Author
    {
        // Author ID, which can be either a profile ID (text) or a steam user number
        public string ID { get; private set; }

        public bool IDIsProfile { get; private set; }

        // Author name
        public string Name { get; private set; }

        // Date the author was last seen / heard from
        public DateTime LastSeen { get; private set; }

        // Is the author retired?
        public bool Retired { get; private set; }


        // Default constructor
        public Author()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        internal Author(string id,
                           bool idIsProfile,
                           string name,
                           DateTime lastSeen,
                           bool retired)
        {
            ID = id ?? "";

            IDIsProfile = idIsProfile;

            Name = string.IsNullOrEmpty(name) ? id : name ?? "";

            LastSeen = lastSeen;
            
            Retired = retired;
        }
    }
}
