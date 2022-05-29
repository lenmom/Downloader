using System.Collections.Generic;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Contract.Events;

namespace Toqe.Downloader.Business.Observer
{
    public class DownloadProgressMonitor : AbstractDownloadObserver
    {
        private readonly Dictionary<IDownload, long> downloadSizes = new Dictionary<IDownload, long>();

        private readonly Dictionary<IDownload, long> alreadyDownloadedSizes = new Dictionary<IDownload, long>();

        public float GetCurrentProgressPercentage(IDownload download)
        {
            lock (this.monitor)
            {
                if (!this.downloadSizes.ContainsKey(download) || !this.alreadyDownloadedSizes.ContainsKey(download) || this.downloadSizes[download] <= 0)
                {
                    return 0;
                }

                return (float)this.alreadyDownloadedSizes[download] / this.downloadSizes[download];
            }
        }

        public long GetCurrentProgressInBytes(IDownload download)
        {
            lock (this.monitor)
            {
                if (!this.alreadyDownloadedSizes.ContainsKey(download))
                {
                    return 0;
                }

                return this.alreadyDownloadedSizes[download];
            }
        }

        public long GetTotalFilesizeInBytes(IDownload download)
        {
            lock (this.monitor)
            {
                if (!this.downloadSizes.ContainsKey(download) || this.downloadSizes[download] <= 0)
                {
                    return 0;
                }

                return this.downloadSizes[download];
            }
        }

        protected override void OnAttach(IDownload download)
        {
            download.DownloadStarted += this.OnDownloadStarted;
            download.DataReceived += this.OnDownloadDataReceived;
            download.DownloadCompleted += this.OnDownloadCompleted;
        }

        protected override void OnDetach(IDownload download)
        {
            download.DownloadStarted -= this.OnDownloadStarted;
            download.DataReceived -= this.OnDownloadDataReceived;
            download.DownloadCompleted -= this.OnDownloadCompleted;

            lock (this.monitor)
            {
                if (this.downloadSizes.ContainsKey(download))
                {
                    this.downloadSizes.Remove(download);
                }

                if (this.alreadyDownloadedSizes.ContainsKey(download))
                {
                    this.alreadyDownloadedSizes.Remove(download);
                }
            }
        }

        private void OnDownloadStarted(DownloadStartedEventArgs args)
        {
            lock (this.monitor)
            {
                this.downloadSizes[args.Download] = args.CheckResult.Size;
                this.alreadyDownloadedSizes[args.Download] = args.AlreadyDownloadedSize;
            }
        }

        private void OnDownloadDataReceived(DownloadDataReceivedEventArgs args)
        {
            lock (this.monitor)
            {
                if (!this.alreadyDownloadedSizes.ContainsKey(args.Download))
                {
                    this.alreadyDownloadedSizes[args.Download] = 0;
                }

                this.alreadyDownloadedSizes[args.Download] += args.Count;
            }
        }

        private void OnDownloadCompleted(DownloadEventArgs args)
        {
            lock (this.monitor)
            {
                this.alreadyDownloadedSizes[args.Download] = this.downloadSizes[args.Download];
            }
        }
    }
}