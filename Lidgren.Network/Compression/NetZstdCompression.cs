using System;
using ZstdNet;

namespace Lidgren.Network
{
    public class NetZstdCompression : NetCompression
    {
        private readonly Compressor _compressor = new Compressor(new CompressionOptions(CompressionOptions.MaxCompressionLevel));
        private readonly Decompressor _decompressor = new Decompressor();

        public override bool Compress(NetOutgoingMessage msg)
        {
            var span = msg.Data.AsSpan().Slice(msg.PositionInBytes, msg.LengthBytes);

            var data = _compressor.Wrap(span);
            
            msg.Data = data;
            msg.m_bitLength = data.Length * 8;
            return true;
        }

        public override bool Decompress(NetIncomingMessage msg)
        {
            var span = msg.Data.AsSpan().Slice(msg.PositionInBytes, msg.LengthBytes);

            var data = _decompressor.Unwrap(span);
            msg.Data = data;
            msg.m_bitLength = data.Length * 8;
            return true;
        }
    }
}
