using System.Collections.Generic;
using System.Net;
using Lidgren.Network;
using NUnit.Framework;

namespace UnitTests;

[TestFixture]
[TestOf(typeof(NetPeer))]
public sealed class NetPeerLogRateLimiterTests
{
	[Test]
	public void RateLimitedWarningSuppressesAfterBurst()
	{
		var logs = new List<string>();
		var config = CreateConfig();
		config.LogRateLimitBurst = 2;
		config.LogRateLimitWindow = 60.0f;
		var peer = CreatePeer(config, logs);
		var endpoint = new IPEndPoint(IPAddress.Loopback, 6767);

		for (var i = 0; i < 5; i++)
			peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedPacket, endpoint, "malformed packet");

		Assert.That(logs, Has.Count.EqualTo(2));
	}

	[Test]
	public void RateLimiterCanBeDisabled()
	{
		var logs = new List<string>();
		var config = CreateConfig();
		config.LogRateLimiterEnabled = false;
		config.LogRateLimitBurst = 1;
		var peer = CreatePeer(config, logs);
		var endpoint = new IPEndPoint(IPAddress.Loopback, 6767);

		for (var i = 0; i < 4; i++)
			peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedPacket, endpoint, "malformed packet");

		Assert.That(logs, Has.Count.EqualTo(4));
	}

	[Test]
	public void RateLimiterCanExcludeTarget()
	{
		var logs = new List<string>();
		var config = CreateConfig();
		config.LogRateLimitTargets = NetLogRateLimitTarget.MalformedFragment;
		config.LogRateLimitBurst = 1;
		var peer = CreatePeer(config, logs);
		var endpoint = new IPEndPoint(IPAddress.Loopback, 6767);

		for (var i = 0; i < 4; i++)
			peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedPacket, endpoint, "malformed packet");

		Assert.That(logs, Has.Count.EqualTo(4));
	}

	[Test]
	public void RateLimiterTracksEndpointAndTargetSeparately()
	{
		var logs = new List<string>();
		var config = CreateConfig();
		config.LogRateLimitBurst = 1;
		config.LogRateLimitWindow = 60.0f;
		var peer = CreatePeer(config, logs);
		var endpointA = new IPEndPoint(IPAddress.Loopback, 6767);
		var endpointB = new IPEndPoint(IPAddress.Loopback, 6768);

		peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedPacket, endpointA, "packet a");
		peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedPacket, endpointA, "packet a");
		peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedPacket, endpointB, "packet b");
		peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedFragment, endpointA, "fragment a");

		Assert.That(logs, Has.Count.EqualTo(3));
	}

	[Test]
	public void RateLimiterAllowsNewLogWhenWindowResets()
	{
		double? advancedFrom = null;
		double? advancedTo = null;
		try
		{
			var logs = new List<string>();
			var config = CreateConfig();
			config.LogRateLimitBurst = 1;
			config.LogRateLimitWindow = 0.1f;
			var peer = CreatePeer(config, logs);
			var endpoint = new IPEndPoint(IPAddress.Loopback, 6767);

			peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedPacket, endpoint, "malformed packet");
			peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedPacket, endpoint, "malformed packet");

			advancedFrom = NetTime.Now;
			advancedTo = advancedFrom.Value + (config.LogRateLimitWindow * 2.1f);
			NetTime.SetNow(advancedTo.Value);

			peer.LogRateLimitedWarning(NetLogRateLimitTarget.MalformedPacket, endpoint, "malformed packet");

			Assert.Multiple(() =>
			{
				Assert.That(logs, Has.Count.EqualTo(2));
				Assert.That(logs[1], Is.EqualTo("malformed packet"));
			});
		}
		finally
		{
			if (advancedFrom != null && advancedTo != null)
				NetTime.SetNow(advancedFrom.Value + (NetTime.Now - advancedTo.Value));
		}
	}

	private static NetPeerConfiguration CreateConfig()
	{
		var config = new NetPeerConfiguration(nameof(NetPeerLogRateLimiterTests));
		config.DisableMessageType(NetIncomingMessageType.WarningMessage);
		config.DisableMessageType(NetIncomingMessageType.ErrorMessage);
		return config;
	}

	private static NetPeer CreatePeer(NetPeerConfiguration config, List<string> logs)
	{
		var peer = new NetPeer(config);
		peer.LogEvent += (type, text) =>
		{
			if (type is NetIncomingMessageType.WarningMessage or NetIncomingMessageType.ErrorMessage)
				logs.Add(text);
		};
		return peer;
	}
}
