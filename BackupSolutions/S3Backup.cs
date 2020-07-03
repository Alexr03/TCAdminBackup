using System;
using System.IO;
using System.Threading.Tasks;
using Minio;
using TCAdmin.SDK.Objects;
using TCAdminBackup.Models;
using TCAdminBackup.Models.Objects;
using Service = TCAdmin.GameHosting.SDK.Objects.Service;

namespace TCAdminBackup.BackupSolutions
{
    public class S3Backup : BackupSolution
    {
        public readonly MinioClient MinioClient;
        private readonly string _bucketName;
        private readonly string _region;

        public S3Backup(FileServer fileServer, string bucketName)
        {
            var host = fileServer.FtpIpAddress;
            var username = fileServer.FtpUser;
            var password = fileServer.FtpPassword;
            _region = fileServer.CustomFields["S3:REGION"] != null
                ? fileServer.CustomFields["S3:REGION"].ToString()
                : "us-east-1";

            MinioClient = new MinioClient(host, username, password, _region).WithSSL();
            MinioClient.SetTraceOn();
            _bucketName = bucketName;
            this.AllowsDirectDownload = true;
        }

        public S3Backup(Service service, string bucketName) : this(
            FileServerModel.DetermineFileServer(service, BackupType.S3), bucketName)
        {
        }

        public S3Backup(Service service) : this(FileServerModel.DetermineFileServer(service, BackupType.S3),
            TCAdmin.SDK.Session.GetCurrentUser().CloudBackupsBucketName())
        {
        }
        
        public override async Task<bool> Backup(string fileName, byte[] contents, string contentType)
        {
            if (!await MinioClient.BucketExistsAsync(_bucketName))
            {
                await MinioClient.MakeBucketAsync(_bucketName, _region);
            }

            Stream stream = new MemoryStream(contents);
            await MinioClient.PutObjectAsync(_bucketName, fileName, stream, stream.Length, contentType);

            return true;
        }

        public override Task<byte[]> DownloadBytes(string fileName)
        {
            throw new NotImplementedException();
        }

        public override async Task<string> DirectDownloadLink(string fileName)
        {
            if (!await MinioClient.BucketExistsAsync(_bucketName))
            {
                throw new Exception("No bucket exists.");
            }

            var downloadUrl = await MinioClient.PresignedGetObjectAsync(_bucketName, fileName, 300);
            return downloadUrl;
        }

        public override async Task<bool> Delete(string fileName)
        {
            if (!await MinioClient.BucketExistsAsync(_bucketName))
            {
                throw new Exception("No bucket exists.");
            }

            await MinioClient.RemoveObjectAsync(_bucketName, fileName);
            return true;
        }
    }
}