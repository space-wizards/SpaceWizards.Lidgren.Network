using Lidgren.Network;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    [Parallelizable]
    [TestOf(typeof(NetPeerConfiguration))]
    public class NetPeerConfigurationTests
    {
        [Test]
        public void TestMessageTypes()
        {
            var config = new NetPeerConfiguration("Test");

            config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            Assert.That(config.IsMessageTypeEnabled(NetIncomingMessageType.UnconnectedData), Is.True);

            config.SetMessageTypeEnabled(NetIncomingMessageType.UnconnectedData, false);
            Assert.That(config.IsMessageTypeEnabled(NetIncomingMessageType.UnconnectedData), Is.False);
        }

        [Test]
        public void ExpandMTUFrequencyRejectsNonPositiveAndNonFiniteValues()
        {
            var config = new NetPeerConfiguration("Test");

            Assert.Multiple(() =>
            {
                Assert.That(() => config.ExpandMTUFrequency = 0, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTUFrequency = -1, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTUFrequency = float.NaN, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTUFrequency = float.PositiveInfinity, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTUFrequency = 0.1f, Throws.Nothing);
                Assert.That(config.ExpandMTUFrequency, Is.EqualTo(0.1f));
            });
        }

        [Test]
        public void ExpandMTUFailAttemptsRejectsNonPositiveValues()
        {
            var config = new NetPeerConfiguration("Test");

            Assert.Multiple(() =>
            {
                Assert.That(() => config.ExpandMTUFailAttempts = 0, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTUFailAttempts = -1, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTUFailAttempts = 1, Throws.Nothing);
                Assert.That(config.ExpandMTUFailAttempts, Is.EqualTo(1));
            });
        }
    }
}
