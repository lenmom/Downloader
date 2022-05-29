using System;
using System.Collections.Generic;
using System.Linq;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Contract.Enums;
using Toqe.Downloader.Business.Contract.Events;
using Toqe.Downloader.Business.Contract.Exceptions;
using Toqe.Downloader.Business.Utils;

namespace Toqe.Downloader.Business.Download
{
    public class MultiPartDownload : AbstractDownload
    {
        #region Field

        private readonly DownloadRangeHelper downloadRangeHelper = new DownloadRangeHelper();

        private readonly int numberOfParts;

        private readonly IDownloadBuilder downloadBuilder;

        private readonly Dictionary<IDownload, DownloadRange> downloads = new Dictionary<IDownload, DownloadRange>();

        #endregion

        #region Property

        public List<DownloadRange> AlreadyDownloadedRanges
        {
            get; private set;
        }

        public List<DownloadRange> ToDoRanges
        {
            get; private set;
        }

        #endregion

        #region Constructor

        public MultiPartDownload(Uri url,
                                  int bufferSize,
                                  int numberOfParts,
                                  IDownloadBuilder downloadBuilder,
                                  IWebRequestBuilder requestBuilder,
                                  IDownloadChecker downloadChecker,
                                  List<DownloadRange> alreadyDownloadedRanges)
           : base(url, bufferSize, null, null, requestBuilder, downloadChecker)
        {
            if (numberOfParts <= 0)
            {
                throw new ArgumentException("numberOfParts <= 0");
            }

            if (downloadBuilder == null)
            {
                throw new ArgumentNullException("downloadBuilder");
            }

            this.numberOfParts = numberOfParts;
            this.downloadBuilder = downloadBuilder;
            this.AlreadyDownloadedRanges = alreadyDownloadedRanges ?? new List<DownloadRange>();

            if (System.Net.ServicePointManager.DefaultConnectionLimit < numberOfParts)
            {
                System.Net.ServicePointManager.DefaultConnectionLimit = numberOfParts;
            }
        }

        #endregion

        #region Override Method

        protected override void OnStart()
        {
            DownloadCheckResult downloadCheck = this.PerformInitialDownloadCheck();
            this.DetermineFileSizeAndStartDownloads(downloadCheck);
        }

        protected override void OnStop()
        {
            List<IDownload> currentDownloads = new List<IDownload>();

            lock (this.monitor)
            {
                if (this.downloads != null && this.downloads.Count > 0)
                {
                    currentDownloads = new List<IDownload>(this.downloads.Keys);
                }
            }

            foreach (IDownload download in currentDownloads)
            {
                download.DetachAllHandlers();
                download.Stop();
            }

            lock (this.monitor)
            {
                this.state = DownloadState.Stopped;
            }
        }

        #endregion

        #region Private Method

        private DownloadCheckResult PerformInitialDownloadCheck()
        {
            DownloadCheckResult downloadCheck = this.downloadChecker.CheckDownload(this.url, this.requestBuilder);

            if (!downloadCheck.Success)
            {
                throw new DownloadCheckNotSuccessfulException("Download check was not successful. HTTP status code: " + downloadCheck.StatusCode, downloadCheck.Exception, downloadCheck);
            }

            if (!downloadCheck.SupportsResume)
            {
                throw new ResumingNotSupportedException();
            }

            this.OnDownloadStarted(new DownloadStartedEventArgs(this, downloadCheck, this.AlreadyDownloadedRanges.Sum(x => x.Length)));

            return downloadCheck;
        }

        private void DetermineFileSizeAndStartDownloads(DownloadCheckResult downloadCheck)
        {
            lock (this.monitor)
            {
                this.ToDoRanges = this.DetermineToDoRanges(downloadCheck.Size, this.AlreadyDownloadedRanges);
                this.SplitToDoRangesForNumberOfParts();

                for (int i = 0; i < this.numberOfParts; i++)
                {
                    DownloadRange todoRange = this.ToDoRanges[i];
                    this.StartDownload(todoRange);
                }
            }
        }

        private void SplitToDoRangesForNumberOfParts()
        {
            while (this.ToDoRanges.Count < this.numberOfParts)
            {
                DownloadRange maxRange = this.ToDoRanges.FirstOrDefault(r => r.Length == this.ToDoRanges.Max(r2 => r2.Length));
                this.ToDoRanges.Remove(maxRange);
                long range1Start = maxRange.Start;
                long range1Length = maxRange.Length / 2;
                long range2Start = range1Start + range1Length;
                long range2Length = maxRange.End - range2Start + 1;
                this.ToDoRanges.Add(new DownloadRange(range1Start, range1Length));
                this.ToDoRanges.Add(new DownloadRange(range2Start, range2Length));
            }
        }

