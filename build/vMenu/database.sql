-- Share code tables, used when vmenu_outfit_codes / vmenu_vehicle_codes / vmenu_loadout_codes are on.
-- Requires oxmysql.
--
-- Upgrading from an older vMenu-ox? CREATE TABLE IF NOT EXISTS leaves existing tables alone, so run these
-- once as well. They make `data` large enough for big loadouts and add the index the daily code limit uses.
--   ALTER TABLE `vmenu_outfits`  MODIFY `data` mediumtext DEFAULT NULL;
--   ALTER TABLE `vmenu_vehicles` MODIFY `data` mediumtext DEFAULT NULL;
--   ALTER TABLE `vmenu_loadouts` MODIFY `data` mediumtext DEFAULT NULL;
--   ALTER TABLE `vmenu_outfits`  ADD INDEX `idx_discord` (`discord_id`);
--   ALTER TABLE `vmenu_vehicles` ADD INDEX `idx_discord` (`discord_id`);
--   ALTER TABLE `vmenu_loadouts` ADD INDEX `idx_discord` (`discord_id`);
-- (An ADD INDEX that fails with "Duplicate key name 'idx_discord'" just means that table already has it.)
--
-- Codes are sequential ids and anyone who knows one can load it; that is by design, codes are meant to be shared.

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
