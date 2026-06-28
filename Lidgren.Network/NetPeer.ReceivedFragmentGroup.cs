namespace Lidgren.Network;

public partial class NetPeer
{
	internal sealed class ReceivedFragmentGroup
	{
		//public float LastReceived;
		public byte[] Data { get; }
		public NetBitVector ReceivedChunks { get; }
		public int TotalBits { get; }
		public int ChunkByteSize { get; }
		public int TotalNumChunks { get; }

		public ReceivedFragmentGroup(byte[] data, NetBitVector receivedChunks, int totalBits, int chunkByteSize, int totalNumChunks)
		{
			Data = data;
			ReceivedChunks = receivedChunks;
			TotalBits = totalBits;
			ChunkByteSize = chunkByteSize;
			TotalNumChunks = totalNumChunks;
		}
	}
}
