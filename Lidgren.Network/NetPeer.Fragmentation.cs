using System;
using System.Threading;
using System.Collections.Generic;

namespace Lidgren.Network
{
	public partial class NetPeer
	{
		private int m_lastUsedFragmentGroup;

		private readonly Dictionary<NetConnection, Dictionary<int, ReceivedFragmentGroup>> m_receivedFragmentGroups;

		// on user thread
		private NetSendResult SendFragmentedMessage(NetOutgoingMessage msg, IList<NetConnection> recipients, NetDeliveryMethod method, int sequenceChannel)
		{
			// Note: this group id is PER SENDING/NetPeer; ie. same id is sent to all recipients;
			// this should be ok however; as long as recipients differentiate between same id but different sender
			int group = Interlocked.Increment(ref m_lastUsedFragmentGroup);
			if (group >= NetConstants.MaxFragmentationGroups)
			{
				// @TODO: not thread safe; but in practice probably not an issue
				m_lastUsedFragmentGroup = 1;
				group = 1;
			}
			msg.m_fragmentGroup = group;

			// do not send msg; but set fragmentgroup in case user tries to recycle it immediately

			// create fragmentation specifics
			int totalBytes = msg.LengthBytes;

			// determine minimum mtu for all recipients
			int mtu = GetMTU(recipients);
			int bytesPerChunk = NetFragmentationHelper.GetBestChunkSize(group, totalBytes, mtu);

			int numChunks = totalBytes / bytesPerChunk;
			if (numChunks * bytesPerChunk < totalBytes)
				numChunks++;

			NetSendResult retval = NetSendResult.Sent;

			int bitsPerChunk = bytesPerChunk * 8;
			int bitsLeft = msg.LengthBits;
			for (int i = 0; i < numChunks; i++)
			{
				NetOutgoingMessage chunk = CreateMessage(0);

				chunk.m_bitLength = (bitsLeft > bitsPerChunk ? bitsPerChunk : bitsLeft);
				chunk.m_data = msg.m_data;
				chunk.m_fragmentGroup = group;
				chunk.m_fragmentGroupTotalBits = totalBytes * 8;
				chunk.m_fragmentChunkByteSize = bytesPerChunk;
				chunk.m_fragmentChunkNumber = i;

				NetException.Assert(chunk.m_bitLength != 0);
				NetException.Assert(chunk.GetEncodedSize() < mtu);

				Interlocked.Add(ref chunk.m_recyclingCount, recipients.Count);

				foreach (NetConnection recipient in recipients)
				{
					var res = recipient.EnqueueMessage(chunk, method, sequenceChannel);
					if (res == NetSendResult.Dropped)
						Interlocked.Decrement(ref chunk.m_recyclingCount);
					if ((int)res > (int)retval)
						retval = res; // return "worst" result
				}

				bitsLeft -= bitsPerChunk;
			}

			return retval;
		}

		private void HandleReleasedFragment(NetIncomingMessage im)
		{
			VerifyNetworkThread();

			//
			// read fragmentation header and combine fragments
			//
			int ptr = NetFragmentationHelper.ReadHeader(
				im.Data, 0,
				out int group,
				out int totalBits,
				out int chunkByteSize,
				out int chunkNumber
			);

			// Starlight start validate the attacker controlled fragment header BEFORE allocating or copying.
			int totalBytes = NetUtility.BytesToHoldBits(totalBits);
			int payloadLength = im.LengthBytes - ptr;

			if (im.SenderConnection == null
				|| group <= 0
				|| totalBits <= 0
				|| chunkByteSize <= 0
				|| chunkByteSize > NetConstants.MaximumFragmentChunkSize
				|| totalBytes <= 0
				|| totalBytes > NetConstants.MaximumFragmentGroupSize
				|| payloadLength <= 0
				|| payloadLength > chunkByteSize)
			{
				LogWarning($"Dropping malformed fragment from {im.SenderEndPoint} (group={group}, totalBits={totalBits}, chunkByteSize={chunkByteSize}, payload={payloadLength})");
				Recycle(im);
				return;
			}

			int totalNumChunks = (totalBytes + chunkByteSize - 1) / chunkByteSize;

			if (chunkNumber < 0
				|| chunkNumber >= totalNumChunks
				|| (long)chunkNumber * chunkByteSize + payloadLength > totalBytes)
			{
				LogWarning($"Dropping out-of-range fragment {chunkNumber}/{totalNumChunks} from {im.SenderEndPoint}");
				Recycle(im);
				return;
			}
			// Starlight end

			if (!m_receivedFragmentGroups.TryGetValue(im.SenderConnection, out Dictionary<int, ReceivedFragmentGroup>? groups))
			{
				groups = new Dictionary<int, ReceivedFragmentGroup>();
				m_receivedFragmentGroups[im.SenderConnection] = groups;
			}

			if (!groups.TryGetValue(group, out ReceivedFragmentGroup? info))
			{
				// Starlight start bound concurrent reassembly groups per connection so a flood of distinct
				// single fragment groups can't accumulate unbounded buffers
				if (groups.Count >= NetConstants.MaximumConcurrentFragmentGroups)
				{
					LogWarning($"Too many concurrent fragment groups from {im.SenderEndPoint}; dropping fragment");
					Recycle(im);
					return;
				}
				// Starlight end
				info = new ReceivedFragmentGroup(new byte[totalBytes], new NetBitVector(totalNumChunks));
				groups[group] = info;
			}
			// Starlight start later fragments must agree with the size the group was created with, otherwise
			// the computed offset/copy could run out of bounds.
			else if (info.Data.Length != totalBytes)
			{
				LogWarning($"Dropping inconsistent fragment for group {group} from {im.SenderEndPoint}");
				Recycle(im);
				return;
			}
			// Starlight end

			info.ReceivedChunks[chunkNumber] = true;
			//info.LastReceived = (float)NetTime.Now;

			// copy to data
			int offset = (chunkNumber * chunkByteSize);
			Buffer.BlockCopy(im.Data, ptr, info.Data, offset, payloadLength);

			int cnt = info.ReceivedChunks.Count();
			//LogVerbose($"Found fragment #{chunkNumber} in group {group} offset {offset} of total bits {totalBits} (total chunks done {cnt})");

			LogVerbose($"Received fragment {chunkNumber} of {totalNumChunks} ({cnt} chunks received)");

			if (info.ReceivedChunks.Count() == totalNumChunks)
			{
				// Done! Transform this incoming message
				im.m_data = info.Data;
				im.m_bitLength = totalBits;
				im.m_isFragment = false;

				LogVerbose($"Fragment group #{group} fully received in {totalNumChunks} chunks ({totalBits} bits)");
				groups.Remove(group);

				ReleaseMessage(im);
			}
			else
			{
				// data has been copied; recycle this incoming message
				Recycle(im);
			}

			return;
		}
	}
}
