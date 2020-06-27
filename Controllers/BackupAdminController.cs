using System.Web.Mvc;
using TCAdmin.SDK.Web.MVC.Controllers;
using TCAdminBackup.Configuration;

namespace TCAdminBackup.Controllers
{
    public class BackupAdminController : BaseController
    {
        [ParentAction("PluginRepository", "Details")]
        public ActionResult Index()
        {
            return View(GlobalBackupSettings.Get());
        }
        
        [HttpPost]
        [ParentAction("PluginRepository", "Details")]
        public ActionResult Index(GlobalBackupSettings settings)
        {
            GlobalBackupSettings.Set(settings);
            return View(GlobalBackupSettings.Get());
        }
    }
}