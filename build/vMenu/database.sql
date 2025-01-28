-- Outfit Code table
CREATE TABLE IF NOT EXISTS `vmenu_outfits` (
    `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
    `discord_id` varchar(50) DEFAULT NULL,
    `data` longtext DEFAULT NULL,
    `created` datetime DEFAULT current_timestamp(),
    `label` varchar(50) DEFAULT NULL,
    PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;