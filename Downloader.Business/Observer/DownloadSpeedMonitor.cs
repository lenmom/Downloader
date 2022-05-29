using System;
using System.Collections.Generic;
using System.Linq;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Contract.Events;

namespace Toqe.Downloader.Business.Observer
{
    public class DownloadSpeedMonitor : AbstractDownloadObserver
    {
        private readonly int maxSampleCount;

        private readonly List<DownloadDataSample> samples = new List<DownloadDataSample>();

        public DownloadSpeedMonitor(int maxSampleCount)
        {
            if (maxSampleCount < 2)
            {
                throw new ArgumentException("maxSampleCount < 2");
            }

            this.maxSampleCount = maxSampleCount;
        }

        public int GetCurrentBytesPerSecond()
        {
            lock (this.monitor)
            {
                if (this.samples.Count < 2)
                {
                    return 0;
                }

                int sumOfBytesFromCalls = this.samples.Sum(s => s.Count);
                long ticksBetweenCalls = (DateTime.UtcNow - this.samples[0].Timestamp).Ticks;

                return (int)((double)sumOfBytesFromCalls / ticksBetweenCalls * 10000 * 1000);
            }
        }

        protected override void OnAttach(IDownload download)
        {
            download.DataReceived += this.downloadDataReceived;
        }

        protected override void OnDetach(IDownload download)
        {
            download.DataReceived -= this.downloadDataReceived;
        }

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

        private void downloadDataReceived(DownloadDataReceivedEventArgs args)
        {
            this.AddSample(args.Count);
        }
    }
}