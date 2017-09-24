using System;
using System.Threading;

namespace PipedMemoryStream
{
    /// <summary>
    /// Provides a blocking concurrent circular byte buffer. 
    /// Has methods to Put and Get byte-buffers of specified sizes from the buffer.
    /// 
    /// Thread safety: It is safe to call all methods of this class concurrently.
    /// </summary>
    public class CircularByteBuffer
    {
        private readonly byte[] _buffer;
        private readonly int _size;
        private int _bottom = 0; // offset of first byte present in buffer
        private int _top = 0;  // offset of next byte to write
        private bool _closed = false; 
        private readonly object _lockObj = new Object();

        /// <summary>
        /// Create a buffer with specified size.
        /// </summary>
        /// <param name="bufferSize">desired size of the buffer</param>
        public CircularByteBuffer(int bufferSize)
        {
            if (bufferSize <=0) throw new ArgumentException("illegal buffer size requested");
            _size = bufferSize;
            _buffer = new byte[_size];
        }

        /// <summary>
        /// A range of bytes to read or write
        /// </summary>
        private class Range
        {
            public readonly int Offset;
            public readonly int Length;

            public Range(int offset, int length)
            {
                Offset = offset;
                Length = length;
            }
        }

        /// <summary>
        /// Put the specifie buffer into the circular buffer. This method blocks if there isn't enough space in 
        /// the buffer, until space becomes available (because of a reader consuming the bytes).
        /// </summary>
        /// <param name="b">The byte-array contents to put into the circular buffer</param>
        /// <param name="offset">offset within the byte-array to start from</param>
        /// <param name="len">number of bytes to put. Cannot be larger than buffer size.</param>
        public void Put(byte[] b, int offset, int len)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (len > _size) throw new ArgumentException("len is bigger than buffer size");
            if (offset + len > b.Length) throw new ArgumentException("requested byte range size exceeds buffer size");
            if (len < 0) throw new ArgumentOutOfRangeException(nameof(len));

            lock (_lockObj)
            {
                if (_closed) throw new ObjectDisposedException("stream is closed");
                while (len > AvailableEmptySpace)  // if not enough space is available in the buffer, then wait for space
                {
                    Monitor.Wait(_lockObj);
                }
                Tuple<Range, Range> ranges = GetWriteRanges(len);
                Buffer.BlockCopy(b, offset, _buffer, ranges.Item1.Offset, ranges.Item1.Length);
                if (ranges.Item2 != null)
                {
                    Buffer.BlockCopy(b, offset + ranges.Item1.Length, _buffer, ranges.Item2.Offset, ranges.Item2.Length);
                }
                _top += len;
                Monitor.PulseAll(_lockObj);
            }
        }

        /// <summary>
        /// Gets the requested number of bytes from the buffer. The method may return less than the requested 
        /// number of bytes. If there are no bytes in the stream, then the method blocks until bytes become available
        /// (because a writer wrote to tbe buffer).
        /// If the stream is closed for writes and all bytes have been consumed, then this method returns 0. In other words,
        /// a return value of zero can be considered analogous to End-Of-File.
        /// </summary>
        /// <param name="b">The byte-array that the read bytes will be written into</param>
        /// <param name="offset">The offset in the byte array to srart writing</param>
        /// <param name="len">Maximum number of bytes to read (may read less)</param>
        /// <returns></returns>
        public int Get(byte[] b, int offset, int len)
        {
            if (b == null) throw new ArgumentException("requested buffer is null");
            if (offset + len > b.Length) throw new ArgumentException("requested byte range size exceeds buffer size");
            if (offset < 0) throw new ArgumentOutOfRangeException("illegal offset: requested offset is less than 0");
            if (len <= 0) throw new ArgumentOutOfRangeException("illegal length: requested length is less than or equal to 0");
            int bytesActuallyRead = 0;

            lock (_lockObj)
            {
                while (OccupiedSize == 0)
                {
                    if (_closed)
                    {
                        return 0; // EOF
                    }
                    else
                    {
                        Monitor.Wait(_lockObj);
                    }
                }
                Tuple<Range, Range> ranges = GetReadRanges(len);
                Buffer.BlockCopy(_buffer, ranges.Item1.Offset, b, offset, ranges.Item1.Length);
                bytesActuallyRead += ranges.Item1.Length;
                if (ranges.Item2 != null)
                {
                    Buffer.BlockCopy(_buffer, ranges.Item2.Offset, b, offset + ranges.Item1.Length, ranges.Item2.Length);
                    bytesActuallyRead += ranges.Item2.Length;
                }
                _bottom += bytesActuallyRead;
                Monitor.PulseAll(_lockObj);
            }
            return bytesActuallyRead;
        }

        /// <summary>
        /// Prevents further writes. Reads will continue to work until all the written bytes are consumed.
        /// </summary>
        public void Close()
        {
            lock (_lockObj)
            {
                _closed = true;
                Monitor.PulseAll(_lockObj);
            }
        }

        /// <summary>
        /// The number of bytes currently in the buffer.
        /// </summary>
        public int OccupiedSize
        {
            get { lock (_lockObj) {return _top - _bottom;} }
        }

        /// <summary>
        /// The number of bytes the buffer can takem before becoming full
        /// </summary>
        public int AvailableEmptySpace
        {
            get { lock (_lockObj) { return _size - (_top - _bottom);} } // buffer size minus occupied size
        }

        /// <summary>
        /// The total capacity of the buffer. The value is the same as was specified in the constructor 
        /// when the buffer was created.
        /// </summary>
        public int TotalCapacity => _size;

        private int TopOffset => _top % _size;
        private int BottomOffset => _bottom % _size;

        /// <summary>
        /// Gets the ranges of bytes that a write should go to. Since this is a circular buffer, 
        /// writes may go to the end of the buffer and then wrap around to the beginning, in which case
        /// a given write will be split into two ranges
        /// </summary>
        /// <param name="length">the number of bytes to write</param>
        /// <returns></returns>
        private Tuple<Range, Range> GetWriteRanges(int length)
        {
            Range range1;
            Range range2 = null;

            if (TopOffset < BottomOffset)
            {
                range1 = new Range(TopOffset, length); // assume this is always called with enough space for length
            }
            else
            {
                int firstRangeSize = Math.Min(length, _size - TopOffset); 
                range1 = new Range(TopOffset, firstRangeSize);
                if (firstRangeSize < length)  // if firstRange alone was not enough for the full length
                {
                    range2 = new Range(0, length - firstRangeSize);
                }
            }
            return new Tuple<Range, Range>(range1, range2);
        }

        /// <summary>
        /// Gets the ranges of bytes that a read should read from. Since this is a circular buffer, 
        /// bytes may go to the end of the buffer and then wrap around to the beginning, in which case
        /// a given read will be map tp two ranges to read from
        /// </summary>
        /// <param name="length">the number of bytes to read</param>
        /// <returns></returns>
        private Tuple<Range, Range> GetReadRanges(int length)
        {
            Range range1;
            Range range2 = null;
            length = Math.Min(length, OccupiedSize);

            if (TopOffset > BottomOffset)
            {
                range1 = new Range(BottomOffset, length); 
            }
            else
            {
                int firstRangeSize = Math.Min(length, _size - BottomOffset);
                range1 = new Range(BottomOffset, firstRangeSize);
                if (firstRangeSize < length)
                {
                    range2 = new Range(0, length - firstRangeSize);
                }
            }
            return new Tuple<Range, Range>(range1, range2);
        }
    }
}
