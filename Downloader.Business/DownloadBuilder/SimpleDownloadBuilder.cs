using System;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Download;

namespace Toqe.Downloader.Business.DownloadBuilder
{
    public class SimpleDownloadBuilder : IDownloadBuilder
    {
        #region Field

        private readonly IWebRequestBuilder requestBuilder;

        private readonly IDownloadChecker downloadChecker;

        #endregion

        #region Constructor

        public SimpleDownloadBuilder(IWebRequestBuilder requestBuilder, IDownloadChecker downloadChecker)
        {
            if (requestBuilder == null)
            {
                throw new ArgumentNullException("requestBuilder");
            }

            if (downloadChecker == null)
            {
                throw new ArgumentNullException("downloadChecker");
            }

            this.requestBuilder = requestBuilder;
            this.downloadChecker = downloadChecker;
        }

        #endregion

        #region Public Method

        public IDownload Build(Uri url, int bufferSize, long? offset, long? maxReadBytes)
        {
            return new SimpleDownload(url, bufferSize, offset, maxReadBytes, this.requestBuilder, this.downloadChecker);
        }

        #endregion
    }
}