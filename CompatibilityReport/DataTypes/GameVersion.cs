using System;
using CompatibilityReport.Util;


namespace CompatibilityReport.DataTypes
{
    internal static class GameVersion
    {
        // Current full and major game version
        internal static readonly Version Current;

        internal static readonly Version CurrentMajor;

        // Note about special versions, used in the log and report
        internal static readonly string SpecialNote;


        // Constructor
        static GameVersion()
        {
            // Get the game version
            Current = new Version(
                Convert.ToInt32(BuildConfig.APPLICATION_VERSION_A),
                Convert.ToInt32(BuildConfig.APPLICATION_VERSION_B),
                Convert.ToInt32(BuildConfig.APPLICATION_VERSION_C),
                Convert.ToInt32(BuildConfig.APPLICATION_BUILD_NUMBER));

            CurrentMajor = new Version(
                Convert.ToInt32(BuildConfig.APPLICATION_VERSION_A),
                Convert.ToInt32(BuildConfig.APPLICATION_VERSION_B));

            // 1.13.2-f1 is Epic only; consider this to be 1.13.1-f1 to not overcomplicate this mod
            if (Current == Patch_1_13_2_f1)
            {
                Current = Patch_1_13_1_f1;

                SpecialNote = $"Your game version is { Formatted(Patch_1_13_2_f1) }, which is an Epic Store only patch." + 
                    $"To avoid complexity this mod will treat it as version { Formatted(Current) }.";
            }
        }


        // Show game version in the commonly used format, as shown on the Main Menu and the Paradox Launcher
        internal static string Formatted(Version version)
        {
            try
            {
                // This will throw an exception on a short Version, like (0, 0)
                return $"{ version.ToString(3) }-f{ version.Revision }";
            }
            catch
            {
                return version.ToString();
            }            
        }


        // Unknown version; a null field written to the catalog comes back like this
        internal static readonly Version Unknown = new Version(0, 0);


        // 2015-03-10 Initial release
        internal static readonly Version Patch_1_0_5 = new Version(1, 0, 5, 0);
        internal static readonly Version Release = Patch_1_0_5;

        // 2015-03-21 Bugfixes
        internal static readonly Version Patch_1_0_6b = new Version(1, 0, 6, 2);

        // 2015-03-27 Asset editor fix
        internal static readonly Version Patch_1_0_7b = new Version(1, 0, 7, 2);

        // 2015-04-07 Asset editor fix
        internal static readonly Version Patch_1_0_7c = new Version(1, 0, 7, 3);

        
        // 2015-05-19 Tunnels and European themed buildings and maps added
        internal static readonly Version Patch_1_1_0b = new Version(1, 1, 0, 2);

        // 2015-07-01 IUserMod changed; any mod older than this is very broken
        internal static readonly Version Patch_1_1_1 = new Version(1, 1, 1, 0);

        
        // 2015-09-24 After Dark expansion
        // Day/night cycle, tourism/leisure, taxis, prisons, bicycle paths
        internal static readonly Version Patch_1_2_0 = new Version(1, 2, 0, 0);
        internal static readonly Version AfterDark = Patch_1_2_0;

        // 2015-10-01 Asset editor fix
        internal static readonly Version Patch_1_2_1_f1 = new Version(1, 2, 1, 1);

        // 2015-11-05 Several game limits increased
        internal static readonly Version Patch_1_2_2_f2 = new Version(1, 2, 2, 2);

        
        // 2016-02-18 Snowfall expansion
        // Winter biome, tram, road maintenance, heating
        internal static readonly Version Patch_1_3_0_f4 = new Version(1, 3, 0, 4);
        internal static readonly Version Snowfall = Patch_1_3_0_f4;

        // 2016-02-23 Bugfixes
        internal static readonly Version Patch_1_3_1_f1 = new Version(1, 3, 1, 1);

        // 2016-03-02 Rendering and pathfinding fix
        internal static readonly Version Patch_1_3_2_f1 = new Version(1, 3, 2, 1);

        
        // 2016-03-22 Landscaping tools update, rocks added, ruins added, map editor update
        internal static readonly Version Patch_1_4_0_f3 = new Version(1, 4, 0, 3);

        // 2016-04-19 Bugfixes
        internal static readonly Version Patch_1_4_1_f2 = new Version(1, 4, 1, 2);

        
        // 2016-06-09 Match Day free DLC
        internal static readonly Version Patch_1_5_0_f4 = new Version(1, 5, 0, 4);

        // 2016-09-01 Art Deco DLC
        internal static readonly Version Patch_1_5_1 = new Version(1, 5, 1, 0);

        // 2016-10-18 Stadiums DLC
        internal static readonly Version Patch_1_5_2 = new Version(1, 5, 2, 0);

        
        // 2016-11-29 Natural Disasters expansion & High-Tech Buildings DLC
        // Unity 5.4, natural disasters, disaster services & buildings, fires spread, helicopters, sub-buildings
        internal static readonly Version Patch_1_6_0_f4 = new Version(1, 6, 0, 4);
        internal static readonly Version NaturalDisasters = Patch_1_6_0_f4;

        // 2016-12-11 Service enumerators fixes in modding API
        internal static readonly Version Patch_1_6_1_f2 = new Version(1, 6, 1, 2);

        // 2016-12-21 Sub building fixes, asset editor pillar fixes
        internal static readonly Version Patch_1_6_2_f1 = new Version(1, 6, 2, 1);

