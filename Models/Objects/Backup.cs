using System.Collections.Generic;
using System.Linq;
using TCAdmin.Interfaces.Database;
using TCAdmin.SDK.Objects;
using TCAdminBackup.BackupSolutions;
using Service = TCAdmin.GameHosting.SDK.Objects.Service;

namespace TCAdminBackup.Models.Objects
{
    public class Backup : ObjectBase
    {
        public Backup()
        {
            this.TableName = "tcmodule_backups";
            this.KeyColumns = new[] {"backupId"};
            this.SetValue("backupId", -1);
            this.UseApplicationDataField = true;
        }

        public Backup(int id) : this()
        {
            this.SetValue("backupId", id);
            this.ValidateKeys();
            if (!this.Find())
            {
                throw new KeyNotFoundException("Cannot find backup with ID: " + id);
            }
        }

        public int BackupId
        {
            get => this.GetIntegerValue("backupId");
            set => this.SetValue("backupId", value);
        }
        
        public int ServiceId
        {
            get => this.GetIntegerValue("serviceId");
            set => this.SetValue("serviceId", value);
        }

        public int FileServerId
        {
            get => this.GetIntegerValue("fileServerId");
            set => this.SetValue("fileServerId", value);
        }

        public string FileName
        {
            get => this.GetStringValue("fileName");
            set => this.SetValue("fileName", value);
        }
        
        public long FileSize
        {
            get => long.Parse(this.CustomFields["SIZE"].ToString());
            set => this.CustomFields["SIZE"] = value;
        }
        
        public BackupType BackupType
        {
            get => (BackupType)this.GetIntegerValue("backupType");
            set => this.SetValue("backupType", (int)value);
        }

        public BackupSolution BackupSolution => ParseBackupSolution(this.BackupType, new Service(this.ServiceId));

        public static List<Backup> GetBackupsForService(Service service)
        {
            var whereList = new WhereList
            {
                {"serviceId", service.ServiceId}
            };
            return new Backup().GetObjectList(whereList).Cast<Backup>().ToList();
        }
        
        public static List<Backup> GetBackupsForService(Service service, BackupType backupType)
        {
            var whereList = new WhereList
            {
                {"serviceId", service.ServiceId},
                {"backupType", (int)backupType}
            };
            return new Backup().GetObjectList(whereList).Cast<Backup>().ToList();
        }
        
        public static bool DoesBackupExist(Service service, string fileName, BackupType backupType)
        {
            var whereList = new WhereList
            {
                {"serviceId", service.ServiceId},
                {"fileName", fileName},
                {"backupType", (int)backupType}
            };
            return new Backup().GetObjectList(whereList).Cast<Backup>().ToList().Any();
        }

        public static BackupSolution ParseBackupSolution(BackupType type, Service service)
        {
            var user = new User(service.UserId);
            switch (type)
            {
                case BackupType.S3:
                    return new S3Backup(service, user.CloudBackupsBucketName());
                case BackupType.Ftp:
                    return new FtpBackup(service, user.CloudBackupsBucketName());
                case BackupType.Local:
                    return new LocalBackup(service);
                default:
                    return null;
            }
        }
    }

    public enum BackupType
    {
        S3,
        Ftp,
        Local
    }
}