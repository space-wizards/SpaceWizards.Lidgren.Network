using System;
using System.Net;
using System.Net.Sockets;
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
		const int configuredMTU = 700;
		var connection = CreateConnection(IPAddress.Loopback, config => config.MaximumTransmissionUnit = configuredMTU);

		connection.InitExpandMTU(NetTime.Now);

		Assert.Multiple(() =>
		{
			Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(configuredMTU));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(configuredMTU));
		});
	}

	[Test]
	public void InitExpandMTUUsesConfiguredIPv6MTUAsLargestKnownSuccess()
	{
		const int configuredMTU = 1_280;
		var connection = CreateConnection(IPAddress.IPv6Loopback, config => config.MaximumTransmissionUnitV6 = configuredMTU);

		connection.InitExpandMTU(NetTime.Now);

		Assert.Multiple(() =>
		{
			Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(configuredMTU));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(configuredMTU));
		});
	}

	[Test]
	public void MTUDiagnosticsExposeExpansionState()
	{
		const int configuredMTU = 700;
		var connection = CreateConnection(IPAddress.Loopback, config => config.MaximumTransmissionUnit = configuredMTU);
		connection.InitExpandMTU(NetTime.Now);
		SetField(connection, "m_expandMTUStatus", GetExpandMTUStatus("InProgress"));
		SetField(connection, "m_smallestFailedMTU", 1_500);
		SetField(connection, "m_lastSentMTUAttemptSize", 1_400);
		SetField(connection, "m_mtuAttemptFails", 2);
		SetField(connection, "m_mtuSendFailures", 3);
		SetField(connection, "m_mtuLossResends", 4);
		SetField(connection, "m_mtuLossRollbacks", 1);

		Assert.Multiple(() =>
		{
			Assert.That(connection.MTUExpansionStatus, Is.EqualTo("InProgress"));
			Assert.That(connection.LargestSuccessfulMTU, Is.EqualTo(configuredMTU));
			Assert.That(connection.SmallestFailedMTU, Is.EqualTo(1_500));
			Assert.That(connection.LastSentMTUAttemptSize, Is.EqualTo(1_400));
			Assert.That(connection.MTUAttemptFailures, Is.EqualTo(2));
			Assert.That(connection.MTUSendFailures, Is.EqualTo(3));
			Assert.That(connection.MTULossResends, Is.EqualTo(4));
			Assert.That(connection.MTULossRollbacks, Is.EqualTo(1));
		});
	}

	[Test]
	public void MessageSizeFailureFallsBackToConfiguredMTU()
	{
		const int configuredMTU = 700;
		var connection = CreateConnection(IPAddress.Loopback, config => config.MaximumTransmissionUnit = configuredMTU);
		connection.InitExpandMTU(NetTime.Now);
		SetField(connection, "m_currentMTU", 1_400);
		SetField(connection, "m_largestSuccessfulMTU", 1_400);
		SetField(connection, "m_expandMTUStatus", GetExpandMTUStatus("Finished"));

		connection.HandleMTUSendFailure(1_401);

		Assert.Multiple(() =>
		{
			Assert.That(connection.CurrentMTU, Is.EqualTo(configuredMTU),
				"Previously the stale expanded MTU remained in use after a socket-level MessageSize failure.");
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(configuredMTU));
			Assert.That(GetField<int>(connection, "m_smallestFailedMTU"), Is.EqualTo(1_401));
		});
	}

	[Test]
	public void ExpandMTUSuccessAtConfiguredCapFinalizesWithoutSendingAnotherProbe()
	{
		var connection = CreateConnection(IPAddress.Loopback, config =>
		{
			config.MaximumTransmissionUnit = 700;
			config.MaximumExpandedTransmissionUnit = 875;
		});
		connection.InitExpandMTU(NetTime.Now);
		SetField(connection, "m_expandMTUStatus", GetExpandMTUStatus("InProgress"));
		SetField(connection, "m_lastSentMTUAttemptSize", 875);

		InvokeHandleExpandMTUSuccess(connection, 875);

		Assert.Multiple(() =>
		{
			Assert.That(connection.CurrentMTU, Is.EqualTo(875));
			Assert.That(GetField<object>(connection, "m_expandMTUStatus"), Is.EqualTo(GetExpandMTUStatus("Finished")));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(875));
		});
	}

	[Test]
	public void ExpansionCapBelowConfiguredMTUDoesNotShrinkMinimum()
	{
		var config = new NetPeerConfiguration(nameof(NetConnectionMTUTests))
		{
			MaximumTransmissionUnit = 700,
			MaximumExpandedTransmissionUnit = 600
		};

		Assert.That(config.MaximumExpandedMTUForEndPoint(new IPEndPoint(IPAddress.Loopback, 12345)), Is.EqualTo(700));
	}

	[Test]
	public void ReliableHoleResendBurstRollsExpandedMTUBackToConfiguredMinimum()
	{
		var connection = CreateConnection(IPAddress.Loopback, config =>
		{
			config.AutoExpandMTU = true;
			config.MaximumTransmissionUnit = 700;
			config.ExpandMTULossResendThreshold = 3;
		});
		var now = NetTime.Now;
		connection.InitExpandMTU(now);
		SetField(connection, "m_currentMTU", 1_000);
		SetField(connection, "m_largestSuccessfulMTU", 1_000);
		SetField(connection, "m_expandMTUStatus", GetExpandMTUStatus("Finished"));

		connection.HandleReliableResendForMTU(MessageResendReason.HoleInSequence, now);
		connection.HandleReliableResendForMTU(MessageResendReason.HoleInSequence, now + 0.1);
		connection.HandleReliableResendForMTU(MessageResendReason.HoleInSequence, now + 0.2);

		Assert.Multiple(() =>
		{
			Assert.That(connection.CurrentMTU, Is.EqualTo(700));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(700));
			Assert.That(GetField<int>(connection, "m_smallestFailedMTU"), Is.EqualTo(1_000));
			Assert.That(GetField<int>(connection, "m_mtuLossResends"), Is.Zero);
			Assert.That(GetField<int>(connection, "m_mtuLossRollbacks"), Is.EqualTo(1));
		});
	}

	[Test]
	public void SingleReliableDelayResendDoesNotRollBackExpandedMTU()
	{
		var connection = CreateConnection(IPAddress.Loopback, config =>
		{
			config.AutoExpandMTU = true;
			config.MaximumTransmissionUnit = 700;
			config.ExpandMTULossResendThreshold = 2;
		});
		var now = NetTime.Now;
		connection.InitExpandMTU(now);
		SetField(connection, "m_currentMTU", 1_000);
		SetField(connection, "m_largestSuccessfulMTU", 1_000);

		connection.HandleReliableResendForMTU(MessageResendReason.Delay, now);

		Assert.Multiple(() =>
		{
			Assert.That(connection.CurrentMTU, Is.EqualTo(1_000));
			Assert.That(GetField<int>(connection, "m_mtuLossRollbacks"), Is.Zero);
		});
	}

	[Test]
	public void ReliableDelayResendBurstRollsExpandedMTUBackToConfiguredMinimum()
	{
		var connection = CreateConnection(IPAddress.Loopback, config =>
		{
			config.AutoExpandMTU = true;
			config.MaximumTransmissionUnit = 700;
			config.ExpandMTULossResendThreshold = 2;
		});
		var now = NetTime.Now;
		connection.InitExpandMTU(now);
		SetField(connection, "m_currentMTU", 1_000);
		SetField(connection, "m_largestSuccessfulMTU", 1_000);

		connection.HandleReliableResendForMTU(MessageResendReason.Delay, now);
		connection.HandleReliableResendForMTU(MessageResendReason.Delay, now + 0.1);

		Assert.Multiple(() =>
		{
			Assert.That(connection.CurrentMTU, Is.EqualTo(700));
			Assert.That(GetField<int>(connection, "m_mtuLossRollbacks"), Is.EqualTo(1));
		});
	}

	[Test]
	public void IPv6DontFragmentCanBeSetOnAutoExpandPlatforms()
	{
		if (!NetNativeSocket.IsWindows && !NetNativeSocket.IsLinux)
			Assert.Ignore("Automatic MTU expansion is only supported on Windows and Linux.");

		using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
		Assert.DoesNotThrow(() =>
		{
			NetNativeSocket.SetIPv6DontFragment(socket, true);
			NetNativeSocket.SetIPv6DontFragment(socket, false);
		});
	}

	[Test]
	public void HandleExpandMTUSuccessIgnoresSuccessWhenExpansionIsNotInProgress()
	{
		const int configuredMTU = 700;
		var connection = CreateConnection(IPAddress.Loopback, config => config.MaximumTransmissionUnit = configuredMTU);
		connection.InitExpandMTU(NetTime.Now);

		InvokeHandleExpandMTUSuccess(connection, 1_400);

		Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(configuredMTU));
	}

	[Test]
	public void HandleExpandMTUSuccessIgnoresUnexpectedProbeSize()
	{
		const int configuredMTU = 700;
		var connection = CreateConnection(IPAddress.Loopback, config => config.MaximumTransmissionUnit = configuredMTU);
		connection.InitExpandMTU(NetTime.Now);
		SetField(connection, "m_expandMTUStatus", GetExpandMTUStatus("InProgress"));
		SetField(connection, "m_lastSentMTUAttemptSize", 1_000);

		InvokeHandleExpandMTUSuccess(connection, 1_200);

		Assert.Multiple(() =>
		{
			Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(configuredMTU));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(configuredMTU));
		});
	}

	[Test]
	public void HandleExpandMTUSuccessIgnoresProtocolOversizedProbeSize()
	{
		const int configuredMTU = 700;
		var connection = CreateConnection(IPAddress.Loopback, config => config.MaximumTransmissionUnit = configuredMTU);
		connection.InitExpandMTU(NetTime.Now);
		SetField(connection, "m_expandMTUStatus", GetExpandMTUStatus("InProgress"));
		SetField(connection, "m_lastSentMTUAttemptSize", NetConstants.MaximumFragmentChunkSize);

		InvokeHandleExpandMTUSuccess(connection, NetConstants.MaximumFragmentChunkSize);

		Assert.Multiple(() =>
		{
			Assert.That(GetField<int>(connection, "m_currentMTU"), Is.EqualTo(configuredMTU));
			Assert.That(GetField<int>(connection, "m_largestSuccessfulMTU"), Is.EqualTo(configuredMTU));
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
