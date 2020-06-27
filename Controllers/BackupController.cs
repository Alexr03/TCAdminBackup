using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using TCAdmin.SDK.Web.FileManager;
using TCAdmin.SDK.Web.MVC.Controllers;
using TCAdminBackup.BackupSolutions;
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
            var server = new Server(service.ServerId);
            var backupSolution = Backup.ParseBackupSolution(backupType, service);

            if (Backup.DoesBackupExist(service, realFileName))
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = $"Backup already exists with name <strong>{realFileName}</strong>"
                }, HttpStatusCode.InternalServerError);
            }

            var remoteDownload = new RemoteDownload(server)
            {
                DirectorySecurity = service.GetDirectorySecurityForCurrentUser(),
                FileName = Path.Combine(service.WorkingDirectory, file)
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
                backup.GenerateKey();
                backup.Save();
            }
            catch (Exception e)
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = "Failed to backup - " + e.Message
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
            if (backupId == 0)
            {
                return new JsonHttpStatusResult(new
                {
                    responseText = "No backup selected to restore."
                }, HttpStatusCode.InternalServerError);
            }

            var service = Service.GetSelectedService();
            var fileSystem = new Server(service.ServerId).FileSystemService;
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
                    fileSystem.CreateTextFile(saveTo, bytes);
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
                    responseText = "An error occurred: " + e.Message
                }, HttpStatusCode.InternalServerError);
            }
        }

        [ParentAction("Service", "Home")]
        public async Task<ActionResult> Delete(int id, int backupId = 0)
        {
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
            var backup = new Backup(backupId);
            var backupSolution = backup.BackupSolution;

            if (backupSolution.AllowsDirectDownload)
            {
                var downloadUrl = await backupSolution.DirectDownloadLink(backup.FileName);
                return Json(new
                {
                    url = downloadUrl
                });
            }

            return new JsonHttpStatusResult(new
            {
                responseText = $"{backupSolution.GetType().Name} does not support direct downloads."
            }, HttpStatusCode.Forbidden);
        }

        [ParentAction("Service", "Home")]
        public async Task<ActionResult> Capacity(int id)
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var service = Service.GetSelectedService();
            var s3 = new S3Backup(FileServerModel.DetermineFileServer(service, BackupType.S3), user.CloudBackupsBucketName());
            var limit = service.Variables["S3:LIMIT"] != null
                ? long.Parse(service.Variables["S3:LIMIT"].ToString())
                : long.Parse(5_000_000_000.ToString());
            var capacity = await s3.GetUsedSizeForCurrentUser();

            if (limit == -1)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            return Json(new
            {
                limit,
                value = capacity
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
            if (s3Limit > 0 && settings.S3Enabled)
            {
                accessibleSolutions.Add("s3");
            }
            
            var ftpLimit = service.Variables["Ftp:LIMIT"] != null
                ? long.Parse(service.Variables["Ftp:LIMIT"].ToString())
                : long.Parse(5_000_000_000.ToString());
            if (ftpLimit > 0 && settings.FtpEnabled)
            {
                accessibleSolutions.Add("ftp");
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