using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PipedMemoryStream
{
    /// <summary>
    /// A pair of streams wrapped around a circular byte buffer; useful as full-duplex
    /// communication channel between two threads. The streams are called ServerStream
    /// and ClientStream, but they are fully symmetric - there is nothing different between 
    /// them. The send buffer of wone sytream is wired to the receive buffer of the other 
    /// stream, and vise versa. This enables what is written by one side to be read by the 
    /// other side, similar to a TCP connection. This stream can be used a as substitute
    /// for a TCP streamm, for fast inter-thread client-server communication.
    /// 
    /// Thread safety: It is safe to call all methods of this class concurrently.
    /// Caution: Do not try to use both the streams on the same thread. Since 
    /// the streams are blocking streams, this will result in a deadlock of any pf the 
    /// calls blocks: there will be no other thread to unblock the call.
    /// </summary>
    public class ByteBufferBidirectionalStream
    {
        private const int DefaultMemoryBufferSize = 4 * 1024 * 1024;
        private readonly RxTxStream _stream1;
        private readonly RxTxStream _stream2;

        /// <summary>
        /// Creates the streams with underlying byte buffer of default size (4MB)
        /// </summary>
        public ByteBufferBidirectionalStream() : this(DefaultMemoryBufferSize)
        {
        }

        /// <summary>
        /// Create the streams with underlying byte buffer of specified size
        /// </summary>
        /// <param name="bufferSize"></param>
        public ByteBufferBidirectionalStream(int bufferSize)
        {
            var buffer1 = new CircularByteBuffer(bufferSize);
            var buffer2 = new CircularByteBuffer(bufferSize);
            _stream1 = new RxTxStream(buffer1, buffer2);
            _stream2 = new RxTxStream(buffer2, buffer1);
        }

        /// <summary>
        /// The "server" stream. Note that the server stream is identical to the client stream, 
        /// they are just two streams with their receive and send buffers hooked up so what is 
        /// written by one stream is read by the other and vise versa.
        /// </summary>
        /// <returns>One of the two symmetric streams</returns>
        public Stream GetServerStream()
        {
            return _stream1;
        }

        /// The "server" stream. Note that the client stream is identical to the server stream, 
        /// they are just two streams with their receive and send buffers hooked up so what is 
        /// written by one stream is read by the other and vise versa.
        /// </summary>
        /// <returns>One of the two symmetric streams</returns>
        public Stream GetClientStream()
        {
            return _stream2;
        }

        private class RxTxStream : Stream
        {
            private readonly CircularByteBuffer _txBuffer;
            private readonly CircularByteBuffer _rxBuffer;

            public RxTxStream(CircularByteBuffer txBuffer, CircularByteBuffer rxBuffer)
            {
                _txBuffer = txBuffer;
                _rxBuffer = rxBuffer;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int actualRead = _rxBuffer.Get(buffer, offset, count);
                return actualRead;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                int writeSize = _txBuffer.TotalCapacity;
                while (count > writeSize)
                {
                    _txBuffer.Put(buffer, offset, writeSize);
                    offset += writeSize;
                    count -= writeSize;
                }
                _txBuffer.Put(buffer, offset, count);
            }

            public override void Flush()
            {
                // no-op
            }

            protected override void Dispose(Boolean disposing)
            {
                _txBuffer.Close();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;  // async version of no-op
            }

            // what about ReadAsync and WriteAsync?
            // let the framework default implementation (that wraps Read/Write) take care of it,
            // since the underlying Monitor.Wait() in circularbuffer doesn't have any async version anyway


            public override bool CanRead => true;
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
