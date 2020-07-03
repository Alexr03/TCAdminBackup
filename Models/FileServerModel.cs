using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TCAdmin.SDK.Objects;
using TCAdminBackup.Models.Objects;
using Service = TCAdmin.GameHosting.SDK.Objects.Service;

namespace TCAdminBackup.Models
{
    public class FileServerModel
    {
        public int S3FileServerId { get; set; } = -1;

        public int FtpFileServerId { get; set; } = -1;

        public static FileServer DetermineFileServer(Service service, BackupType backupType)
        {
            var s3FileServerIds = new List<int>();
            var ftpFileServerIds = new List<int>();
            Server server;
            Datacenter datacenter;
            if (TCAdmin.SDK.Utility.IsWebEnvironment())
            {
                service = Service.GetSelectedService();
                server = Server.GetSelectedServer();
                datacenter = Datacenter.GetSelectedDatacenter();
            }
            else
            {
                server = new Server(service.ServerId);
                datacenter = new Datacenter(server.DatacenterId);
            }

            if (service.Variables["BACKUP:FILESERVERMODEL"] != null)
            {
                var model = JsonConvert.DeserializeObject<FileServerModel>(service.Variables["BACKUP:FILESERVERMODEL"]
                    .ToString());

                s3FileServerIds.Add(model.S3FileServerId);
                ftpFileServerIds.Add(model.FtpFileServerId);
            }
            else if (!string.IsNullOrEmpty(server.CustomField15))
            {
                var model = JsonConvert.DeserializeObject<FileServerModel>(server.CustomField15);

                s3FileServerIds.Add(model.S3FileServerId);
                ftpFileServerIds.Add(model.FtpFileServerId);
            }
            else if (!string.IsNullOrEmpty(datacenter.CustomField15))
            {
                var model = JsonConvert.DeserializeObject<FileServerModel>(datacenter.CustomField15);

                s3FileServerIds.Add(model.S3FileServerId);
                ftpFileServerIds.Add(model.FtpFileServerId);
            }

            // Add absolutely last default servers.
            s3FileServerIds.AddRange(FileServer.GetFileServers().S3FileServers()
                .Select(s3FileServer => s3FileServer.FileServerId));
            
            ftpFileServerIds.AddRange(FileServer.GetFileServers().FtpFileServers()
                .Select(s3FileServer => s3FileServer.FileServerId));

            switch (backupType)
            {
                case BackupType.S3:
                    var s3ValidFileServerId = s3FileServerIds.FirstOrDefault(x => x != -1);
                    return new FileServer(s3ValidFileServerId);
                case BackupType.Ftp:
                    var ftpValidFileServerId = ftpFileServerIds.FirstOrDefault(x => x != -1);
                    return new FileServer(ftpValidFileServerId);
                case BackupType.Local:
                    var customFileServer = new FileServer();
                    customFileServer.FileServerId = -10;
                    return customFileServer;
            }

            return FileServer.GetSelectedFileServer();
        }
    }
}