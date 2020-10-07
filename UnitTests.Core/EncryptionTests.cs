using System;
using Lidgren.Network;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class EncryptionTests
    {
        private NetPeer _peer;

        [OneTimeSetUp]
        public void Setup()
        {
            _peer = new NetPeer(new NetPeerConfiguration("unittests"));
            _peer.Start();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _peer.Shutdown("Done");
        }

        [Test]
        [TestCase(typeof(NetXorEncryption))]
        [TestCase(typeof(NetXtea))]
        [TestCase(typeof(NetAESEncryption))]
        [TestCase(typeof(NetRC2Encryption))]
        [TestCase(typeof(NetDESEncryption))]
        [TestCase(typeof(NetTripleDESEncryption))]
        public void TestAlgorithms(Type encryptionType)
        {
            var peer = new NetPeer(new NetPeerConfiguration("unittests"));
            peer.Start();

            var algo = (NetEncryption) Activator.CreateInstance(encryptionType, peer, "TopSecret");

            NetOutgoingMessage om = peer.CreateMessage();
            om.Write("Hallon");
            om.Write(42);
            om.Write(5, 5);
            om.Write(true);
            om.Write("kokos");
            int unencLen = om.LengthBits;
            om.Encrypt(algo);

            // convert to incoming message
            NetIncomingMessage im = Program.CreateIncomingMessage(om.PeekDataBuffer(), om.LengthBits);
            if (im.Data == null || im.Data.Length == 0)
                throw new NetException("bad im!");

            im.Decrypt(algo);

            if (im.Data == null || im.Data.Length == 0 || im.LengthBits != unencLen)
                throw new NetException("Length fail");

            Assert.That(im.ReadString(), Is.EqualTo("Hallon"));
            Assert.That(im.ReadInt32(), Is.EqualTo(42));
            Assert.That(im.ReadInt32(5), Is.EqualTo(5));
            Assert.That(im.ReadBoolean(), Is.EqualTo(true));
            Assert.That(im.ReadString(), Is.EqualTo("kokos"));
        }

        [Test]
        public void TestNetSRP()
        {
            var peer = new NetPeer(new NetPeerConfiguration("unittests"));
            peer.Start();
            for (int i = 0; i < 100; i++)
            {
                byte[] salt = NetSRP.CreateRandomSalt();
                byte[] x = NetSRP.ComputePrivateKey("user", "password", salt);

                byte[] v = NetSRP.ComputeServerVerifier(x);
                //Console.WriteLine("v = " + NetUtility.ToHexString(v));

                byte[]
                    a = NetSRP
                        .CreateRandomEphemeral(); //  NetUtility.ToByteArray("393ed364924a71ba7258633cc4854d655ca4ec4e8ba833eceaad2511e80db2b5");
                byte[] A = NetSRP.ComputeClientEphemeral(a);
                //Console.WriteLine("A = " + NetUtility.ToHexString(A));

                byte[]
                    b = NetSRP
                        .CreateRandomEphemeral(); // NetUtility.ToByteArray("cc4d87a90db91067d52e2778b802ca6f7d362490c4be294b21b4a57c71cf55a9");
                byte[] B = NetSRP.ComputeServerEphemeral(b, v);
                //Console.WriteLine("B = " + NetUtility.ToHexString(B));

                byte[] u = NetSRP.ComputeU(A, B);
                //Console.WriteLine("u = " + NetUtility.ToHexString(u));

                byte[] Ss = NetSRP.ComputeServerSessionValue(A, v, u, b);
                //Console.WriteLine("Ss = " + NetUtility.ToHexString(Ss));

                byte[] Sc = NetSRP.ComputeClientSessionValue(B, x, u, a);
                //Console.WriteLine("Sc = " + NetUtility.ToHexString(Sc));

                Assert.That(Ss, Is.EqualTo(Sc), "SRP non matching session values!");

                NetSRP.CreateEncryption(peer, Ss);
            }
        }
    }
}