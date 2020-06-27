using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace TCAdminBackup.Configuration
{
    public class GlobalBackupSettings
    {
        [Required(AllowEmptyStrings = true)]
        public bool S3Enabled { get; set; }
        
        [Required(AllowEmptyStrings = true)]
        public bool FtpEnabled { get; set; }
        
        [Required(AllowEmptyStrings = true)]
        public bool GoogleDriveEnabled { get; set; }
        
        [Required(AllowEmptyStrings = true)]
        public string GoogleApiKey { get; set; }
        
        [Required(AllowEmptyStrings = true)]
        public long DefaultS3Capacity { get; set; } = 5_000_000_000;

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
    }
}