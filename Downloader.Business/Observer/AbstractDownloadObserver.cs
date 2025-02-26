﻿using System;
using System.Collections.Generic;

using Toqe.Downloader.Business.Contract;

namespace Toqe.Downloader.Business.Observer
{
    public abstract class AbstractDownloadObserver : IDownloadObserver, IDisposable
    {
        #region Field

        protected List<IDownload> attachedDownloads = new List<IDownload>();

        protected object monitor = new object();

        #endregion

        #region Public Method

        public void Attach(IDownload download)
        {
            if (download == null)
            {
                throw new ArgumentNullException("download");
            }

            lock (this.monitor)
            {
                this.attachedDownloads.Add(download);
            }

            this.OnAttach(download);
        }

        public void Detach(IDownload download)
        {
            lock (this.monitor)
            {
                this.attachedDownloads.Remove(download);
            }

            this.OnDetach(download);
        }

        public void DetachAll()
        {
            List<IDownload> downloadsCopy;

            lock (this.monitor)
            {
                downloadsCopy = new List<IDownload>(this.attachedDownloads);
            }

            foreach (IDownload download in downloadsCopy)
            {
                this.Detach(download);
            }
        }

        public virtual void Dispose()
        {
            this.DetachAll();
        }

        #endregion

        #region Protected Method

        protected virtual void OnAttach(IDownload download)
        {
        }

        protected virtual void OnDetach(IDownload download)
        {
        }

        #endregion
    }
}