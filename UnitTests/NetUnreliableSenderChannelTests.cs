using System;
using System.Net;
using System.Reflection;
using Lidgren.Network;
using NUnit.Framework;

namespace UnitTests;

[TestFixture]
[TestOf(typeof(NetUnreliableSenderChannel))]
public sealed class NetUnreliableSenderChannelTests
{
	[Test]
	public void IgnoreMTUFragmentsMessageThatCannotFitUnfragmentedHeader()
	{
		var (peer, connection) = CreateConnectedPeer(NetUnreliableSizeBehaviour.IgnoreMTU);
		var message = CreateMessage(peer, 8192);

		var result = peer.SendMessage(message, connection, NetDeliveryMethod.Unreliable);

		Assert.Multiple(() =>
		{
			Assert.That(result, Is.EqualTo(NetSendResult.Sent),
				"Previously IgnoreMTU dropped a byte-aligned payload above the 16-bit bit-length limit.");
			Assert.That(message.m_fragmentGroup, Is.GreaterThan(0));
		});
	}

	[Test]
	public void IgnoreMTUPreservesSingleDatagramBehaviorWithinHeaderLimit()
	{
		var (peer, connection) = CreateConnectedPeer(NetUnreliableSizeBehaviour.IgnoreMTU);
		var message = CreateMessage(peer, 8191);

		var result = peer.SendMessage(message, connection, NetDeliveryMethod.Unreliable);

		Assert.Multiple(() =>
		{
			Assert.That(result, Is.EqualTo(NetSendResult.Sent));
			Assert.That(message.m_fragmentGroup, Is.Zero);
		});
	}

	[Test]
	public void DropAboveMTUUsesEncodedPacketSize()
	{
		var (peer, connection) = CreateConnectedPeer(NetUnreliableSizeBehaviour.DropAboveMTU);
		var message = CreateMessage(peer,
			connection.CurrentMTU - NetConstants.UnfragmentedMessageHeaderSize + 1);

		var result = peer.SendMessage(message, connection, NetDeliveryMethod.Unreliable);

		Assert.Multiple(() =>
		{
			Assert.That(message.LengthBytes, Is.LessThanOrEqualTo(connection.CurrentMTU),
				"The previous payload-only comparison allowed this over-MTU packet.");
			Assert.That(message.GetEncodedSize(), Is.GreaterThan(connection.CurrentMTU));
			Assert.That(result, Is.EqualTo(NetSendResult.Dropped));
		});
	}

	private static (NetPeer Peer, NetConnection Connection) CreateConnectedPeer(NetUnreliableSizeBehaviour sizeBehaviour)
	{
		var config = new NetPeerConfiguration(nameof(NetUnreliableSenderChannelTests))
		{
			UnreliableSizeBehaviour = sizeBehaviour
		};
		var peer = new NetPeer(config);
		InitializePools(peer);
		var connection = new NetConnection(peer, new IPEndPoint(IPAddress.Loopback, 12345));
		connection.InitExpandMTU(NetTime.Now);
		connection.m_status = NetConnectionStatus.Connected;
		return (peer, connection);
	}

	private static NetOutgoingMessage CreateMessage(NetPeer peer, int byteCount)
	{
		var message = peer.CreateMessage(byteCount);
		message.Zero(byteCount);
		return message;
	}

	private static void InitializePools(NetPeer peer)
	{
		var method = typeof(NetPeer).GetMethod("InitializePools", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null);
		method!.Invoke(peer, Array.Empty<object>());
	}
}
