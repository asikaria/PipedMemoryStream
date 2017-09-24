using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PipedMemoryStream;


namespace UnitTests
{
    [TestClass]
    public class MultithreadedTests
    {
        private long dataWritten = 0;
        private long dataRead = 0;

        [TestMethod]
        public void TwoReaderTwoWriterTest()
        {
            var str = new ByteBufferStream(1 * 1024 * 1024);

            Thread writerThread = new Thread(WriterThreadMethod) {Name = "WriterThread2" };
            Thread writerThread2 = new Thread(WriterThreadMethod) { Name = "WriterThread2" };
            Thread clientThread = new Thread(ReaderThreadMethod) { Name = "ReaderThread1" };
            Thread clientThread2 = new Thread(ReaderThreadMethod) { Name = "ReaderThread2" };

            clientThread.Start(str);
            clientThread2.Start(str);
            writerThread.Start(str);
            writerThread2.Start(str);

            writerThread.Join();
            writerThread2.Join();
            str.Dispose();
            clientThread.Join();
            clientThread2.Join();

            Interlocked.Read(ref dataWritten);
            Interlocked.Read(ref dataRead);
            Assert.IsTrue(dataWritten == dataRead, "size comparison: data written ({0}) != data read ({1})", dataWritten, dataRead);
        }


        public void WriterThreadMethod(object o)
        {
            ByteBufferStream p = (ByteBufferStream) o;
            byte[] writeBlock = new byte[8 * 1024];
            for (int i = 0; i < 70; i++)
            {
                p.Write(writeBlock, 0, writeBlock.Length);
                Interlocked.Add(ref dataWritten, writeBlock.Length);
            }
        }

        public void ReaderThreadMethod(object o)
        {
            ByteBufferStream p = (ByteBufferStream) o;

            byte[] readBlock = new byte[8 * 1024];
            int readSize;
            while ((readSize = p.Read(readBlock, 0, readBlock.Length)) != 0)
            {
                Interlocked.Add(ref dataRead, readSize);
            }
        }
    }
}

