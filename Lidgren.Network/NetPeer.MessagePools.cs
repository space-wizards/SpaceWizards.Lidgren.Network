using System;
using System.Collections.Generic;
using System.Text;

namespace Lidgren.Network
{
	public partial class NetPeer
	{
		private const int c_storagePoolMinBucketSize = 16;
		private const int c_storagePoolMaxBucketSize = 1024 * 1024;

		internal NetQueue<byte[]>[]? m_storagePools;
		private NetQueue<NetOutgoingMessage>? m_outgoingMessagesPool;
		private NetQueue<NetIncomingMessage>? m_incomingMessagesPool;

		internal int m_storagePoolBytes;
		internal int m_storageSlotsUsedCount;
		private int m_maxCacheCount;

		private void InitializePools()
		{
			m_storageSlotsUsedCount = 0;
			m_storagePoolBytes = 0;

			if (m_configuration.UseMessageRecycling)
			{
				m_storagePools = CreateStoragePools();
				m_outgoingMessagesPool = new NetQueue<NetOutgoingMessage>(4);
				m_incomingMessagesPool = new NetQueue<NetIncomingMessage>(4);
			}
			else
			{
				m_storagePools = null;
				m_outgoingMessagesPool = null;
				m_incomingMessagesPool = null;
			}

			m_maxCacheCount = m_configuration.RecycledCacheMaxCount;
		}

		internal byte[] GetStorage(int minimumCapacityInBytes)
		{
			var pools = m_storagePools;
			if (pools == null)
				return new byte[minimumCapacityInBytes];

			var bucket = GetStorageBucket(minimumCapacityInBytes);
			if (bucket >= 0)
			{
				lock (pools)
				{
					if (pools[bucket].TryDequeue(out var storage))
					{
						m_storageSlotsUsedCount--;
						m_storagePoolBytes -= storage.Length;
						return storage;
					}
				}
			}

			var allocationSize = GetStorageBucketSize(minimumCapacityInBytes);
			m_statistics.m_bytesAllocated += allocationSize;
			return new byte[allocationSize];
		}

		internal void Recycle(byte[] storage)
		{
			var pools = m_storagePools;
			if (pools == null)
				return;

			var bucket = GetStorageBucket(storage.Length);
			if (bucket < 0 || storage.Length != GetStorageBucketSize(storage.Length))
				return;

			lock (pools)
			{
				if (m_storageSlotsUsedCount >= m_maxCacheCount)
					return;

				m_storageSlotsUsedCount++;
				m_storagePoolBytes += storage.Length;
				pools[bucket].Enqueue(storage);
			}
		}

		private static NetQueue<byte[]>[] CreateStoragePools()
		{
			var bucketCount = GetStorageBucket(c_storagePoolMaxBucketSize) + 1;
			var pools = new NetQueue<byte[]>[bucketCount];
			for (var i = 0; i < pools.Length; i++)
			{
				pools[i] = new NetQueue<byte[]>(4);
			}

			return pools;
		}

		private static int GetStorageBucket(int byteCount)
		{
			if (byteCount <= 0)
				return 0;

			if (byteCount > c_storagePoolMaxBucketSize)
				return -1;

			var size = c_storagePoolMinBucketSize;
			var bucket = 0;
			while (size < byteCount)
			{
				size <<= 1;
				bucket++;
			}

			return bucket;
		}

		private static int GetStorageBucketSize(int minimumCapacityInBytes)
		{
			var bucket = GetStorageBucket(minimumCapacityInBytes);
			if (bucket < 0)
				return minimumCapacityInBytes;

			return c_storagePoolMinBucketSize << bucket;
		}

		/// <summary>
		/// Creates a new message for sending
		/// </summary>
		public NetOutgoingMessage CreateMessage()
		{
			return CreateMessage(m_configuration.m_defaultOutgoingMessageCapacity);
		}

		/// <summary>
		/// Creates a new message for sending and writes the provided string to it
		/// </summary>
		public NetOutgoingMessage CreateMessage(string? content)
		{
			NetOutgoingMessage om;

			// Since this could be null.
			if (string.IsNullOrEmpty(content))
			{
				om = CreateMessage(1); // One byte for the internal variable-length zero byte.
			}
			else
			{
				om = CreateMessage(2 + content.Length); // Fair guess.
			}

			om.Write(content);
			return om;
		}

