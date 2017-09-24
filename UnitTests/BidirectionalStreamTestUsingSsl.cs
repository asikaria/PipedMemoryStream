using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using PipedMemoryStream;
using System.Net.Security;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using RandomByteStream;

namespace UnitTests
{
    [TestClass]
    public class BidirectionalStreamTestUsingSsl
    {
        /// <summary>
        /// SSL connection makes for a great test tool - integrity check is built into SSL. 
        /// So the fact that SSL delivers the data at all to you on the receiver side means that 
        /// the data was identical to what was sent. Also, SSL negotiation does bi-directional 
        /// communication, so just doing the SSL handshake means that bi-directional communication
        /// is working. The only thing remaining to do on the test side is to check and make sure 
        /// all the bytes did make it through to the other side.
        /// </summary>
        [TestMethod]
        public void SslTest()
        {
            var str = new ByteBufferBidirectionalStream(4 * 1024);  // test with small buffer

            Thread serverThread = new Thread(ServerThreadMethod) { Name = "ServerThread" };
            Thread clientThread = new Thread(ClientThreadMethod) { Name = "clientThread" };
            clientThread.Start(str);
            serverThread.Start(str);

            serverThread.Join();
            clientThread.Join();

            Assert.IsTrue(Interlocked.Read(ref _bytesInFlight) == 0, "SSL Stream's net bytes remaining != 0");
        }

        private long _bytesInFlight = 0;

        public void ServerThreadMethod(object o)
        {
            var p = (ByteBufferBidirectionalStream)o;
            SslStream serverStream = new SslStream(p.GetServerStream());
            String password = File.ReadAllText(@".\certpassword.txt");
            X509Certificate2 cert = new X509Certificate2(@".\localcert.pfx", password);
            serverStream.AuthenticateAsServerAsync(cert).GetAwaiter().GetResult();

            byte[] writeBlock = new byte[8 * 1024];
            RandomDataStream sourceData = new RandomDataStream(577 * 1024);
            int readSize;
            while ((readSize = sourceData.Read(writeBlock, 0, writeBlock.Length)) != 0)
            {
                serverStream.Write(writeBlock, 0, readSize);
                Interlocked.Add(ref _bytesInFlight, readSize);
            }
            serverStream.Dispose();
        }

        public void ClientThreadMethod(object o)
        {
            var p = (ByteBufferBidirectionalStream)o;
            SslStream clientStream = new SslStream(p.GetClientStream(), false, TrivialCertificateValidator);
            clientStream.AuthenticateAsClientAsync("localcert").GetAwaiter().GetResult();
            byte[] readBlock = new byte[7 * 1024];
            int readSize;
            while ((readSize = clientStream.Read(readBlock, 0, readBlock.Length)) != 0)
            {
                Interlocked.Add(ref _bytesInFlight, -readSize);
            }
        }

        public bool TrivialCertificateValidator(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        ) => true;


    }
}
