using System;
using System.IO;
using System.Threading.Tasks;
using TCAdmin.SDK.Objects;
using TCAdmin.SDK.Web.FileManager;
using TCAdmin.SDK.Web.References.FileSystem;
using TCAdminBackup.Configuration;
using Service = TCAdmin.GameHosting.SDK.Objects.Service;

namespace TCAdminBackup.BackupSolutions
{
    public class LocalBackup : BackupSolution
    {
        public readonly GlobalBackupSettings GlobalBackupSettings = GlobalBackupSettings.Get();
        private readonly FileSystem _fileSystemService;

        public LocalBackup()
        {
            this.AllowsDirectDownload = true;
        }

        public LocalBackup(Service service)
        {
            this.AllowsDirectDownload = true;
            this.Service = service;
            _fileSystemService = new Server(this.Service.ServerId).FileSystemService;
        }

        public override Task<bool> Backup(string fileName, byte[] contents, string contentType)
        {
            var saveTo = Path.Combine(GlobalBackupSettings.LocalDirectory.ReplaceVariables(Service), fileName);
            if (!_fileSystemService.DirectoryExists(Path.GetDirectoryName(saveTo)))
            {
                _fileSystemService.CreateDirectory(Path.GetDirectoryName(saveTo));
            }
            
            var memoryStream = new MemoryStream(contents);
            var byteBuffer = new byte[1024 * 1024 * 2];
            memoryStream.Position = 0;
            while (memoryStream.Read(byteBuffer, 0, byteBuffer.Length) > 0)
            {
                _fileSystemService.AppendFile(saveTo, byteBuffer);
            }

            return Task.FromResult(true);
        }

        public override Task<byte[]> DownloadBytes(string fileName)
        {
            throw new NotImplementedException();
        }

        public override Task<string> DirectDownloadLink(string fileName)
        {
            var saveTo = Path.Combine(GlobalBackupSettings.LocalDirectory.ReplaceVariables(Service), fileName);

            var remoteDownload = new RemoteDownload(new Server(this.Service.ServerId))
            {
                DirectorySecurity = this.Service.GetDirectorySecurityForCurrentUser(),
                FileName = saveTo
            };

            return Task.FromResult(remoteDownload.GetDownloadUrl());
        }

        public override Task<bool> Delete(string fileName)
        {
            var saveTo = Path.Combine(GlobalBackupSettings.LocalDirectory.ReplaceVariables(Service), fileName);
            _fileSystemService.DeleteFile(saveTo);
            
            return Task.FromResult(true);
        }
    }
}