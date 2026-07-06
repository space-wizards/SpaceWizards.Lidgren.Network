using System;
using System.Net;
using System.Reflection;
using System.Threading;
using Lidgren.Network;
using NUnit.Framework;

namespace UnitTests;

[TestFixture]
[TestOf(typeof(NetConnection))]
public sealed class NetConnectionMTUTests
{
	[Test]
	public void InitExpandMTUUsesConfiguredMTUAsLargestKnownSuccess()
	{
		var connection = CreateConnection(IPAddress.Loopback);

		connection.InitExpandMTU(NetTime.Now);

		Assert.Multiple(() =>
		{
			Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(NetPeerConfiguration.kDefaultMTU));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(NetPeerConfiguration.kDefaultMTU));
		});
	}

	[Test]
	public void InitExpandMTUUsesConfiguredIPv6MTUAsLargestKnownSuccess()
	{
		var connection = CreateConnection(IPAddress.IPv6Loopback);

		connection.InitExpandMTU(NetTime.Now);

		Assert.Multiple(() =>
		{
			Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(NetPeerConfiguration.kDefaultMTUV6));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(NetPeerConfiguration.kDefaultMTUV6));
		});
	}

	[Test]
	public void HandleExpandMTUSuccessIgnoresSuccessWhenExpansionIsNotInProgress()
	{
		var connection = CreateConnection(IPAddress.Loopback);
		connection.InitExpandMTU(NetTime.Now);

		InvokeHandleExpandMTUSuccess(connection, 1_400);

		Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(NetPeerConfiguration.kDefaultMTU));
	}

	[Test]
	public void HandleExpandMTUSuccessIgnoresUnexpectedProbeSize()
	{
		var connection = CreateConnection(IPAddress.Loopback);
		connection.InitExpandMTU(NetTime.Now);
		SetField(connection, "m_expandMTUStatus", GetExpandMTUStatus("InProgress"));
		SetField(connection, "m_lastSentMTUAttemptSize", 1_000);

		InvokeHandleExpandMTUSuccess(connection, 1_200);

		Assert.Multiple(() =>
		{
			Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(NetPeerConfiguration.kDefaultMTU));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(NetPeerConfiguration.kDefaultMTU));
		});
	}

	[Test]
	public void HandleExpandMTUSuccessIgnoresProtocolOversizedProbeSize()
	{
		var connection = CreateConnection(IPAddress.Loopback);
		connection.InitExpandMTU(NetTime.Now);
		SetField(connection, "m_expandMTUStatus", GetExpandMTUStatus("InProgress"));
		SetField(connection, "m_lastSentMTUAttemptSize", NetConstants.MaximumFragmentChunkSize);

		InvokeHandleExpandMTUSuccess(connection, NetConstants.MaximumFragmentChunkSize);

		Assert.Multiple(() =>
		{
			Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(NetPeerConfiguration.kDefaultMTU));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(NetPeerConfiguration.kDefaultMTU));
		});
	}

	[Test]
	public void SendMTUSuccessIgnoresInvalidRequestSizeBeforeSending()
	{
		var connection = CreateConnection(IPAddress.Loopback);

		Assert.DoesNotThrow(() => InvokeSendMTUSuccess(connection, 0));
		Assert.DoesNotThrow(() => InvokeSendMTUSuccess(connection, NetConstants.MaximumFragmentChunkSize));
	}

	[Test]
	public void ReceivedLibraryMessageIgnoresMalformedExpandMTUSuccessPayload()
	{
		var connection = CreateConnection(IPAddress.Loopback, config => config.AutoExpandMTU = true);
		MarkCurrentThreadAsNetworkThread(connection.m_peer);

		Assert.DoesNotThrow(() => connection.ReceivedLibraryMessage(NetMessageType.ExpandMTUSuccess, 0, 3));
	}

	[Test]
	public void ReceivedLibraryMessageIgnoresAcknowledgePayloadWithTrailingBytes()
	{
		var connection = CreateConnection(IPAddress.Loopback);
		MarkCurrentThreadAsNetworkThread(connection.m_peer);
		connection.m_peer.m_receiveBuffer[0] = (byte)NetMessageType.UserReliableUnordered;
		connection.m_peer.m_receiveBuffer[1] = 1;
		connection.m_peer.m_receiveBuffer[2] = 0;
		connection.m_peer.m_receiveBuffer[3] = 0xff;

		connection.ReceivedLibraryMessage(NetMessageType.Acknowledge, 0, 4);

		Assert.That(connection.m_queuedIncomingAcks.Count, Is.Zero);
	}

	[Test]
	public void ReceivedLibraryMessageIgnoresAcknowledgeWithInvalidMessageType()
	{
		var connection = CreateConnection(IPAddress.Loopback);
		MarkCurrentThreadAsNetworkThread(connection.m_peer);
		connection.m_peer.m_receiveBuffer[0] = (byte)NetMessageType.Acknowledge;
		connection.m_peer.m_receiveBuffer[1] = 1;
		connection.m_peer.m_receiveBuffer[2] = 0;

		connection.ReceivedLibraryMessage(NetMessageType.Acknowledge, 0, 3);

		Assert.That(connection.m_queuedIncomingAcks.Count, Is.Zero);
	}

	private static NetConnection CreateConnection(IPAddress address, Action<NetPeerConfiguration>? configure = null)
	{
		var config = new NetPeerConfiguration(nameof(NetConnectionMTUTests));
		configure?.Invoke(config);
		var peer = new NetPeer(config);
		return new NetConnection(peer, new IPEndPoint(address, 12345));
	}

	private static void InvokeHandleExpandMTUSuccess(NetConnection connection, int size)
	{
		var method = typeof(NetConnection).GetMethod("HandleExpandMTUSuccess", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, "NetConnection.HandleExpandMTUSuccess is missing.");
		method!.Invoke(connection, new object[] { NetTime.Now, size });
	}

	private static void InvokeSendMTUSuccess(NetConnection connection, int size)
	{
		var method = typeof(NetConnection).GetMethod("SendMTUSuccess", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, "NetConnection.SendMTUSuccess is missing.");
		method!.Invoke(connection, new object[] { size });
	}

	private static void InvokeSendExpandMTU(NetConnection connection, double now, int size)
	{
		var method = typeof(NetConnection).GetMethod("SendExpandMTU", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, "NetConnection.SendExpandMTU is missing.");
		method!.Invoke(connection, new object[] { now, size });
	}

	private static void InitializePools(NetPeer peer)
	{
		var method = typeof(NetPeer).GetMethod("InitializePools", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, "NetPeer.InitializePools is missing.");
		method!.Invoke(peer, Array.Empty<object>());
	}

	private static int GetOutgoingMessagePoolCount(NetPeer peer)
	{
		var field = typeof(NetPeer).GetField("m_outgoingMessagesPool", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, "NetPeer.m_outgoingMessagesPool is missing.");
		var pool = field!.GetValue(peer);
		Assert.That(pool, Is.Not.Null);
		return (int)pool!.GetType().GetProperty("Count")!.GetValue(pool)!;
	}

	private static object GetExpandMTUStatus(string name)
	{
		var type = typeof(NetConnection).GetNestedType("ExpandMTUStatus", BindingFlags.NonPublic);
		Assert.That(type, Is.Not.Null, "NetConnection.ExpandMTUStatus is missing.");
		return Enum.Parse(type!, name);
	}

	private static void MarkCurrentThreadAsNetworkThread(NetPeer peer)
	{
		var field = typeof(NetPeer).GetField("m_networkThread", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, "NetPeer.m_networkThread is missing.");
		field!.SetValue(peer, Thread.CurrentThread);
	}

	private static T GetField<T>(NetConnection connection, string name)
	{
		var field = typeof(NetConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"{name} is missing.");
		return (T)field!.GetValue(connection)!;
	}

	private static void SetField(NetConnection connection, string name, object value)
	{
		var field = typeof(NetConnection).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, $"{name} is missing.");
		field!.SetValue(connection, value);
	}
}
