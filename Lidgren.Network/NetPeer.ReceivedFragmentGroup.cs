namespace Lidgren.Network;

public partial class NetPeer
{
	internal sealed class ReceivedFragmentGroup
	{
		//public float LastReceived;
		public byte[] Data { get; }
		public NetBitVector ReceivedChunks { get; }

		public ReceivedFragmentGroup(byte[] data, NetBitVector receivedChunks)
		{
			Data = data;
			ReceivedChunks = receivedChunks;
		}
	}
}
