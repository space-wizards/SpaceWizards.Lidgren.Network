using System;
using System.Reflection;
using Lidgren.Network;
using NUnit.Framework;

namespace UnitTests;

[TestFixture]
[TestOf(typeof(NetPeer))]
public sealed class NetPeerStoragePoolTests
{
	[Test]
	public void GetStorageRoundsSmallRequestsUpToBucketSize()
	{
		var peer = CreatePeer();

		var storage = peer.GetStorage(17);

		Assert.That(storage.Length, Is.EqualTo(32));
	}

	[Test]
	public void RecycleReusesMatchingBucket()
	{
		var peer = CreatePeer();
		var storage = peer.GetStorage(17);

		peer.Recycle(storage);
		var reused = peer.GetStorage(31);

		Assert.Multiple(() =>
		{
			Assert.That(reused, Is.SameAs(storage));
			Assert.That(reused.Length, Is.EqualTo(32));
			Assert.That(peer.Statistics.BytesInRecyclePool, Is.Zero);
			Assert.That(peer.m_storageSlotsUsedCount, Is.Zero);
		});
	}

	[Test]
	public void GetStorageDoesNotReuseSmallerBucketForLargerRequest()
	{
		var peer = CreatePeer();
		var storage = peer.GetStorage(17);
		peer.Recycle(storage);

		var larger = peer.GetStorage(33);

		Assert.Multiple(() =>
		{
			Assert.That(larger, Is.Not.SameAs(storage));
			Assert.That(larger.Length, Is.EqualTo(64));
			Assert.That(peer.Statistics.BytesInRecyclePool, Is.EqualTo(32));
			Assert.That(peer.m_storageSlotsUsedCount, Is.EqualTo(1));
		});
	}

	[Test]
	public void RecycleHonorsMaxCacheCount()
	{
		var peer = CreatePeer(configuration => configuration.RecycledCacheMaxCount = 1);
		var first = peer.GetStorage(17);
		var second = peer.GetStorage(33);

		peer.Recycle(first);
		peer.Recycle(second);

		Assert.Multiple(() =>
		{
			Assert.That(peer.Statistics.BytesInRecyclePool, Is.EqualTo(32));
			Assert.That(peer.m_storageSlotsUsedCount, Is.EqualTo(1));
		});
	}

	[Test]
	public void RecycleIgnoresOversizedStorage()
	{
		var peer = CreatePeer();
		var storage = peer.GetStorage((1024 * 1024) + 1);

		peer.Recycle(storage);

		Assert.Multiple(() =>
		{
			Assert.That(storage.Length, Is.EqualTo((1024 * 1024) + 1));
			Assert.That(peer.Statistics.BytesInRecyclePool, Is.Zero);
			Assert.That(peer.m_storageSlotsUsedCount, Is.Zero);
		});
	}

	[Test]
	public void RecyclingDisabledAllocatesExactStorageAndDoesNotPool()
	{
		var peer = CreatePeer(configuration => configuration.UseMessageRecycling = false);
		var storage = peer.GetStorage(17);

		peer.Recycle(storage);

		Assert.Multiple(() =>
		{
			Assert.That(storage.Length, Is.EqualTo(17));
			Assert.That(peer.Statistics.BytesInRecyclePool, Is.Zero);
			Assert.That(peer.m_storageSlotsUsedCount, Is.Zero);
		});
	}

	private static NetPeer CreatePeer(Action<NetPeerConfiguration>? configure = null)
	{
		var configuration = new NetPeerConfiguration(nameof(NetPeerStoragePoolTests));
		configure?.Invoke(configuration);
		var peer = new NetPeer(configuration);
		InitializePools(peer);
		return peer;
	}

	private static void InitializePools(NetPeer peer)
	{
		var method = typeof(NetPeer).GetMethod("InitializePools", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null, "NetPeer.InitializePools is missing.");
		method!.Invoke(peer, Array.Empty<object>());
	}
}