		/// <summary>
		/// Creates a new message for sending
		/// </summary>
		/// <param name="initialCapacity">initial capacity in bytes</param>
		public NetOutgoingMessage CreateMessage(int initialCapacity)
		{
			if (m_outgoingMessagesPool == null || !m_outgoingMessagesPool.TryDequeue(out NetOutgoingMessage? retval))
				retval = new NetOutgoingMessage();

			NetException.Assert(retval.m_recyclingCount == 0, "Wrong recycling count! Should be zero" + retval.m_recyclingCount);

			if (initialCapacity > 0)
				retval.m_data = GetStorage(initialCapacity);

			return retval;
		}

		internal NetIncomingMessage CreateIncomingMessage(NetIncomingMessageType tp, byte[] useStorageData)
		{
			if (m_incomingMessagesPool == null || !m_incomingMessagesPool.TryDequeue(out NetIncomingMessage? retval))
				retval = new NetIncomingMessage(tp);
			else
				retval.m_incomingMessageType = tp;
			retval.m_data = useStorageData;
			return retval;
		}

		internal NetIncomingMessage CreateIncomingMessage(NetIncomingMessageType tp, int minimumByteSize)
		{
			if (m_incomingMessagesPool == null || !m_incomingMessagesPool.TryDequeue(out NetIncomingMessage? retval))
				retval = new NetIncomingMessage(tp);
			else
				retval.m_incomingMessageType = tp;
			retval.m_data = GetStorage(minimumByteSize);
			return retval;
		}

		/// <summary>
		/// Recycles a NetIncomingMessage instance for reuse; taking pressure off the garbage collector
		/// </summary>
		public void Recycle(NetIncomingMessage msg)
		{
			if (m_incomingMessagesPool == null || msg == null)
				return;

			NetException.Assert(m_incomingMessagesPool.Contains(msg) == false, "Recyling already recycled incoming message! Thread race?");

			byte[] storage = msg.Data;
			msg.m_data = null;
			Recycle(storage);

			msg.Reset();

			if (m_incomingMessagesPool.Count < m_maxCacheCount)
				m_incomingMessagesPool.Enqueue(msg);
		}

		/// <summary>
		/// Recycles a list of NetIncomingMessage instances for reuse; taking pressure off the garbage collector
		/// </summary>
		public void Recycle(IEnumerable<NetIncomingMessage> toRecycle)
		{
			if (m_incomingMessagesPool == null)
				return;
			foreach (var im in toRecycle)
				Recycle(im);
		}

		internal void Recycle(NetOutgoingMessage msg)
		{
			if (m_outgoingMessagesPool == null)
				return;
#if DEBUG
			NetException.Assert(m_outgoingMessagesPool.Contains(msg) == false, "Recyling already recycled outgoing message! Thread race?");
			if (msg.m_recyclingCount != 0)
				LogWarning($"Wrong recycling count! should be zero; found {msg.m_recyclingCount}");
#endif
			// setting m_recyclingCount to zero SHOULD be an unnecessary maneuver, if it's not zero something is wrong
			// however, in RELEASE, we'll just have to accept this and move on with life
			msg.m_recyclingCount = 0;

			byte[] storage = msg.Data;
			msg.m_data = null;

			// message fragments cannot be recycled
			// TODO: find a way to recycle large message after all fragments has been acknowledged; or? possibly better just to garbage collect them
			if (msg.m_fragmentGroup == 0)
				Recycle(storage);

			msg.Reset();
			if (m_outgoingMessagesPool.Count < m_maxCacheCount)
				m_outgoingMessagesPool.Enqueue(msg);
		}

		/// <summary>
		/// Creates an incoming message with the required capacity for releasing to the application
		/// </summary>
		internal NetIncomingMessage CreateIncomingMessage(NetIncomingMessageType tp, string text)
		{
			NetIncomingMessage retval;
			if (string.IsNullOrEmpty(text))
			{
				retval = CreateIncomingMessage(tp, 1);
				retval.Write(string.Empty);
				return retval;
			}

			int numBytes = System.Text.Encoding.UTF8.GetByteCount(text);
			retval = CreateIncomingMessage(tp, numBytes + (numBytes > 127 ? 2 : 1));
			retval.Write(text);

			return retval;
		}
	}
}