        // 2017-01-26 Content manager asset sorting
        internal static readonly Version Patch_1_6_3_f1 = new Version(1, 6, 3, 1);

        
        // 2017-05-18 Mass Transit expansion
        // Unlimited soil/ore mods added, ferries, cable cars, blimps, monorail, road naming, citizen assets, 
        // emergency vehicle swerving, railway intercity toggle, stop signs, transport unbunching, ...
        internal static readonly Version Patch_1_7_0_f5 = new Version(1, 7, 0, 5);
        internal static readonly Version MassTransit = Patch_1_7_0_f5;

        // 2017-05-23 Bugfixes
        internal static readonly Version Patch_1_7_1_f1 = new Version(1, 7, 1, 1);

        // 2017-06-01 Bugfixes
        internal static readonly Version Patch_1_7_2_f1 = new Version(1, 7, 2, 1);

        
        // 2017-08-17 Concerts DLC; content manager overhaul
        internal static readonly Version Patch_1_8_0_f3 = new Version(1, 8, 0, 3);

        
        // 2017-10-19 Green Cities expansion & European Suburbia DLC
        // Unity 5.6.3, new commercial/residential/office specialisations, new service buildings, 
        // electric cars, noise pollution overhaul, moddable roads, train track intersection rules, ...
        internal static readonly Version Patch_1_9_0_f5 = new Version(1, 9, 0, 5);
        internal static readonly Version GreenCities = Patch_1_9_0_f5;

        // 2017-12-05 All That Jazz DLC & Carols, Candles and Candies DLC
        // Unity 5.6.4p2, dams and powerlines editable in asset editor
        internal static readonly Version Patch_1_9_1 = new Version(1, 9, 1, 0);

        // 2018-03-09 ChirpX free DLC & Mars Radio free DLC
        internal static readonly Version Patch_1_9_2_f1 = new Version(1, 9, 2, 1);

        // 2018-03-23 Bugfixes
        internal static readonly Version Patch_1_9_3_f1 = new Version(1, 9, 3, 1);

        
        // 2018-05-24 Park Life expansion
        // Trees reduce noise pollition, submesh modding, cinematic camera, menu filtering, 
        // new rocks/trees/etc, tourism info view, changes to some menus/info views
        internal static readonly Version Patch_1_10_0_f3 = new Version(1, 10, 0, 3);
        internal static readonly Version ParkLife = Patch_1_10_0_f3;

        // 2018-07-05 Bugfixes
        internal static readonly Version Patch_1_10_1_f3 = new Version(1, 10, 1, 3);

        
        // 2018-10-23 Industries expansion & Synthetic Dawn radio DLC
        // Industry areas for farming, forestry, oil and ore, post office service, warehouses and storage, 
        // unique factories, new industry vehicles, workers, Cargo airport and aircraft, auxilliary buildings, 
        // new animals, toll booths, new industry train wagons, new trees, dust/ore/sand debris markers, 
        // custom names, new light colors, new assets now have DLC requirements set, historic buildings
        internal static readonly Version Patch_1_11_0_f3 = new Version(1, 11, 0, 3);
        internal static readonly Version Industries = Patch_1_11_0_f3;

        // 2018-12-13 "Holiday Surprise Patch"; Winter Market asset added
        internal static readonly Version Patch_1_11_1_f2 = new Version(1, 11, 1, 2);

        // 2019-02-27 Remove duplicate Snowy Hills map
        internal static readonly Version Patch_1_11_1_f4 = new Version(1, 11, 1, 4);

        
        // 2019-05-21 Campus expansion & University City DLC & Campus radio DLC & Deep Focus radio DLC
        internal static readonly Version Patch_1_12_0_f5 = new Version(1, 12, 0, 5);
        internal static readonly Version Campus = Patch_1_12_0_f5;

        // 2019-06-04 Bugfixes; Steam used f1 for this version, but the game uses f2
        internal static readonly Version Patch_1_12_1_f2 = new Version(1, 12, 1, 2);
        internal static readonly Version Patch_1_12_1_f1 = Patch_1_12_1_f2;

        // 2019-11-07 Modern City Center & Downtown radio DLC
        internal static readonly Version Patch_1_12_2_f3 = new Version(1, 12, 2, 3);

        // 2020-01-22 Paradox Launcher
        internal static readonly Version Patch_1_12_3_f2 = new Version(1, 12, 3, 2);

        
        // 2020-03-26 Sunset Harbor expansion & Modern Japan DLC & Coast to Coast radio DLC
        // Fishing industry, intercity bus, trolleybus, passenger helicopter, waste transfer system, 
        // inland water treatment, aviation club, child/elder care, overground metro, tutorial log, 
        // pedestrian path snap to quays, walkable quays
        internal static readonly Version Patch_1_13_0_f7 = new Version(1, 13, 0, 7);
        internal static readonly Version SunsetHarbor = Patch_1_13_0_f7;

        // 2020-04-06 Bugfixes
        internal static readonly Version Patch_1_13_0_f8 = new Version(1, 13, 0, 8);

        // 2020-06-04 Bugfixes
        internal static readonly Version Patch_1_13_1_f1 = new Version(1, 13, 1, 1);
        
        // 2021-02-03 Bugfixes for the Epic Game Store edition; version is not available on other editions
        internal static readonly Version Patch_1_13_2_f1 = new Version(1, 13, 2, 1);

        // 2021-05-21 Bridges and Piers DLC & Train Stations DLC & Rail Hawk radio DLC & Sunny Breeze radio DLC
        internal static readonly Version Patch_1_13_3_f9 = new Version(1, 13, 3, 9);
    }
}
