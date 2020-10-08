using System;
using System.IO;
using System.Security.Cryptography;

namespace Lidgren.Network
{
	public class NetDESEncryption : NetCryptoProviderBase
	{
		public NetDESEncryption(NetPeer peer)
			: base(peer, new DESCryptoServiceProvider())
		{
		}

		public NetDESEncryption(NetPeer peer, string key)
			: base(peer, new DESCryptoServiceProvider())
		{
			SetKey(key);
		}

		public NetDESEncryption(NetPeer peer, byte[] data, int offset, int count)
			: this(peer, data.AsSpan(offset, count))
		{
		}

		public NetDESEncryption(NetPeer peer, ReadOnlySpan<byte> data)
			: base(peer, new DESCryptoServiceProvider())
		{
			SetKey(data);
		}
	}
}
