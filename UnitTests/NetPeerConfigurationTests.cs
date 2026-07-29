using System.Net;
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

        [Test]
        public void ExpandMTUCapsRejectNonPositiveValues()
        {
            var config = new NetPeerConfiguration("Test");

            Assert.Multiple(() =>
            {
                Assert.That(() => config.MaximumExpandedTransmissionUnit = 0, Throws.TypeOf<NetException>());
                Assert.That(() => config.MaximumExpandedTransmissionUnit = -1, Throws.TypeOf<NetException>());
                Assert.That(() => config.MaximumExpandedTransmissionUnitV6 = 0, Throws.TypeOf<NetException>());
                Assert.That(() => config.MaximumExpandedTransmissionUnitV6 = -1, Throws.TypeOf<NetException>());
                Assert.That(() => config.MaximumExpandedTransmissionUnit = 1_000, Throws.Nothing);
                Assert.That(() => config.MaximumExpandedTransmissionUnitV6 = 1_000, Throws.Nothing);
            });
        }

        [Test]
        public void ExpandMTUCapsRespectConfiguredMinimums()
        {
            var config = new NetPeerConfiguration("Test")
            {
                MaximumTransmissionUnit = 700,
                MaximumTransmissionUnitV6 = 1232,
                MaximumExpandedTransmissionUnit = 600,
                MaximumExpandedTransmissionUnitV6 = 1000
            };

            Assert.Multiple(() =>
            {
                Assert.That(config.MaximumExpandedMTUForEndPoint(new IPEndPoint(IPAddress.Loopback, 12345)), Is.EqualTo(700));
                Assert.That(config.MaximumExpandedMTUForEndPoint(new IPEndPoint(IPAddress.IPv6Loopback, 12345)), Is.EqualTo(1232));
            });
        }

        [Test]
        public void ExpandMTULossRollbackSettingsRejectNonPositiveValues()
        {
            var config = new NetPeerConfiguration("Test");

            Assert.Multiple(() =>
            {
                Assert.That(() => config.ExpandMTULossResendThreshold = 0, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTULossResendThreshold = -1, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTULossWindow = 0, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTULossWindow = -1, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTULossWindow = float.NaN, Throws.TypeOf<NetException>());
                Assert.That(() => config.ExpandMTULossWindow = 1, Throws.Nothing);
            });
        }

        [Test]
        public void ConnectionApprovalTimeoutRejectsNonPositiveValues()
        {
            var config = new NetPeerConfiguration("Test");

            Assert.Multiple(() =>
            {
                Assert.That(() => config.ConnectionApprovalTimeout = 0, Throws.TypeOf<NetException>());
                Assert.That(() => config.ConnectionApprovalTimeout = -1, Throws.TypeOf<NetException>());
                Assert.That(() => config.ConnectionApprovalTimeout = 1, Throws.Nothing);
                Assert.That(config.ConnectionApprovalTimeout, Is.EqualTo(1));
            });
        }
    }
}
