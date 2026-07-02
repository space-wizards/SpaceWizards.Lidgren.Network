namespace Lidgren.Network;

public partial class NetPeer
{
	internal sealed class ReceivedFragmentGroup
	{
		public byte[] Data { get; }
		public NetBitVector ReceivedChunks { get; }
		public int TotalBytes { get; }
		public int TotalBits { get; }
		public int ChunkByteSize { get; }
		public int TotalNumChunks { get; }
		public double LastReceived { get; set; }

		public ReceivedFragmentGroup(byte[] data, NetBitVector receivedChunks, int totalBytes, int totalBits, int chunkByteSize, int totalNumChunks, double lastReceived)
		{
			Data = data;
			ReceivedChunks = receivedChunks;
			TotalBytes = totalBytes;
			TotalBits = totalBits;
			ChunkByteSize = chunkByteSize;
			TotalNumChunks = totalNumChunks;
			LastReceived = lastReceived;
		}
	}
}
