using System;

namespace CompatibilityReport.Settings.ConfigData
{
    [Serializable]
    public class UpdaterConfig
    {
        public int SteamMaxFailedPages { get; set; } = 4;
        public int SteamMaxListingPages { get; set; } = 300;
        public int EstimatedMillisecondsPerModPage { get; set; } = 500;
        public int DaysOfInactivityToRetireAuthor { get; set; } = 365;
        public int WeeksForSoonRetired { get; set; } = 2;
    }
}