        private void StartDownload(DownloadRange range)
        {
            IDownload download = this.downloadBuilder.Build(this.url, this.bufferSize, range.Start, range.Length);
            download.DataReceived += this.downloadDataReceived;
            download.DownloadCancelled += this.downloadCancelled;
            download.DownloadCompleted += this.downloadCompleted;
            download.Start();

            lock (this.monitor)
            {
                this.downloads.Add(download, range);
            }
        }

        private List<DownloadRange> DetermineToDoRanges(long fileSize, List<DownloadRange> alreadyDoneRanges)
        {
            List<DownloadRange> result = new List<DownloadRange>();

            DownloadRange initialRange = new DownloadRange(0, fileSize);
            result.Add(initialRange);

            if (alreadyDoneRanges != null && alreadyDoneRanges.Count > 0)
            {
                foreach (DownloadRange range in alreadyDoneRanges)
                {
                    List<DownloadRange> newResult = new List<DownloadRange>(result);

                    foreach (DownloadRange resultRange in result)
                    {
                        if (this.downloadRangeHelper.RangesCollide(range, resultRange))
                        {
                            newResult.Remove(resultRange);
                            List<DownloadRange> difference = this.downloadRangeHelper.RangeDifference(resultRange, range);
                            newResult.AddRange(difference);
                        }
                    }

                    result = newResult;
                }
            }

            return result;
        }

        private void StartDownloadOfNextRange()
        {
            DownloadRange nextRange = null;

            lock (this.monitor)
            {
                nextRange = this.ToDoRanges.FirstOrDefault(r => !this.downloads.Values.Any(r2 => this.downloadRangeHelper.RangesCollide(r, r2)));
            }

            if (nextRange != null)
            {
                this.StartDownload(nextRange);
            }

            if (!this.downloads.Any())
            {
                lock (this.monitor)
                {
                    this.state = DownloadState.Finished;
                }

                this.OnDownloadCompleted(new DownloadEventArgs(this));
            }
        }

        #endregion

        #region Event Handler

        private void downloadDataReceived(DownloadDataReceivedEventArgs args)
        {
            long offset = args.Offset;
            int count = args.Count;
            byte[] data = args.Data;

            lock (this.monitor)
            {
                DownloadRange justDownloadedRange = new DownloadRange(offset, count);

                DownloadRange todoRange = this.ToDoRanges.Single(r => this.downloadRangeHelper.RangesCollide(r, justDownloadedRange));
                this.ToDoRanges.Remove(todoRange);
                List<DownloadRange> differences = this.downloadRangeHelper.RangeDifference(todoRange, justDownloadedRange);
                this.ToDoRanges.AddRange(differences);

                DownloadRange alreadyDoneRange = this.AlreadyDownloadedRanges.FirstOrDefault(r => r.End + 1 == justDownloadedRange.Start);

                if (alreadyDoneRange == null)
                {
                    alreadyDoneRange = justDownloadedRange;
                    this.AlreadyDownloadedRanges.Add(alreadyDoneRange);
                }
                else
                {
                    alreadyDoneRange.Length += justDownloadedRange.Length;
                }

                DownloadRange neighborRange = this.AlreadyDownloadedRanges.FirstOrDefault(r => r.Start == alreadyDoneRange.End + 1);

                if (neighborRange != null)
                {
                    this.AlreadyDownloadedRanges.Remove(alreadyDoneRange);
                    this.AlreadyDownloadedRanges.Remove(neighborRange);
                    DownloadRange combinedRange = new DownloadRange(alreadyDoneRange.Start, alreadyDoneRange.Length + neighborRange.Length);
                    this.AlreadyDownloadedRanges.Add(combinedRange);
                }
            }

            this.OnDataReceived(new DownloadDataReceivedEventArgs(this, data, offset, count));
        }

        private void downloadCompleted(DownloadEventArgs args)
        {
            lock (this.monitor)
            {
                ResumingDownload resumingDownload = (ResumingDownload)args.Download;
                this.downloads.Remove(resumingDownload);
            }

            this.StartDownloadOfNextRange();
        }

        private void downloadCancelled(DownloadCancelledEventArgs args)
        {
            this.StartDownloadOfNextRange();
        }

        #endregion
    }
}