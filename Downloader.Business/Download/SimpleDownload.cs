using System;
using System.Net;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Contract.Enums;
using Toqe.Downloader.Business.Contract.Events;

namespace Toqe.Downloader.Business.Download
{
    public class SimpleDownload : AbstractDownload
    {
        public SimpleDownload(Uri url, int bufferSize, long? offset, long? maxReadBytes, IWebRequestBuilder requestBuilder, IDownloadChecker downloadChecker)
            : base(url, bufferSize, offset, maxReadBytes, requestBuilder, downloadChecker)
        {
        }

        protected override void OnStart()
        {
            try
            {
                HttpWebRequest request = this.requestBuilder.CreateRequest(this.url, this.offset);

                using (WebResponse response = request.GetResponse())
                {
                    HttpWebResponse httpResponse = response as HttpWebResponse;

                    if (httpResponse != null)
                    {
                        HttpStatusCode statusCode = httpResponse.StatusCode;

                        if (!(statusCode == HttpStatusCode.OK || (this.offset.HasValue && statusCode == HttpStatusCode.PartialContent)))
                        {
                            throw new InvalidOperationException("Invalid HTTP status code: " + httpResponse.StatusCode);
                        }
                    }

                    DownloadCheckResult checkResult = this.downloadChecker.CheckDownload(response);
                    bool supportsResume = checkResult.SupportsResume;
                    long currentOffset = supportsResume && this.offset.HasValue ? this.offset.Value : 0;
                    long sumOfBytesRead = 0;

                    this.OnDownloadStarted(new DownloadStartedEventArgs(this, checkResult, currentOffset));

                    using (System.IO.Stream stream = response.GetResponseStream())
                    {
                        byte[] buffer = new byte[this.bufferSize];

                        while (true)
                        {
                            lock (this.monitor)
                            {
                                if (this.stopping)
                                {
                                    this.state = DownloadState.Stopped;
                                    break;
                                }
                            }

                            int bytesRead = stream.Read(buffer, 0, buffer.Length);

                            if (bytesRead == 0)
                            {
                                lock (this.monitor)
                                {
                                    this.state = DownloadState.Finished;
                                }

                                this.OnDownloadCompleted(new DownloadEventArgs(this));
                                break;
                            }

                            if (this.maxReadBytes.HasValue && sumOfBytesRead + bytesRead > this.maxReadBytes.Value)
                            {
                                int count = (int)(this.maxReadBytes.Value - sumOfBytesRead);

                                if (count > 0)
                                {
                                    this.OnDataReceived(new DownloadDataReceivedEventArgs(this, buffer, currentOffset, count));
                                }
                            }
                            else
                            {
                                this.OnDataReceived(new DownloadDataReceivedEventArgs(this, buffer, currentOffset, bytesRead));
                            }

                            currentOffset += bytesRead;
                            sumOfBytesRead += bytesRead;

                            if (this.maxReadBytes.HasValue && sumOfBytesRead >= this.maxReadBytes.Value)
                            {
                                lock (this.monitor)
                                {
                                    this.state = DownloadState.Finished;
                                }

                                this.OnDownloadCompleted(new DownloadEventArgs(this));
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (this.monitor)
                {
                    this.state = DownloadState.Cancelled;
                }

                this.OnDownloadCancelled(new DownloadCancelledEventArgs(this, ex));
            }
        }
    }
}