﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Contract.Events;

namespace Toqe.Downloader.Business.Observer
{
    public class DownloadThrottling : IDownloadObserver, IDisposable
    {
        #region Field

        private readonly int maxBytesPerSecond;

        private readonly List<IDownload> downloads = new List<IDownload>();

        private readonly int maxSampleCount;

        private List<DownloadDataSample> samples;

        private double floatingWaitingTimeInMilliseconds = 0;

        private object monitor = new object();

        #endregion

        #region Constructor

        public DownloadThrottling(int maxBytesPerSecond, int maxSampleCount)
        {
            if (maxBytesPerSecond <= 0)
            {
                throw new ArgumentException("maxBytesPerSecond <= 0");
            }

            if (maxSampleCount < 2)
            {
                throw new ArgumentException("sampleCount < 2");
            }

            this.maxBytesPerSecond = maxBytesPerSecond;
            this.maxSampleCount = maxSampleCount;
            this.samples = new List<DownloadDataSample>();
        }

        #endregion

        #region Public Method

        public void Attach(IDownload download)
        {
            if (download == null)
            {
                throw new ArgumentNullException("download");
            }

            download.DataReceived += this.downloadDataReceived;

            lock (this.monitor)
            {
                this.downloads.Add(download);
            }
        }

        public void Detach(IDownload download)
        {
            if (download == null)
            {
                throw new ArgumentNullException("download");
            }

            download.DataReceived -= this.downloadDataReceived;

            lock (this.monitor)
            {
                this.downloads.Remove(download);
            }
        }

        public void DetachAll()
        {
            lock (this.monitor)
            {
                foreach (IDownload download in this.downloads)
                {
                    this.Detach(download);
                }
            }
        }

        public void Dispose()
        {
            this.DetachAll();
        }

        #endregion

        #region Private Method

        private void AddSample(int count)
        {
            lock (this.monitor)
            {
                DownloadDataSample sample = new DownloadDataSample()
                {
                    Count = count,
                    Timestamp = DateTime.UtcNow
                };

                this.samples.Add(sample);

                if (this.samples.Count > this.maxSampleCount)
                {
                    this.samples.RemoveAt(0);
                }
            }
        }

        private int CalculateWaitingTime()
        {
            lock (this.monitor)
            {
                if (this.samples.Count < 2)
                {
                    return 0;
                }

                double averageBytesPerCall = this.samples.Average(s => s.Count);
                double sumOfTicksBetweenCalls = 0;

                // 1 tick = 100 nano seconds, 1 ms ^= 10.000 ticks
                for (int i = 0; i < this.samples.Count - 1; i++)
                {
                    sumOfTicksBetweenCalls += (this.samples[i + 1].Timestamp - this.samples[i].Timestamp).Ticks;
                }

                double averageTicksBetweenCalls = sumOfTicksBetweenCalls / (this.samples.Count - 1);
                double timePerNetworkRequestInMilliseconds = averageTicksBetweenCalls / 10000 - this.floatingWaitingTimeInMilliseconds;

                double currentBytesPerMillisecond = averageBytesPerCall / averageTicksBetweenCalls * 10000;
                double maxBytesPerMillisecond = (double)this.maxBytesPerSecond / 1000;

                double waitingTimeInMilliseconds = averageBytesPerCall / maxBytesPerMillisecond - timePerNetworkRequestInMilliseconds;
                this.floatingWaitingTimeInMilliseconds = waitingTimeInMilliseconds * 0.6 + this.floatingWaitingTimeInMilliseconds * 0.4;

                return (int)waitingTimeInMilliseconds;
            }
        }

        #endregion

        #region Event Handler

        private void downloadDataReceived(DownloadDataReceivedEventArgs args)
        {
            this.AddSample(args.Count);
            int waitingTime = this.CalculateWaitingTime();

            if (waitingTime > 0)
            {
                Thread.Sleep(waitingTime);
            }
        }

        #endregion
    }
}