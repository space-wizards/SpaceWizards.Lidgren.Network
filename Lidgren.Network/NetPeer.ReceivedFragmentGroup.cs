namespace Lidgren.Network;

public partial class NetPeer
{
	internal sealed class ReceivedFragmentGroup
	{
		//public float LastReceived;
		public byte[] Data { get; }
		public int TotalBytes { get; }
		public NetBitVector ReceivedChunks { get; }

		public ReceivedFragmentGroup(byte[] data, int totalBytes, NetBitVector receivedChunks)
		{
			Data = data;
			TotalBytes = totalBytes;
			ReceivedChunks = receivedChunks;
		}
	}
}
