using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using TCAdmin.SDK.Objects;
using TCAdmin.SDK.Web.FileManager;
using TCAdmin.SDK.Web.MVC.Controllers;
using TCAdminBackup.Configuration;
using TCAdminBackup.Models;
using TCAdminBackup.Models.Objects;
using Server = TCAdmin.GameHosting.SDK.Objects.Server;
using Service = TCAdmin.GameHosting.SDK.Objects.Service;

namespace TCAdminBackup.Controllers
{
    [Authorize]
    public class BackupController : BaseServiceController
    {
        [ParentAction("Service", "Home")]
        [HttpPost]
        public async Task<ActionResult> BackupFile(int id, string file, BackupType backupType = BackupType.S3)
        {
            this.EnforceFeaturePermission("FileManager");
            if (string.IsNullOrEmpty(file))
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = "Please choose a file to backup."
                }, HttpStatusCode.BadRequest);
            }

            var realFileName = Path.GetFileName(file);

            if (realFileName.Any(Path.GetInvalidFileNameChars().Contains) ||
                !Regex.IsMatch(realFileName, @"^[\w\-.@ ]+$"))
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = "File contains invalid characters."
                }, HttpStatusCode.BadRequest);
            }

            var service = Service.GetSelectedService();
            var server = TCAdmin.GameHosting.SDK.Objects.Server.GetSelectedServer();
            var fileSystem = server.FileSystemService;
            var backupSolution = Backup.ParseBackupSolution(backupType, service);
            var filePath = Path.Combine(service.WorkingDirectory, file);

            if (Backup.DoesBackupExist(service, realFileName, backupType))
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = $"Backup already exists with name <strong>{realFileName}</strong>"
                }, HttpStatusCode.BadRequest);
            }

            var remoteDownload = new RemoteDownload(server)
            {
                DirectorySecurity = service.GetDirectorySecurityForCurrentUser(),
                FileName = filePath
            };

            var backupName = $"{realFileName}";
            var contents = GetFileContents(remoteDownload.GetDownloadUrl());

            try
            {
                await backupSolution.Backup(backupName, contents, MimeMapping.GetMimeMapping(realFileName));
                var fileServer = FileServerModel.DetermineFileServer(service, backupType);
                var backup = new Backup
                {
                    ServiceId = service.ServiceId,
                    FileServerId = fileServer.FileServerId,
                    FileName = backupName,
                    BackupType = backupType,
                };
                backup.CustomFields["SIZE"] = fileSystem.GetFileSize(filePath);
                backup.GenerateKey();
                backup.Save();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return new JsonHttpStatusResult(new
                {
                    responseText = "Failed to backup - " + e.Message + " | " + e.StackTrace
                }, HttpStatusCode.InternalServerError);
            }

            return Json(new
            {
                responseText = $"Backed up <strong>{backupName}</strong>"
            });
        }

        [ParentAction("Service", "Home")]
        public async Task<ActionResult> Restore(int id, string target, int backupId = 0)
        {
            this.EnforceFeaturePermission("FileManager");
            if (backupId == 0)
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = "No backup selected to restore."
                }, HttpStatusCode.InternalServerError);
            }

            var service = Service.GetSelectedService();
            var fileSystem = TCAdmin.SDK.Objects.Server.GetSelectedServer().FileSystemService;
            var backup = new Backup(backupId);
            var backupSolution = backup.BackupSolution;

            try
            {
                var saveTo = Path.Combine(service.WorkingDirectory, target, backup.FileName);

                if (backupSolution.AllowsDirectDownload)
                {
                    var downloadUrl = await backupSolution.DirectDownloadLink(backup.FileName);
                    fileSystem.DownloadFile(saveTo, downloadUrl);
                }
                else
                {
                    var bytes = await backupSolution.DownloadBytes(backup.FileName);
                    var memoryStream = new MemoryStream(bytes);
                    var byteBuffer = new byte[1024 * 1024 * 2];
                    memoryStream.Position = 0;
                    while (memoryStream.Read(byteBuffer, 0, byteBuffer.Length) > 0)
                    {
                        fileSystem.AppendFile(saveTo, byteBuffer);
                    }
                }

                return new JsonHttpStatusResult(new
                {
                    responseText = $"Restored <strong>{backup.FileName}</strong>"
                }, HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = "An error occurred: " + e.Message + " | " + e.StackTrace
                }, HttpStatusCode.InternalServerError);
            }
        }

        [ParentAction("Service", "Home")]
        public async Task<ActionResult> Delete(int id, int backupId = 0)
        {
            this.EnforceFeaturePermission("FileManager");
            if (backupId == 0)
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = "No backup selected to delete."
                }, HttpStatusCode.InternalServerError);
            }

            var backup = new Backup(backupId);
            var backupSolution = backup.BackupSolution;

            try
            {
                await backupSolution.Delete(backup.FileName);
                backup.Delete();
                return new JsonHttpStatusResult(new
                {
                    responseText = $"Deleted <strong>{backup.FileName}</strong>"
                }, HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = "An error occurred: " + e.Message
                }, HttpStatusCode.InternalServerError);
            }
        }

        [ParentAction("Service", "Home")]
        public ActionResult List(int id, BackupType backupType)
        {
            this.EnforceFeaturePermission("FileManager");
            var service = Service.GetSelectedService();

            var backups = Backup.GetBackupsForService(service, backupType).ToList();
            return Json(backups.Select(x => new
            {
                name = x.FileName,
                value = x.BackupId
            }), JsonRequestBehavior.AllowGet);
        }

        [ParentAction("Service", "Home")]
        public async Task<ActionResult> Download(int id, int backupId)
        {
            this.EnforceFeaturePermission("FileManager");
            var backup = new Backup(backupId);
            var backupSolution = backup.BackupSolution;

            if (backupSolution.AllowsDirectDownload)
            {
                var downloadUrl = await backupSolution.DirectDownloadLink(backup.FileName);
                return Redirect(downloadUrl);
            }

            var bytes = await backup.BackupSolution.DownloadBytes(backup.FileName);
            return File(bytes, System.Net.Mime.MediaTypeNames.Application.Octet, backup.FileName);
        }

        [ParentAction("Service", "Home")]
        public async Task<ActionResult> Capacity(int id, BackupType backupType)
        {
            this.EnforceFeaturePermission("FileManager");
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var service = Service.GetSelectedService();
            var backups = Backup.GetBackupsForService(service);
            backups.RemoveAll(x => x.BackupType != backupType);

            var value = backups.Sum(backup => backup.FileSize);
            var limit = service.Variables[$"{backupType.ToString()}:LIMIT"] != null
                ? long.Parse(service.Variables[$"{backupType.ToString().ToUpper()}:LIMIT"].ToString())
                : GlobalBackupSettings.Get().DefaultS3Capacity;

            if (limit == -1)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            return Json(new
            {
                limit,
                value
            }, JsonRequestBehavior.AllowGet);
        }

        [ParentAction("Service", "Home")]
        public static List<string> AccessibleSolutions(int id)
        {
            var settings = GlobalBackupSettings.Get();
            var accessibleSolutions = new List<string>();
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var service = Service.GetSelectedService();

            var s3Limit = service.Variables["S3:LIMIT"] != null
                ? long.Parse(service.Variables["S3:LIMIT"].ToString())
                : long.Parse(5_000_000_000.ToString());
            if (FileServer.GetFileServers().S3FileServers().Any() && s3Limit > 0 && settings.S3Enabled)
            {
                accessibleSolutions.Add("s3");
            }

            var ftpLimit = service.Variables["Ftp:LIMIT"] != null
                ? long.Parse(service.Variables["Ftp:LIMIT"].ToString())
                : long.Parse(5_000_000_000.ToString());
            if (FileServer.GetFileServers().FtpFileServers().Any() && ftpLimit > 0 && settings.FtpEnabled)
            {
                accessibleSolutions.Add("ftp");
            }

            if (settings.LocalEnabled)
            {
                accessibleSolutions.Add("local");
            }

            return accessibleSolutions;
        }

        private static byte[] GetFileContents(string downloadUrl)
        {
            using (var wc = new WebClient())
            {
                return wc.DownloadData(downloadUrl);
            }
        }
    }
}