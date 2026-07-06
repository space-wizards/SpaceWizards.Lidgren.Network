using System;
using System.Net;
using System.Reflection;
using System.Threading;
using Lidgren.Network;
using NUnit.Framework;

namespace UnitTests;

[TestFixture]
[TestOf(typeof(NetPeer))]
public sealed class NetNatIntroductionTests
{
	[Test]
	public void HandleNatIntroductionRejectsTruncatedPayload()
	{
		var peer = CreatePeer();
		MarkCurrentThreadAsNetworkThread(peer);
		peer.m_receiveBuffer[0] = 0;

		Assert.DoesNotThrow(() => peer.HandleNatIntroduction(0, 1));
		Assert.That(peer.m_unsentUnconnectedMessages.Count, Is.Zero);
	}

	[Test]
	public void HandleNatPunchRejectsTruncatedToken()
	{
		var peer = CreatePeer();
		MarkCurrentThreadAsNetworkThread(peer);
		peer.m_receiveBuffer[0] = 0;
		peer.m_receiveBuffer[1] = 5;

		Assert.DoesNotThrow(() => InvokeNatHandler(peer, "HandleNatPunch", 0, 2, new IPEndPoint(IPAddress.Loopback, 12345)));
		Assert.That(peer.m_unsentUnconnectedMessages.Count, Is.Zero);
	}

	[Test]
	public void HandleNatIntroductionUsesPayloadLengthForValidMessage()
	{
		var peer = CreatePeer();
		MarkCurrentThreadAsNetworkThread(peer);
		var message = peer.CreateMessage();
		message.Write((byte)0);
		message.Write(new IPEndPoint(IPAddress.Loopback, 1111));
		message.Write(new IPEndPoint(IPAddress.Loopback, 2222));
		message.Write("token");
		Buffer.BlockCopy(message.Data, 0, peer.m_receiveBuffer, 0, message.LengthBytes);

		peer.HandleNatIntroduction(0, message.LengthBytes);

		Assert.That(peer.m_unsentUnconnectedMessages.Count, Is.EqualTo(2));
	}

	private static NetPeer CreatePeer()
	{
		var config = new NetPeerConfiguration(nameof(NetNatIntroductionTests));
		config.EnableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
		return new NetPeer(config);
	}

	private static void InvokeNatHandler(NetPeer peer, string methodName, int ptr, int payloadLength, IPEndPoint senderEndPoint)
	{
		var method = typeof(NetPeer).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, $"{methodName} is missing.");
		method!.Invoke(peer, new object[] { ptr, payloadLength, senderEndPoint });
	}

	private static void MarkCurrentThreadAsNetworkThread(NetPeer peer)
	{
		var field = typeof(NetPeer).GetField("m_networkThread", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null, "NetPeer.m_networkThread is missing.");
		field!.SetValue(peer, Thread.CurrentThread);
	}
}
