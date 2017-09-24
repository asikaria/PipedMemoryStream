using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PipedMemoryStream
{
    /// <summary>
    /// A Stream wrapped around a circular byte buffer. Read() will read the bytes written by Write().
    ///
    /// Thread safety: It is safe to call all methods of this class concurrently.
    /// Caution: Do not try to do both reads and writes on the same thread. Since
    /// the streams are blocking streams, this will result in a deadlock of any of the
    /// calls blocks: there will be no other thread to unblock the call. The only way to
    /// call this on the same thread is if you know your data sizes wont result in
    /// any call blocking.
    /// </summary>
    public class ByteBufferStream : Stream
    {
        private const int DefaultMemoryBufferSize = 4 * 1024 * 1024;
        private readonly CircularByteBuffer _circularByteBuffer;

        /// <summary>
        /// Create a stream with underlying byte buffer of default size (4MB)
        /// </summary>
        public ByteBufferStream() : this(DefaultMemoryBufferSize) { }

        /// <summary>
        /// Create a stream with underlying byte buffer of specified size
        /// </summary>
        /// <param name="pipeMemoryBufferSize"></param>
        public ByteBufferStream(int pipeMemoryBufferSize)
        {
            _circularByteBuffer = new CircularByteBuffer(pipeMemoryBufferSize);
        }

        internal ByteBufferStream(CircularByteBuffer buf)
        {
            _circularByteBuffer = buf;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            int actualRead = _circularByteBuffer.Get(buffer, offset, count);
            return actualRead;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            int writeSize = _circularByteBuffer.TotalCapacity;
            while (count > writeSize)
            {
                _circularByteBuffer.Put(buffer, offset, writeSize);
                offset += writeSize;
                count -= writeSize;
            }
            _circularByteBuffer.Put(buffer, offset, count);
        }

        /// <inheritdoc />
        public override void Flush()
        {
            // no-op
        }

        /// <inheritdoc />
        protected override void Dispose(Boolean disposing)
        {
            _circularByteBuffer.Close();
        }

        /// <summary>
        /// No-op
        /// </summary>
        /// <param name="cancellationToken">not used</param>
        /// <returns></returns>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;  // async version of no-op
        }

        // what about ReadAsync and WriteAsync?
        // let the framework default implementation (that wraps Read/Write) take care of it,
        // since the underlying Monitor.Wait() in circularbuffer doesn't have any async version anyway

        /// <summary>
        /// Returns the number of unconsumed bytes currently in the stream
        /// </summary>
        public int BytesCurrentlyInStream => _circularByteBuffer.OccupiedSize;

        /// <summary>Gets a value indicating whether the current stream supports reading (true always).</summary>
        /// <returns>true always,</returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanRead => true;

        /// <summary>Gets a value indicating whether the current stream supports seeking (false always).</summary>
        /// <returns>false</returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanSeek => false;

        /// <summary>Gets a value indicating whether the current stream supports writing (true always).</summary>
        /// <returns>true</returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException("Stream does not support Length property");
        public override long Position {
            get => throw new NotSupportedException("Stream does not support Position property");
            set => throw new NotSupportedException("Stream does not support Position property");
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Stream does not support Seek");
        public override void SetLength(long value) => throw new NotSupportedException("Stream does not support SetLength");
    }
}
