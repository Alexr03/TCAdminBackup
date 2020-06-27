using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using TCAdmin.SDK.Objects;
using TCAdminBackup.Models.Objects;
using Service = TCAdmin.GameHosting.SDK.Objects.Service;

namespace TCAdminBackup
{
    public static class Extensions
    {
        public static List<FileServer> S3FileServers(this ObjectList fileServers)
        {
            return fileServers.Cast<FileServer>().Where(x => x.Name.StartsWith("[S3]")).ToList();
        }
        
        public static List<FileServer> FtpFileServers(this ObjectList fileServers)
        {
            return fileServers.Cast<FileServer>().Where(x => x.Name.StartsWith("[FTP]")).ToList();
        }

        public static string CloudBackupsBucketName(this User user)
        {
            return $"{user.UserName.ToLower()}-backups";
        }
    }
    
    public class JsonHttpStatusResult : JsonResult
    {
        private readonly HttpStatusCode _httpStatus;

        public JsonHttpStatusResult(object data, HttpStatusCode httpStatus, JsonRequestBehavior behavior = JsonRequestBehavior.AllowGet)
        {
            Data = data;
            _httpStatus = httpStatus;
            JsonRequestBehavior = behavior;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            context.RequestContext.HttpContext.Response.StatusCode = (int)_httpStatus;
            base.ExecuteResult(context);
        }
    }
}