using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using TCAdminBackup.Models.Objects;

namespace TCAdminBackup.Configuration
{
    public class GlobalBackupSettings
    {
        [Required(AllowEmptyStrings = true)]
        [Display(Description = "Enable S3 Backup Solution", Name = "S3 Backups")]
        public bool S3Enabled { get; set; }
        
        [Required(AllowEmptyStrings = true)]
        [Display(Description = "Enable FTP Backup Solution", Name = "FTP Backups")]
        public bool FtpEnabled { get; set; }
        
        [Required(AllowEmptyStrings = true)]
        [Display(Description = "Enable Local Backup Solution", Name = "Local Backups")]
        public bool LocalEnabled { get; set; }

        [Required(AllowEmptyStrings = true)]
        [Display(Description = "Default capacity size for users that use S3 Backups. This is measured in bytes", Name = "Default S3 Capacity")]
        public long DefaultS3Capacity { get; set; } = 5_000_000_000;
        
        [Required(AllowEmptyStrings = true)]
        [Display(Description = "Default capacity size for users that use FTP Backups. This is measured in bytes", Name = "Default FTP Capacity")]
        public long DefaultFtpCapacity { get; set; } = 5_000_000_000;
        
        [Required(AllowEmptyStrings = true)]
        [Display(Description = "Default capacity size for users that use Local Backups. This is measured in bytes", Name = "Default Local Capacity")]
        public long DefaultLocalCapacity { get; set; } = 5_000_000_000;
        
        [Required(AllowEmptyStrings = true)]
        [Display(Description = "Location for Local Backups.", Name = "Local Storage Location")]
        public string LocalDirectory { get; set; } = "$[Service.WorkingDirectory]/TCAdminBackups";

        public static GlobalBackupSettings Get()
        {
            var globalSettings = TCAdmin.SDK.Utility.GetDatabaseValue("Global.Backup.Settings");
            return !string.IsNullOrEmpty(globalSettings)
                ? JsonConvert.DeserializeObject<GlobalBackupSettings>(globalSettings)
                : new GlobalBackupSettings();
        }

        public static void Set(GlobalBackupSettings settings)
        {
            var json = JsonConvert.SerializeObject(settings);
            TCAdmin.SDK.Utility.SetDatabaseValue("Global.Backup.Settings", json);
        }

        public static long GetDefaultCapacity(BackupType backupType)
        {
            var globalSettings = Get();

            switch (backupType)
            {
                case BackupType.S3:
                    return globalSettings.DefaultS3Capacity;
                case BackupType.Ftp:
                    return globalSettings.DefaultFtpCapacity;
                case BackupType.Local:
                    return globalSettings.DefaultLocalCapacity;
            }

            return 0L;
        }
    }
}