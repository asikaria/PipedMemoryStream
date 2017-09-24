using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PipedMemoryStream
{
    /// <summary>
    /// A pair of unidirectional streams wrapped around a circular byte buffer; useful as a pipe
    /// between two threads.
    /// 
    /// Thread safety: It is safe to call all methods of this class concurrently.
    /// Caution: Do not try to use both streams on the same thread. Since 
    /// the streams are blocking streams, this will result in a deadlock of any pf the 
    /// calls blocks: there will be no other thread to unblock the call.
    /// </summary>
    public class ByteBufferUnidirectionalStream
    {
        private const int DefaultMemoryBufferSize = 4 * 1024 * 1024;
        private readonly PipedMemoryInputStream _inputStream;
        private readonly PipedMemoryOutputStream _outputStream;

        /// <summary>
        /// Create the streams with underlying byte buffer of default size (4MB)
        /// </summary>
        public ByteBufferUnidirectionalStream() : this(DefaultMemoryBufferSize) { }

        /// <summary>
        /// Create the streams with underlying byte buffer of specified size
        /// </summary>
        /// <param name="pipeMemoryBufferSize"></param>
        public ByteBufferUnidirectionalStream(int pipeMemoryBufferSize)
        {
            var circularByteBuffer = new CircularByteBuffer(pipeMemoryBufferSize);
            _inputStream = new PipedMemoryInputStream(circularByteBuffer);
            _outputStream = new PipedMemoryOutputStream(circularByteBuffer);
        }

        /// <summary>
        /// Gets the sender stream of the pipe. You can only write to this stream, not read from it.
        /// </summary>
        /// <returns></returns>
        public Stream GetSenderStream() => _outputStream;

        /// <summary>
        /// Gets the receiver stream of the pipe. You can only read from this stream, not write to it.
        /// </summary>
        /// <returns></returns>
        public Stream GetReceiverStream() => _inputStream;



        private class PipedMemoryInputStream : Stream
        {
            private readonly CircularByteBuffer _circularByteBuffer;

            public PipedMemoryInputStream(CircularByteBuffer buffer)
            {
                _circularByteBuffer = buffer;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int actualRead = _circularByteBuffer.Get(buffer, offset, count);
                return actualRead;
            }

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Cannot write to Input Stream");
            public override void Flush() => throw new NotSupportedException("Cannot flush Input Stream");

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException("Stream does not support Length property");
            public override long Position
            {
                get => throw new NotSupportedException("Stream does not support Position property");
                set => throw new NotSupportedException("Stream does not support Position property");
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Stream does not support Seek");
            public override void SetLength(long value) => throw new NotSupportedException("Stream does not support SetLength");
        }
        
        private class PipedMemoryOutputStream : Stream
        {
            private readonly CircularByteBuffer _circularByteBuffer;

            public PipedMemoryOutputStream(CircularByteBuffer buffer)
            {
                _circularByteBuffer = buffer;
            }

            public override int Read(byte[] buffer, int offset, int count) =>  throw new NotSupportedException("Cannot read from Input Stream");
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

            public override void Flush()
            {
                // no-op
            }

            protected override void Dispose(Boolean disposing)
            {
                _circularByteBuffer.Close();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;  // async version of no-op
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException("Stream does not support Length property");
            public override long Position
            {
                get => throw new NotSupportedException("Stream does not support Position property");
                set => throw new NotSupportedException("Stream does not support Position property");
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Stream does not support Seek");
            public override void SetLength(long value) => throw new NotSupportedException("Stream does not support SetLength");
        }
    }
}
