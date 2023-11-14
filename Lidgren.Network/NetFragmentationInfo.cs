using System;

namespace Lidgren.Network
{
	public sealed class NetFragmentationInfo
	{
		public int TotalFragmentCount { get; }
		public bool[] Received{ get; }
		public int TotalReceived{ get; }
		public int FragmentSize{ get; }

		public NetFragmentationInfo(int totalFragmentCount, bool[] received, int totalReceived, int fragmentSize)
		{
			TotalFragmentCount = totalFragmentCount;
			Received = received;
			TotalReceived = totalReceived;
			FragmentSize = fragmentSize;
		}
	}
}