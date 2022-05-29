using System;
using System.Collections.Generic;

using Toqe.Downloader.Business.Contract.Events;
using Toqe.Downloader.Business.DownloadBuilder;

namespace Downloader.Test
{
    public class MultiPartDownloadTest
    {
        private static readonly Uri url = new Uri("http://test.com");

        private static readonly int bufferSize = 4096;

        private static readonly int numberOfParts = 4;

        [Fact]
        public void TestMultiPartDownloadListsDuringDownload()
        {
            TestDownloadBuilder dlBuilder = new TestDownloadBuilder();
            TestWebRequestBuilder requestBuilder = new TestWebRequestBuilder();
            TestDownloadChecker dlChecker = new TestDownloadChecker();
            MultiPartDownloadBuilder mpdlBuilder = new MultiPartDownloadBuilder(numberOfParts, dlBuilder, requestBuilder, dlChecker, null);
            Toqe.Downloader.Business.Contract.IDownload dl = mpdlBuilder.Build(url, bufferSize, null, null);

            List<DownloadDataReceivedEventArgs> dataReceivedList = new List<DownloadDataReceivedEventArgs>();
            List<DownloadStartedEventArgs> downloadStartedList = new List<DownloadStartedEventArgs>();
            List<DownloadEventArgs> downloadCompletedList = new List<DownloadEventArgs>();
            List<DownloadEventArgs> downloadStoppedList = new List<DownloadEventArgs>();
            List<DownloadCancelledEventArgs> downloadCancelledList = new List<DownloadCancelledEventArgs>();

            // TODO: Register events and add args to list, if handler is called

            dl.Start();

            // TODO: wait for download to build up

            // TODO: simulate download parts and check for correct results
        }
    }
}
