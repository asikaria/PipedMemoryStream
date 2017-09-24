using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PipedMemoryStream;


namespace UnitTests
{
    [TestClass]
    public class UnisirectionalStreamTests
    {
        private long dataWritten = 0;
        private long dataRead = 0;

        [TestMethod]
        public void UnidirectionalStreamTest()
        {
            var str = new ByteBufferUnidirectionalStream(1 * 1024 * 1024);

            Thread writerThread = new Thread(WriterThreadMethod) { Name = "WriterThread2" };
            Thread writerThread2 = new Thread(WriterThreadMethod) { Name = "WriterThread2" };
            Thread clientThread = new Thread(ReaderThreadMethod) { Name = "ReaderThread1" };
            Thread clientThread2 = new Thread(ReaderThreadMethod) { Name = "ReaderThread2" };

            clientThread.Start(str);
            clientThread2.Start(str);
            writerThread.Start(str);
            writerThread2.Start(str);

            writerThread.Join();
            writerThread2.Join();
            str.GetSenderStream().Dispose();
            clientThread.Join();
            clientThread2.Join();

            Interlocked.Read(ref dataWritten);
            Interlocked.Read(ref dataRead);
            Assert.IsTrue(dataWritten == dataRead, "size comparison: data written ({0}) != data read ({1})", dataWritten, dataRead);
        }


        public void WriterThreadMethod(object o)
        {
            ByteBufferUnidirectionalStream p = (ByteBufferUnidirectionalStream)o;
            Stream writerStream = p.GetSenderStream();
            byte[] writeBlock = new byte[8 * 1024];
            for (int i = 0; i < 70; i++)
            {
                writerStream.Write(writeBlock, 0, writeBlock.Length);
                Interlocked.Add(ref dataWritten, writeBlock.Length);
            }
        }

        public void ReaderThreadMethod(object o)
        {
            ByteBufferUnidirectionalStream p = (ByteBufferUnidirectionalStream)o;
            Stream readerStream = p.GetReceiverStream();
            byte[] readBlock = new byte[8 * 1024];
            int readSize;
            while ((readSize = readerStream.Read(readBlock, 0, readBlock.Length)) != 0)
            {
                Interlocked.Add(ref dataRead, readSize);
            }
        }
    }
}

