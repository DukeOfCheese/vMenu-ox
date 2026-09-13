-- Teleport locations and map blips.
-- Blips are pushed to clients by server/addons.lua; teleports here are the authored set and are
-- merged at runtime with any locations players save in-game (those live in server KVP, not this file).
Config = Config or {}

Config.Locations = {
    teleports = {
        { name = "Example Location", x = 472.94, y = -3035.96, z = 6.2, heading = 356.1 },
    },

    blips = {
        -- Police Station
        { name = "Police Station", sprite = 526, color = 0, x = 442.18, y = -983.14, z = 30.1 },
        { name = "Police Station", sprite = 526, color = 0, x = -1094.83, y = -836.18, z = 38.06 },
        { name = "Police Station", sprite = 526, color = 0, x = 825.62, y = -1290.11, z = 28.24 },
        { name = "Police Station", sprite = 526, color = 0, x = -560.62, y = -133.38, z = 38.08 },
        { name = "Police Station", sprite = 526, color = 0, x = 1853.82, y = 3686.43, z = 34.27 },
        { name = "Police Station", sprite = 526, color = 0, x = -445.8, y = 6014.2, z = 31.71 },
        -- Ammu-Nation With Range
        { name = "Ammu-Nation With Range", sprite = 313, color = 0, x = 21.13, y = -1108.05, z = 29.79 },
        { name = "Ammu-Nation With Range", sprite = 313, color = 0, x = 812.37, y = -2157.64, z = 29.61 },
        -- Ammu-Nation
        { name = "Ammu-Nation", sprite = 110, color = 0, x = -663.57, y = -939.91, z = 21.82 },
        { name = "Ammu-Nation", sprite = 110, color = 0, x = 2569.44, y = 300.06, z = 108.73 },
        { name = "Ammu-Nation", sprite = 110, color = 0, x = -1310.64, y = -392.84, z = 36.69 },
        { name = "Ammu-Nation", sprite = 110, color = 0, x = 248.45, y = -47.13, z = 69.94 },
        { name = "Ammu-Nation", sprite = 110, color = 0, x = -1115.16, y = 2695.98, z = 18.55 },
        { name = "Ammu-Nation", sprite = 110, color = 0, x = 1696.98, y = 3754.87, z = 34.7 },
        { name = "Ammu-Nation", sprite = 110, color = 0, x = -327.41, y = 6080.03, z = 31.45 },
        { name = "Ammu-Nation", sprite = 110, color = 0, x = -3167.55, y = 1084.48, z = 20.83 },
        { name = "Ammu-Nation", sprite = 110, color = 0, x = 843.89, y = -1027.62, z = 28.19 },
        -- Golf Club
        { name = "Golf Club", sprite = 109, color = 0, x = -1369.88, y = 57.38, z = 53.7 },
        -- Fort Zancudo
        { name = "Fort Zancudo", sprite = 419, color = 0, x = -2086.37, y = 3080.96, z = 32.81 },
        -- Horse Race Track
        { name = "Horse Race Track", sprite = 309, color = 0, x = 1151.84, y = 119.37, z = 81.87 },
        -- Prison
        { name = "Prison", sprite = 58, color = 0, x = 1693.35, y = 2605.47, z = 45.56 },
        -- Cave
        { name = "Cave", sprite = 162, color = 0, x = 3109.63, y = 2192.22, z = 8.59 },
        -- N.O.O.S.E Facility
        { name = "N.O.O.S.E Facility", sprite = 60, color = 0, x = 2506, y = -382.86, z = 94.12 },
        -- Vehicle Impound
        { name = "Vehicle Impound", sprite = 225, color = 0, x = 395.18, y = -1615.73, z = 29.29 },
        -- Hospital
        { name = "Hospital", sprite = 61, color = 0, x = 306.58, y = -1435.32, z = 29.8 },
        { name = "Hospital", sprite = 61, color = 0, x = 305, y = -583, z = 50 },
        { name = "Hospital", sprite = 61, color = 0, x = -463.67, y = -338.42, z = 34.5 },
        { name = "Hospital", sprite = 61, color = 0, x = 1828.13, y = 3667.26, z = 34.28 },
        -- Clothes Store
        { name = "Clothes Store", sprite = 73, color = 0, x = 1689.29, y = 4820.49, z = 42.06 },
        { name = "Clothes Store", sprite = 73, color = 0, x = 1199, y = 2706.09, z = 38.22 },
        { name = "Clothes Store", sprite = 73, color = 0, x = 78.62, y = -1391.16, z = 29.37 },
        { name = "Clothes Store", sprite = 73, color = 0, x = 422.37, y = -807.84, z = 29.49 },
        { name = "Clothes Store", sprite = 73, color = 0, x = -1196.41, y = -774.02, z = 17.32 },
        { name = "Clothes Store", sprite = 73, color = 0, x = 0.61, y = 6514.44, z = 31.87 },
        { name = "Clothes Store", sprite = 73, color = 0, x = 124.54, y = -218.71, z = 54.55 },
        { name = "Clothes Store", sprite = 73, color = 0, x = -1097.86, y = 2708.82, z = 19.11 },
        { name = "Clothes Store", sprite = 73, color = 0, x = -3170.29, y = 1051.89, z = 20.86 },
        { name = "Clothes Store", sprite = 73, color = 0, x = -818.76, y = -1076.82, z = 11.33 },
        { name = "Clothes Store", sprite = 73, color = 0, x = -162.18, y = -302.3, z = 39.71 },
        { name = "Clothes Store", sprite = 73, color = 0, x = -712.28, y = -154.21, z = 37.41 },
        { name = "Clothes Store", sprite = 73, color = 0, x = 617.84, y = 2756.28, z = 42.09 },
        -- Mask Store
        { name = "Mask Store", sprite = 362, color = 0, x = -1336.3, y = -1277.71, z = 4.87 },
        -- Tattoo Parlor
        { name = "Tattoo Parlor", sprite = 75, color = 0, x = -3169.42, y = 1074.76, z = 20.83 },
        { name = "Tattoo Parlor", sprite = 75, color = 0, x = 1862.07, y = 3749.95, z = 33.03 },
        { name = "Tattoo Parlor", sprite = 75, color = 0, x = 322.11, y = 180.81, z = 103.59 },
        { name = "Tattoo Parlor", sprite = 75, color = 0, x = -1153.31, y = -1425.48, z = 4.95 },
        -- Barber
        { name = "Barber", sprite = 71, color = 0, x = 1212.17, y = -472.87, z = 66.21 },
        { name = "Barber", sprite = 71, color = 0, x = 1933.21, y = 3727.99, z = 32.84 },
        { name = "Barber", sprite = 71, color = 0, x = 134.77, y = -1710.31, z = 29.29 },
        -- Store
        { name = "Store", sprite = 52, color = 0, x = 1159.44, y = -323.77, z = 69.21 },
        { name = "Store", sprite = 52, color = 0, x = 1166.78, y = 2708.1, z = 38.15 },
        { name = "Store", sprite = 52, color = 0, x = 544.5, y = 2669.01, z = 42.15 },
        { name = "Store", sprite = 52, color = 0, x = 1393.15, y = 3602, z = 34.98 },
        { name = "Store", sprite = 52, color = 0, x = -1225.3, y = -905.1, z = 12.33 },
        { name = "Store", sprite = 52, color = 0, x = -3041.25, y = 589.09, z = 7.91 },
        { name = "Store", sprite = 52, color = 0, x = -2970.96, y = 309.88, z = 15.04 },
        { name = "Store", sprite = 52, color = 0, x = 1701.72, y = 4927.75, z = 42.06 },
        { name = "Store", sprite = 52, color = 0, x = -50.7, y = -1755.71, z = 29.42 },
        { name = "Store", sprite = 52, color = 0, x = -3241.03, y = 1004.56, z = 12.83 },
        { name = "Store", sprite = 52, color = 0, x = 2680.96, y = 3282.95, z = 55.24 },
        { name = "Store", sprite = 52, color = 0, x = 1964.67, y = 3741.8, z = 32.34 },
        { name = "Store", sprite = 52, color = 0, x = -1488.68, y = -381.17, z = 40.16 },
        { name = "Store", sprite = 52, color = 0, x = 377, y = 324.88, z = 103.57 },
        { name = "Store", sprite = 52, color = 0, x = 2558.06, y = 385.81, z = 108.62 },
        { name = "Store", sprite = 52, color = 0, x = 29.04, y = -1347.47, z = 29.5 },
        -- Benny's
        { name = "Benny's", sprite = 446, color = 28, x = -211.6, y = -1323.28, z = 30.8 },
        -- Los Santos Customs
        { name = "Los Santos Customs", sprite = 72, color = 0, x = -352.94, y = -135.84, z = 39 },
        { name = "Los Santos Customs", sprite = 72, color = 0, x = 729.16, y = -1088.22, z = 22.17 },
        { name = "Los Santos Customs", sprite = 72, color = 0, x = -1148.52, y = -1994.11, z = 13.18 },
        { name = "Los Santos Customs", sprite = 72, color = 0, x = 1179.05, y = 2644, z = 37.79 },
        { name = "Los Santos Customs", sprite = 72, color = 0, x = 110.7, y = 6625.64, z = 31.78 },
        -- Helicopter Pad
        { name = "Helicopter Pad", sprite = 360, color = 0, x = -735.56, y = -1456.48, z = 5 },
        -- Harbor
        { name = "Harbor", sprite = 356, color = 0, x = -854.76, y = -1424.97, z = 5 },
        -- Del Perro Pier
        { name = "Del Perro Pier", sprite = 266, color = 0, x = -1843.59, y = -1219.52, z = 12.81 },
        -- Strip Club
        { name = "Strip Club", sprite = 121, color = 0, x = 126.55, y = -1289.25, z = 29.28 },
        -- Tequi-la-la
        { name = "Tequi-la-la", sprite = 93, color = 0, x = -555.16, y = 284.97, z = 82.17 },
        -- Safehouse
        { name = "Safehouse", sprite = 40, color = 0, x = -818.05, y = 177.76, z = 72.22 },
        { name = "Safehouse", sprite = 40, color = 0, x = 1985.481, y = 3828.768, z = 32.5 },
        { name = "Safehouse", sprite = 40, color = 0, x = -1117.163, y = 303.0907, z = 66.52217 },
        { name = "Safehouse", sprite = 40, color = 0, x = 1395.17, y = 1141.81, z = 114.63 },
        { name = "Safehouse", sprite = 40, color = 0, x = 5.09, y = 532.59, z = 175.34 },
        { name = "Safehouse", sprite = 40, color = 0, x = -14.69, y = -1439.27, z = 31.1 },
        { name = "Safehouse", sprite = 40, color = 0, x = -3086.428, y = 339.2523, z = 6.3717 },
        -- Redwood Lights Race Track
        { name = "Redwood Lights Race Track", sprite = 127, color = 0, x = 1022.5, y = 2408.15, z = 55.22 },
        -- Simeon's Showroom
        { name = "Simeon's Showroom", sprite = 369, color = 0, x = -47.1617, y = -1115.333, z = 26.5 },
        -- Jewel Store
        { name = "Jewel Store", sprite = 439, color = 0, x = -637.2016, y = -239.1625, z = 38.1 },
        -- Union Depository Heist
        { name = "Union Depository Heist", sprite = 107, color = 0, x = 2.696893, y = -667.0166, z = 16.13063 },
        -- Morgue
        { name = "Morgue", sprite = 162, color = 0, x = 239.752, y = -1360.65, z = 39.53437 },
        -- Cluckin Bell
        { name = "Cluckin Bell", sprite = 162, color = 0, x = -146.3837, y = 6161.5, z = 30.2062 },
        -- O'Neil Brothers
        { name = "O'Neil Brothers", sprite = 140, color = 0, x = 2447.9, y = 4973.4, z = 47.7 },
        -- FIB Building
        { name = "FIB Building", sprite = 475, color = 0, x = 105.4557, y = -745.4835, z = 44.7548 },
        -- Lester's Factory
        { name = "Lester's Factory", sprite = 473, color = 0, x = 716.84, y = -962.05, z = 31.59 },
        -- Life Invader
        { name = "Life Invader", sprite = 475, color = 0, x = -1047.9, y = -233, z = 39 },
        -- Carwash
        { name = "Carwash", sprite = 100, color = 0, x = 55.7, y = -1391.3, z = 30.5 },
        { name = "Carwash", sprite = 100, color = 0, x = -700.01, y = -933.06, z = 18.48 },
        -- The Lost
        { name = "The Lost", sprite = 226, color = 0, x = 49.49379, y = 3744.472, z = 46.38629 },
        { name = "The Lost", sprite = 226, color = 0, x = 984.1552, y = -95.3662, z = 74.5 },
        -- Heist Aircraft Carrier
        { name = "Heist Aircraft Carrier", sprite = 455, color = 0, x = 3082.312, y = -4717.119, z = 15.2622 },
        -- Heist Yacht
        { name = "Heist Yacht", sprite = 455, color = 0, x = -2043.974, y = -1031.582, z = 11.981 },
        -- Bunker
        { name = "Bunker", sprite = 473, color = 0, x = 848, y = 3004, z = 44 },
        { name = "Bunker", sprite = 473, color = 0, x = 2118, y = 3330, z = 46 },
        { name = "Bunker", sprite = 473, color = 0, x = 2491, y = 3150, z = 50 },
        { name = "Bunker", sprite = 473, color = 0, x = 486, y = 3005, z = 42 },
        { name = "Bunker", sprite = 473, color = 0, x = -391, y = 4354, z = 57 },
        { name = "Bunker", sprite = 473, color = 0, x = 1815, y = 4706, z = 41 },
        { name = "Bunker", sprite = 473, color = 0, x = 1571, y = 2241, z = 79 },
        { name = "Bunker", sprite = 473, color = 0, x = -772, y = 5938, z = 23 },
        { name = "Bunker", sprite = 473, color = 0, x = 31, y = 2945, z = 58 },
        { name = "Bunker", sprite = 473, color = 0, x = -3046, y = 3331, z = 11 },
        { name = "Bunker", sprite = 473, color = 0, x = -3171, y = 1375, z = 18 },
        -- Bahama Mamas
        { name = "Bahama Mamas", sprite = 93, color = 0, x = -1388.001, y = -618.4197, z = 30.8196 },
        -- Aircraft Hangar
        { name = "Aircraft Hangar", sprite = 359, color = 0, x = -1152.17, y = -3410.83, z = 13.95 },
        { name = "Aircraft Hangar", sprite = 359, color = 0, x = -1395.4, y = -3266.66, z = 13.95 },
        { name = "Aircraft Hangar", sprite = 359, color = 0, x = -2470.22, y = 3274.79, z = 32.83 },
        { name = "Aircraft Hangar", sprite = 359, color = 0, x = -2021.5, y = 3157.4, z = 32.81 },
        { name = "Aircraft Hangar", sprite = 359, color = 0, x = -1878.01, y = 3108.85, z = 32.81 },
    },
}

