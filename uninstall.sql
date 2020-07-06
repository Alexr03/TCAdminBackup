DROP TABLE tcmodule_backups;

DELETE FROM tc_info WHERE name LIKE 'Global.Backup.Settings';

DELETE FROM tc_site_map WHERE controller LIKE 'BackupAdmin';
DELETE FROM tc_site_map WHERE controller LIKE 'Backup';