using Microsoft.VisualStudio.TestTools.UnitTesting;

using PipedMemoryStream;



namespace UnitTests
{
    [TestClass]
    public class SingleThreadedTests
    {
        [TestMethod]
        public void TestSerialWrites()
        {
            var str = new ByteBufferStream(16 * 1024);
            byte[] writeBlock = new byte[8 * 1024];
            byte[] readBlock = new byte[7 * 1024];
            int detritusSize = 0;
            for (int i = 0; i < 73; i++)
            {
                str.Write(writeBlock, 0, writeBlock.Length);
                detritusSize += writeBlock.Length;
                Assert.IsTrue(detritusSize == str.BytesCurrentlyInStream, "expect size ({0}) does not match actual size ({1})", detritusSize, str.BytesCurrentlyInStream);
                detritusSize -= str.Read(readBlock, 0, readBlock.Length);
                Assert.IsTrue(detritusSize == str.BytesCurrentlyInStream, "expect size ({0}) does not match actual size ({1})", detritusSize, str.BytesCurrentlyInStream);
                while (detritusSize >= readBlock.Length)
                {
                    detritusSize -= str.Read(readBlock, 0, readBlock.Length);
                    Assert.IsTrue(detritusSize == str.BytesCurrentlyInStream, "expect size ({0}) does not match actual size ({1})", detritusSize, str.BytesCurrentlyInStream);
                }
            }
            Assert.IsTrue(detritusSize == str.BytesCurrentlyInStream, "expect size ({0}) does not match actual size ({1})", detritusSize, str.BytesCurrentlyInStream);
        }
    }
}
