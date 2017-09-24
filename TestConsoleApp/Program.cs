using System;
using PipedMemoryStream;
using System.Net.Security;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using RandomByteStream;

namespace TestConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            SslTest(args);
        }

        public static void SslTest(string[] args)
        {
            var str = new ByteBufferBidirectionalStream(64 * 1024 * 1024);
            Program p = new Program();

            Thread serverThread = new Thread(p.ServerThreadMethod) { Name = "ServerThread" };
            Thread clientThread = new Thread(p.ClientThreadMethod) { Name = "clientThread" };

            clientThread.Start(str);
            serverThread.Start(str);

            serverThread.Join();
            clientThread.Join();

            Console.WriteLine("Remaining bytes in flight = " + Interlocked.Read(ref p._bytesInFlight));

            Console.WriteLine("Completed - press ENTER to continue");
            Console.ReadLine();
        }

        private long _bytesInFlight = 0;

        public void ServerThreadMethod(object o)
        {
            var p = (ByteBufferBidirectionalStream) o;
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " Server Started");
            SslStream serverStream = new SslStream(p.GetServerStream());
            String password = File.ReadAllText(@".\certpassword.txt");
            X509Certificate2 cert = new X509Certificate2(@".\localcert.pfx", password);
            Console.WriteLine("Got cert");
            serverStream.AuthenticateAsServerAsync(cert).GetAwaiter().GetResult();
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " Done authenticating as server");

            byte[] writeBlock = new byte[8 * 1024];
            RandomDataStream sourceData = new RandomDataStream(577 * 1024);
            int readSize;
            int i = 0;
            while ((readSize = sourceData.Read(writeBlock, 0, writeBlock.Length)) != 0)
            {
                serverStream.Write(writeBlock, 0, writeBlock.Length);
                Console.WriteLine("Pass " + i + " write done, size=" + readSize);
                i++;
                Interlocked.Add(ref _bytesInFlight, writeBlock.Length);
            }
            serverStream.Dispose();
            Console.WriteLine("All writes done.");
        }

        public void ClientThreadMethod(object o)
        {
            var p = (ByteBufferBidirectionalStream) o;
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " Client Started");
            SslStream clientStream = new SslStream(p.GetClientStream(), false, TrivialCertificateValidator);
            clientStream.AuthenticateAsClientAsync("localcert").GetAwaiter().GetResult();
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " Client done authenticating");
            byte[] readBlock = new byte[7 * 1024];
            int readSize;
            int i = 0;
            while ((readSize = clientStream.Read(readBlock, 0, readBlock.Length)) != 0)
            {
                Console.WriteLine("Pass " + i + " read done, got " + readSize);
                i++;
                Interlocked.Add(ref _bytesInFlight, -readSize);
            }
            Console.WriteLine("All reads done. Last read was pass " + i + ", readSize=" + readSize);
        }

        public bool TrivialCertificateValidator(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        ) => true;
    }
}