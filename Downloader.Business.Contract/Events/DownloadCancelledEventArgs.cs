using System;

namespace Toqe.Downloader.Business.Contract.Events
{
    public class DownloadCancelledEventArgs : DownloadEventArgs
    {
        public DownloadCancelledEventArgs()
        {
        }

        public DownloadCancelledEventArgs(IDownload download, Exception exception)
        {
            this.Download = download;
            this.Exception = exception;
        }

        public Exception Exception
        {
            get; set;
        }
    }
}
