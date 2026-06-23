-- Outfit Code Table
CREATE TABLE IF NOT EXISTS `vmenu_outfits` (
    `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
    `discord_id` varchar(50) DEFAULT NULL,
    `data` mediumtext DEFAULT NULL,
    `created` datetime DEFAULT current_timestamp(),
    PRIMARY KEY (`id`) USING BTREE,
    KEY `idx_discord` (`discord_id`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

-- Vehicle Code Table
CREATE TABLE IF NOT EXISTS `vmenu_vehicles` (
    `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
    `discord_id` varchar(50) DEFAULT NULL,
    `data` mediumtext DEFAULT NULL,
    `created` datetime DEFAULT current_timestamp(),
    PRIMARY KEY (`id`) USING BTREE,
    KEY `idx_discord` (`discord_id`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;

-- Loadout Code Table
CREATE TABLE IF NOT EXISTS `vmenu_loadouts` (
    `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
    `discord_id` varchar(50) DEFAULT NULL,
    `data` mediumtext DEFAULT NULL,
    `created` datetime DEFAULT current_timestamp(),
    PRIMARY KEY (`id`) USING BTREE,
    KEY `idx_discord` (`discord_id`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
