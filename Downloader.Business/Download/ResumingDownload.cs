using System;
using System.Threading;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Contract.Enums;
using Toqe.Downloader.Business.Contract.Events;
using Toqe.Downloader.Business.Contract.Exceptions;

namespace Toqe.Downloader.Business.Download
{
    public class ResumingDownload : AbstractDownload
    {
        #region Field

        private readonly int timeForHeartbeat;

        private readonly int timeToRetry;

        private readonly int? maxRetries;

        private readonly IDownloadBuilder downloadBuilder;

        private bool downloadStartedNotified;

        private long currentOffset;

        private long sumOfBytesRead;

        private IDownload currentDownload;

        private DateTime lastHeartbeat;

        private int currentRetry = 0;

        #endregion

        #region Constructor

        public ResumingDownload(Uri url, 
                                int bufferSize, 
                                long? offset, 
                                long? maxReadBytes, 
                                int timeForHeartbeat, 
                                int timeToRetry, 
                                int? maxRetries, 
                                IDownloadBuilder downloadBuilder)
            : base(url, bufferSize, offset, maxReadBytes, null, null)
        {
            if (timeForHeartbeat <= 0)
            {
                throw new ArgumentException("timeForHeartbeat <= 0");
            }

            if (timeToRetry <= 0)
            {
                throw new ArgumentException("timeToRetry <= 0");
            }

            if (downloadBuilder == null)
            {
                throw new ArgumentException("downloadBuilder");
            }

            this.timeForHeartbeat = timeForHeartbeat;
            this.timeToRetry = timeToRetry;
            this.maxRetries = maxRetries;
            this.downloadBuilder = downloadBuilder;
        }

        #endregion

        #region Protected Method

        protected override void OnStart()
        {
            this.StartThread(this.StartDownload, string.Format("ResumingDownload offset {0} length {1} Main", this.offset, this.maxReadBytes));
            this.StartThread(this.CheckHeartbeat, string.Format("ResumingDownload offset {0} length {1} Heartbeat", this.offset, this.maxReadBytes));
        }

        protected override void OnStop()
        {
            lock (this.monitor)
            {
                this.stopping = true;
                this.DoStopIfNecessary();
            }
        }

        #endregion

        #region Private Method

        private void StartDownload()
        {
            lock (this.monitor)
            {
                this.StartNewDownload();
            }
        }

        private void StartNewDownload()
        {
            this.currentOffset = this.offset.HasValue ? this.offset.Value : 0;
            this.BuildDownload();
        }

        private void CheckHeartbeat()
        {
            while (true)
            {
                Thread.Sleep(this.timeForHeartbeat);

                lock (this.monitor)
                {
                    if (this.DoStopIfNecessary())
                    {
                        return;
                    }

                    if (DateTime.Now - this.lastHeartbeat > TimeSpan.FromMilliseconds(this.timeForHeartbeat))
                    {
                        this.CountRetryAndCancelIfMaxRetriesReached();

                        if (this.currentDownload != null)
                        {
                            this.CloseDownload();
                            this.StartThread(this.BuildDownload, Thread.CurrentThread.Name + "-byHeartbeat");
                        }
                    }
                }
            }
        }

        private void CountRetryAndCancelIfMaxRetriesReached()
        {
            if (this.maxRetries.HasValue && this.currentRetry >= this.maxRetries)
            {
                this.state = DownloadState.Cancelled;
                this.OnDownloadCancelled(new DownloadCancelledEventArgs(this, new TooManyRetriesException()));
                this.DoStop(DownloadStopType.WithoutNotification);
            }

            this.currentRetry++;
        }

        private void BuildDownload()
        {
            lock (this.monitor)
            {
                if (this.DoStopIfNecessary())
                {
                    return;
                }

                long? currentMaxReadBytes = this.maxReadBytes.HasValue ? (long?)this.maxReadBytes.Value - this.sumOfBytesRead : null;

                this.currentDownload = this.downloadBuilder.Build(this.url, this.bufferSize, this.currentOffset, currentMaxReadBytes);
                this.currentDownload.DownloadStarted += this.downloadStarted;
                this.currentDownload.DownloadCancelled += this.downloadCancelled;
                this.currentDownload.DownloadCompleted += this.downloadCompleted;
                this.currentDownload.DataReceived += this.downloadDataReceived;
                this.StartThread(this.currentDownload.Start, Thread.CurrentThread.Name + "-buildDownload");
            }
        }

        private bool DoStopIfNecessary()
        {
            if (this.stopping)
            {
                this.CloseDownload();

                lock (this.monitor)
                {
                    this.state = DownloadState.Stopped;
                }
            }

            return this.stopping;
        }

        private void SleepThenBuildDownload()
        {
            Thread.Sleep(this.timeToRetry);
            this.BuildDownload();
        }

        private void CloseDownload()
        {
            if (this.currentDownload != null)
            {
                this.currentDownload.DetachAllHandlers();
                this.currentDownload.Stop();
                this.currentDownload = null;
            }
        }

        #endregion

        #region Event Handler

        private void downloadDataReceived(DownloadDataReceivedEventArgs args)
        {
            IDownload download = args.Download;
            int count = args.Count;
            byte[] data = args.Data;
            long previousOffset = 0;

            lock (this.monitor)
            {
                if (this.currentDownload == download)
                {
                    if (this.DoStopIfNecessary())
                    {
                        return;
                    }

                    previousOffset = this.currentOffset;

                    this.lastHeartbeat = DateTime.Now;
                    this.currentOffset += count;
                    this.sumOfBytesRead += count;
                }
            }

            this.OnDataReceived(new DownloadDataReceivedEventArgs(this, data, previousOffset, count));
        }

        private void downloadStarted(DownloadStartedEventArgs args)
        {
            IDownload download = args.Download;
            bool shouldNotifyDownloadStarted = false;

            lock (this.monitor)
            {
                if (download == this.currentDownload)
                {
                    if (!this.downloadStartedNotified)
                    {
                        shouldNotifyDownloadStarted = true;
                        this.downloadStartedNotified = true;
                    }
                }
            }

            if (shouldNotifyDownloadStarted)
            {
                this.OnDownloadStarted(new DownloadStartedEventArgs(this, args.CheckResult, args.AlreadyDownloadedSize));
            }
        }

        private void downloadCompleted(DownloadEventArgs args)
        {
            lock (this.monitor)
            {
                this.CloseDownload();
                this.state = DownloadState.Finished;
                this.stopping = true;
            }

            this.OnDownloadCompleted(new DownloadEventArgs(this));
        }

        private void downloadCancelled(DownloadCancelledEventArgs args)
        {
            IDownload download = args.Download;

            lock (this.monitor)
            {
                if (download == this.currentDownload)
                {
                    this.CountRetryAndCancelIfMaxRetriesReached();

                    if (this.currentDownload != null)
                    {
                        this.currentDownload = null;
                        this.StartThread(this.SleepThenBuildDownload, Thread.CurrentThread.Name + "-afterCancel");
                    }
                }
            }
        }

        #endregion
    }
}