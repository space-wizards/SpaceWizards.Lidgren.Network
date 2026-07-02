namespace Lidgren.Network;

public partial class NetPeer
{
	internal sealed class ReceivedFragmentGroup
	{
		//public float LastReceived;
		public byte[] Data { get; }
		public NetBitVector ReceivedChunks { get; }
		public int TotalBytes { get; }
		public int TotalBits { get; }
		public int ChunkByteSize { get; }
		public int TotalNumChunks { get; }
		public int ReceivedChunkCount { get; private set; }

		public ReceivedFragmentGroup(byte[] data, NetBitVector receivedChunks, int totalBytes, int totalBits, int chunkByteSize, int totalNumChunks)
		{
			Data = data;
			ReceivedChunks = receivedChunks;
			TotalBytes = totalBytes;
			TotalBits = totalBits;
			ChunkByteSize = chunkByteSize;
			TotalNumChunks = totalNumChunks;
		}

		public void MarkChunkReceived(int chunkNumber)
		{
			if (ReceivedChunks[chunkNumber])
				return;

			ReceivedChunks[chunkNumber] = true;
			ReceivedChunkCount++;
		}
	}
}
