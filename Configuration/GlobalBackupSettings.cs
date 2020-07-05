using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using TCAdminBackup.Models.Objects;

namespace TCAdminBackup.Configuration
{
    public class GlobalBackupSettings
    {
        [Required(AllowEmptyStrings = true)]
        public bool S3Enabled { get; set; }
        
        [Required(AllowEmptyStrings = true)]
        public bool FtpEnabled { get; set; }
        
        [Required(AllowEmptyStrings = true)]
        public bool LocalEnabled { get; set; }

        [Required(AllowEmptyStrings = true)]
        public long DefaultS3Capacity { get; set; } = 5_000_000_000;
        
        [Required(AllowEmptyStrings = true)]
        public long DefaultFtpCapacity { get; set; } = 5_000_000_000;
        
        [Required(AllowEmptyStrings = true)]
        public long DefaultLocalCapacity { get; set; } = 5_000_000_000;
        
        [Required(AllowEmptyStrings = true)]
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