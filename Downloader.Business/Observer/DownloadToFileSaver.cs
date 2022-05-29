using System;
using System.IO;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Contract.Events;

namespace Toqe.Downloader.Business.Observer
{
    public class DownloadToFileSaver : AbstractDownloadObserver
    {
        #region Field

        private FileInfo file;

        private FileStream fileStream;

        #endregion

        #region Constructor

        public DownloadToFileSaver(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException("filename");
            }

            this.file = new FileInfo(filename);
        }

        public DownloadToFileSaver(FileInfo file)
        {
            if (file == null)
            {
                throw new ArgumentNullException("file");
            }

            this.file = file;
        }

        #endregion

        #region Public Method

        public override void Dispose()
        {
            lock (this.monitor)
            {
                this.CloseFile();
            }

            base.Dispose();
        }

        #endregion

        #region Protected Method

        protected override void OnAttach(IDownload download)
        {
            download.DownloadStarted += this.downloadStarted;
            download.DownloadCancelled += this.downloadCancelled;
            download.DownloadCompleted += this.downloadCompleted;
            download.DownloadStopped += this.downloadStopped;
            download.DataReceived += this.downloadDataReceived;
        }

        protected override void OnDetach(IDownload download)
        {
            download.DownloadStarted -= this.downloadStarted;
            download.DownloadCancelled -= this.downloadCancelled;
            download.DownloadCompleted -= this.downloadCompleted;
            download.DownloadStopped -= this.downloadStopped;
            download.DataReceived -= this.downloadDataReceived;
        }

        #endregion

        #region Private Method

        private void OpenFileIfNecessary()
        {
            lock (this.monitor)
            {
                if (this.fileStream == null)
                {
                    this.fileStream = this.file.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
            }
        }

        private void WriteToFile(byte[] data, long offset, int count)
        {
            lock (this.monitor)
            {
                this.OpenFileIfNecessary();

                this.fileStream.Position = offset;
                this.fileStream.Write(data, 0, count);
            }
        }

        private void CloseFile()
        {
            lock (this.monitor)
            {
                if (this.fileStream != null)
                {
                    this.fileStream.Flush();
                    this.fileStream.Close();
                    this.fileStream.Dispose();
                    this.fileStream = null;
                }
            }
        }

        #endregion

        #region Evvent Handler

        private void downloadDataReceived(DownloadDataReceivedEventArgs args)
        {
            lock (this.monitor)
            {
                this.WriteToFile(args.Data, args.Offset, args.Count);
            }
        }

        private void downloadStarted(DownloadStartedEventArgs args)
        {
            lock (this.monitor)
            {
                this.OpenFileIfNecessary();
            }
        }

        private void downloadCompleted(DownloadEventArgs args)
        {
            lock (this.monitor)
            {
                this.CloseFile();
            }
        }

        private void downloadStopped(DownloadEventArgs args)
        {
            lock (this.monitor)
            {
                this.CloseFile();
            }
        }

        private void downloadCancelled(DownloadCancelledEventArgs args)
        {
            lock (this.monitor)
            {
                this.CloseFile();
            }
        }

        #endregion
    }
}
