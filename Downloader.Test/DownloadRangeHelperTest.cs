using System.Collections.Generic;
using System.Linq;

using Toqe.Downloader.Business.Contract;
using Toqe.Downloader.Business.Utils;

namespace Downloader.Test
{
    public class DownloadRangeHelperTest
    {
        private DownloadRangeHelper helper = new DownloadRangeHelper();

        [Fact]
        public void NoIntersection()
        {
            DownloadRange fullRange = new DownloadRange(1, 8);
            DownloadRange range1 = new DownloadRange(9, 3);
            DownloadRange range2 = new DownloadRange(0, 1);

            Assert.False(this.helper.RangesCollide(fullRange, range1));
            Assert.False(this.helper.RangesCollide(fullRange, range2));

            List<DownloadRange> differenceWithRange1 = this.helper.RangeDifference(fullRange, range1);
            List<DownloadRange> differenceWithRange2 = this.helper.RangeDifference(fullRange, range2);

            Assert.Equal(1, differenceWithRange1.Count);
            Assert.Equal(1, differenceWithRange2.Count);
            Assert.Contains(fullRange, differenceWithRange1);
            Assert.Contains(fullRange, differenceWithRange2);
        }

        [Fact]
        public void FullOverlay()
        {
            DownloadRange fullRange = new DownloadRange(1, 8);
            DownloadRange range1 = new DownloadRange(0, 10);
            DownloadRange range2 = new DownloadRange(1, 8);

            Assert.True(this.helper.RangesCollide(fullRange, range1));
            Assert.True(this.helper.RangesCollide(fullRange, range2));

            List<DownloadRange> differenceWithRange1 = this.helper.RangeDifference(fullRange, range1);
            List<DownloadRange> differenceWithRange2 = this.helper.RangeDifference(fullRange, range2);

            Assert.Empty(differenceWithRange1);
            Assert.Empty(differenceWithRange2);
        }

        [Fact]
        public void PartialIntersectionWithOneResult()
        {
            DownloadRange fullRange = new DownloadRange(1, 8);
            DownloadRange range1 = new DownloadRange(0, 5);
            DownloadRange range2 = new DownloadRange(1, 4);
            DownloadRange range3 = new DownloadRange(7, 4);
            DownloadRange range4 = new DownloadRange(8, 4);

            Assert.True(this.helper.RangesCollide(fullRange, range1));
            Assert.True(this.helper.RangesCollide(fullRange, range2));
            Assert.True(this.helper.RangesCollide(fullRange, range3));
            Assert.True(this.helper.RangesCollide(fullRange, range4));

            List<DownloadRange> differenceWithRange1 = this.helper.RangeDifference(fullRange, range1);
            List<DownloadRange> differenceWithRange2 = this.helper.RangeDifference(fullRange, range2);
            List<DownloadRange> differenceWithRange3 = this.helper.RangeDifference(fullRange, range3);
            List<DownloadRange> differenceWithRange4 = this.helper.RangeDifference(fullRange, range4);

            Assert.Equal(1, differenceWithRange1.Count);
            Assert.Equal(1, differenceWithRange2.Count);
            Assert.Equal(1, differenceWithRange3.Count);
            Assert.Equal(1, differenceWithRange4.Count);
            Assert.Contains(new DownloadRange(5, 4), differenceWithRange1);
            Assert.Contains(new DownloadRange(5, 4), differenceWithRange2);
            Assert.Contains(new DownloadRange(1, 6), differenceWithRange3);
            Assert.Contains(new DownloadRange(1, 7), differenceWithRange4);
        }

        [Fact]
        public void PartialIntersectionWithTwoResults()
        {
            DownloadRange fullRange = new DownloadRange(1, 8);
            DownloadRange range1 = new DownloadRange(2, 1);
            DownloadRange range2 = new DownloadRange(3, 3);

            Assert.True(this.helper.RangesCollide(fullRange, range1));
            Assert.True(this.helper.RangesCollide(fullRange, range2));

            List<DownloadRange> differenceWithRange1 = this.helper.RangeDifference(fullRange, range1);
            List<DownloadRange> differenceWithRange2 = this.helper.RangeDifference(fullRange, range2);

            Assert.Equal(2, differenceWithRange1.Count);
            Assert.Equal(2, differenceWithRange2.Count);
            Assert.Contains(new DownloadRange(1, 1), differenceWithRange1);
            Assert.Contains(new DownloadRange(3, 6), differenceWithRange1);
            Assert.Contains(new DownloadRange(1, 2), differenceWithRange2);
            Assert.Contains(new DownloadRange(6, 3), differenceWithRange2);
        }
    }
}