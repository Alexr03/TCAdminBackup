using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using TCAdmin.Web.MVC;
using TCAdminBackup.Configuration;
using TCAdminBackup.Models;
using TCAdminBackup.Models.Objects;
using Service = TCAdmin.GameHosting.SDK.Objects.Service;

namespace TCAdminBackup.Controllers
{
    [ExceptionHandler]
    [Authorize]
    public class BackupController : BaseServiceController
    {
        [ParentAction("Service", "Home")]
        [HttpPost]
        public async Task<ActionResult> BackupFile(int id, string file, BackupType backupType = BackupType.S3)
        {
            var service = Service.GetSelectedService();
            var server = TCAdmin.GameHosting.SDK.Objects.Server.GetSelectedServer();
            var dirsec = service.GetDirectorySecurityForCurrentUser();
            var vdir = new TCAdmin.SDK.VirtualFileSystem.VirtualDirectory(server.OperatingSystem, dirsec);

            this.EnforceFeaturePermission("FileManager");
            if (string.IsNullOrEmpty(file))
            {
                return new JsonHttpStatusResult(new
                {
                    Message = "Please choose a file to backup."
                }, HttpStatusCode.BadRequest);
            }

            var realFileName = Path.GetFileName(file);

            if (realFileName.Any(Path.GetInvalidFileNameChars().Contains) ||
                !Regex.IsMatch(realFileName, @"^[\w\-.@ ]+$"))
            {
                return new JsonHttpStatusResult(new
                {
                    Message = "File contains invalid characters."
                }, HttpStatusCode.BadRequest);
            }


            var fileSystem = server.FileSystemService;
            var backupSolution = Backup.ParseBackupSolution(backupType, service);
            var filePath = vdir.CombineWithPhysicalPath(file);
            var fileSize = fileSystem.GetFileSize(filePath);
            if (GetBackupsSize(service, backupType) + fileSize > GetBackupsLimit(service, backupType))
            {
                return new JsonHttpStatusResult(new
                {
                    Message = "Backing up this file will exceed your assigned capacity."
                }, HttpStatusCode.BadRequest);
            }

            if (Backup.DoesBackupExist(service, realFileName, backupType))
            {
                return new JsonHttpStatusResult(new
                {
                    Message = $"Backup already exists with name <strong>{realFileName}</strong>"
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
                backup.CustomFields["SIZE"] = fileSize;
                backup.GenerateKey();
                backup.Save();
            }
            catch (Exception e)
            {
                return new JsonHttpStatusResult(new
                {
                    Message = "Failed to backup - " + e.Message + " | " + e.StackTrace
                }, HttpStatusCode.InternalServerError);
            }

            return Json(new
            {
                Message = $"Backed up <strong>{backupName}</strong>"
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
                    Message = "No backup selected to restore."
                }, HttpStatusCode.InternalServerError);
            }

            var service = Service.GetSelectedService();
            var server = TCAdmin.GameHosting.SDK.Objects.Server.GetSelectedServer();
            var dirsec = service.GetDirectorySecurityForCurrentUser();
            var vdir = new TCAdmin.SDK.VirtualFileSystem.VirtualDirectory(server.OperatingSystem, dirsec);
            var fileSystem = TCAdmin.SDK.Objects.Server.GetSelectedServer().FileSystemService;
            var backup = new Backup(backupId);
            var backupSolution = backup.BackupSolution;

            try
            {
                var targetpath = vdir.CombineWithPhysicalPath(target);
                var saveTo = TCAdmin.SDK.Misc.FileSystem.CombinePath(targetpath, backup.FileName, server.OperatingSystem);

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
                    int bytesread;
                    memoryStream.Position = 0;

                    if (fileSystem.FileExists(saveTo))
                    {
                        fileSystem.DeleteFile(saveTo);
                    }

                    while ((bytesread = memoryStream.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
                    {
                        fileSystem.AppendFile(saveTo, byteBuffer.Take(bytesread).ToArray());
                    }
                    fileSystem.SetOwnerAutomatically(saveTo);
                }
                
                return new JsonHttpStatusResult(new
                {
                    Message = $"Restored <strong>{backup.FileName}</strong>"
                }, HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                return new JsonHttpStatusResult(new
                {
                    Message = "An error occurred: " + e.Message + " | " + e.StackTrace
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
                    Message = "No backup selected to delete."
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
                    Message = $"Deleted <strong>{backup.FileName}</strong>"
                }, HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                return new JsonHttpStatusResult(new
                {
                    Message = "An error occurred: " + e.Message
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
            var value = GetBackupsSize(service, backupType);
            var limit = GetBackupsLimit(service, backupType);

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
        public static List<BackupType> AccessibleSolutions(int id)
        {
            var settings = GlobalBackupSettings.Get();
            var accessibleSolutions = new List<BackupType>();
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var service = Service.GetSelectedService();

            var s3Limit = service.Variables["S3:LIMIT"] != null
                ? long.Parse(service.Variables["S3:LIMIT"].ToString())
                : settings.DefaultS3Capacity;
            if (FileServer.GetFileServers().S3FileServers().Any() && s3Limit > 0 && settings.S3Enabled)
            {
                accessibleSolutions.Add(BackupType.S3);
            }

            var ftpLimit = service.Variables["Ftp:LIMIT"] != null
                ? long.Parse(service.Variables["Ftp:LIMIT"].ToString())
                : settings.DefaultFtpCapacity;
            if (FileServer.GetFileServers().FtpFileServers().Any() && ftpLimit > 0 && settings.FtpEnabled)
            {
                accessibleSolutions.Add(BackupType.Ftp);
            }

            var localLimit = service.Variables["Local:LIMIT"] != null
                ? long.Parse(service.Variables["Local:LIMIT"].ToString())
                : settings.DefaultLocalCapacity;
            if (localLimit > 0 && settings.LocalEnabled)
            {
                accessibleSolutions.Add(BackupType.Local);
            }

            return accessibleSolutions;
        }

        private static long GetBackupsSize(Service service, BackupType backupType)
        {
            var backups = Backup.GetBackupsForService(service);
            backups.RemoveAll(x => x.BackupType != backupType);

            var value = backups.Sum(backup => backup.FileSize);

            return value;
        }

        private static long GetBackupsLimit(Service service, BackupType backupType)
        {
            var limit = service.Variables[$"{backupType.ToString()}:LIMIT"] != null
                ? long.Parse(service.Variables[$"{backupType.ToString().ToUpper()}:LIMIT"].ToString())
                : GlobalBackupSettings.GetDefaultCapacity(backupType);

            return limit;
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