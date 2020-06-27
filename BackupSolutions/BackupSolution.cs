using System.Threading.Tasks;
using TCAdmin.GameHosting.SDK.Objects;

namespace TCAdminBackup.BackupSolutions
{
    public abstract class BackupSolution
    {
        public bool AllowsDirectDownload { get; set; }

        public readonly Service Service;

        protected BackupSolution()
        {
            this.Service = Service.GetSelectedService();
            this.AllowsDirectDownload = false;
        }

        protected BackupSolution(Service service, bool allowsDirectDownload) : this()
        {
            Service = service;
            AllowsDirectDownload = allowsDirectDownload;
        }

        /// <summary>
        /// Backup a file with specific file name, contents and content type
        /// </summary>
        /// <param name="fileName">Name of the file</param>
        /// <param name="contents">Contents of the file</param>
        /// <param name="contentType">Type of the file</param>
        /// <returns></returns>
        public abstract Task<bool> Backup(string fileName, byte[] contents, string contentType);

        /// <summary>
        /// Download the bytes of the file.
        /// </summary>
        /// <param name="fileName">Name of the file to get the bytes for</param>
        /// <returns>Byte array of the file</returns>
        public abstract Task<byte[]> DownloadBytes(string fileName);

        /// <summary>
        /// If the backup type supports a direct download link to the file. Return it here.
        /// Set "AllowsDirectDownload" to true.
        /// </summary>
        /// <param name="fileName">Name of the file to get direct download for.</param>
        /// <returns>URL for direct download of the file</returns>
        public abstract Task<string> DirectDownloadLink(string fileName);

        /// <summary>
        /// Delete the file from the backup server.
        /// </summary>
        /// <param name="fileName">Name of the file to delete</param>
        /// <returns>True if deleted successfully.</returns>
        public abstract Task<bool> Delete(string fileName);
    }
}