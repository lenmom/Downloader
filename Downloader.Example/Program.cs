using System;
using System.Collections.Generic;
using System.Threading;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Contract.Events;
using Toqe.Downloader.Business.Download;
using Toqe.Downloader.Business.DownloadBuilder;
using Toqe.Downloader.Business.Observer;
using Toqe.Downloader.Business.Utils;

namespace Downloader.Example
{
    public class Program
    {
        private static bool finished = false;

        public static void Main()
        {
            bool useDownloadSpeedThrottling = false;

            // Please insert an URL of a large file here, otherwise the download will be finished too quickly to really demonstrate the functionality.
            Uri url = new Uri("https://raw.githubusercontent.com/Toqe/Downloader/master/README.md");
            System.IO.FileInfo file = new System.IO.FileInfo("README.md");
            SimpleWebRequestBuilder requestBuilder = new SimpleWebRequestBuilder();
            DownloadChecker dlChecker = new DownloadChecker();
            SimpleDownloadBuilder httpDlBuilder = new SimpleDownloadBuilder(requestBuilder, dlChecker);
            int timeForHeartbeat = 3000;
            int timeToRetry = 5000;
            int maxRetries = 5;
            ResumingDownloadBuilder resumingDlBuilder = new ResumingDownloadBuilder(timeForHeartbeat, timeToRetry, maxRetries, httpDlBuilder);
            List<DownloadRange> alreadyDownloadedRanges = null;
            int bufferSize = 4096;
            int numberOfParts = 4;
            MultiPartDownload download = new MultiPartDownload(url, bufferSize, numberOfParts, resumingDlBuilder, requestBuilder, dlChecker, alreadyDownloadedRanges);
            DownloadSpeedMonitor speedMonitor = new DownloadSpeedMonitor(maxSampleCount: 128);
            speedMonitor.Attach(download);
            DownloadProgressMonitor progressMonitor = new DownloadProgressMonitor();
            progressMonitor.Attach(download);

            if (useDownloadSpeedThrottling)
            {
                DownloadThrottling downloadThrottling = new DownloadThrottling(maxBytesPerSecond: 200 * 1024, maxSampleCount: 128);
                downloadThrottling.Attach(download);
            }

            DownloadToFileSaver dlSaver = new DownloadToFileSaver(file);
            dlSaver.Attach(download);
            download.DownloadCompleted += OnCompleted;
            download.Start();

            while (!finished)
            {
                Thread.Sleep(1000);

                long alreadyDownloadedSizeInBytes = progressMonitor.GetCurrentProgressInBytes(download);
                long totalDownloadSizeInBytes = progressMonitor.GetTotalFilesizeInBytes(download);
                int currentSpeedInBytesPerSecond = speedMonitor.GetCurrentBytesPerSecond();

                float currentProgressInPercent = progressMonitor.GetCurrentProgressPercentage(download) * 100;
                long alreadyDownloadedSizeInKiB = (alreadyDownloadedSizeInBytes / 1024);
                long totalDownloadSizeInKiB = (totalDownloadSizeInBytes / 1024);
                int currentSpeedInKiBPerSecond = (currentSpeedInBytesPerSecond / 1024);
                long remainingTimeInSeconds = currentSpeedInBytesPerSecond == 0 ? 0 : (totalDownloadSizeInBytes - alreadyDownloadedSizeInBytes) / currentSpeedInBytesPerSecond;

                Console.WriteLine(
                    "Progress: " + currentProgressInPercent + "% " + "(" + alreadyDownloadedSizeInKiB + " of " + totalDownloadSizeInKiB + " KiB)" +
                    "   Speed: " + currentSpeedInKiBPerSecond + " KiB/sec." +
                    "   Remaining time: " + remainingTimeInSeconds + " sec.");
            }
        }

        private static void OnCompleted(DownloadEventArgs args)
        {
            // this is an important thing to do after a download isn't used anymore, otherwise you will run into a memory leak.
            args.Download.DetachAllHandlers();
            Console.WriteLine("Download has finished!");
            finished = true;
        }
    }
}