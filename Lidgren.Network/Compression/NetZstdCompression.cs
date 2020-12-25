#nullable enable
using System;
using ZstdNet;

namespace Lidgren.Network
{
    public class NetZstdCompression : NetCompression, IDisposable
    {
	    private bool _disposed = false;
        private readonly Compressor _compressor;
        private readonly Decompressor _decompressor;

        public NetZstdCompression(byte[]? dictionary = null, int compressionLevel = CompressionOptions.DefaultCompressionLevel)
        {
	        _compressor = new Compressor(new CompressionOptions(dictionary));
	        _decompressor = new Decompressor();
        }

        public override bool Compress(NetOutgoingMessage msg)
        {
	        if (_disposed) return false;
	        
            var span = msg.Data.AsSpan().Slice(msg.PositionInBytes, msg.LengthBytes);
            var data = _compressor.Wrap(span);
            msg.Data = data;
            msg.m_bitLength = data.Length * 8;
            return true;
        }

        public override bool Decompress(NetIncomingMessage msg)
        {
	        if (_disposed) return false;
	        
            var span = msg.Data.AsSpan().Slice(msg.PositionInBytes, msg.LengthBytes);
            var data = _decompressor.Unwrap(span);
            msg.Data = data;
            msg.m_bitLength = data.Length * 8;
            return true;
        }

        public void Dispose()
        {
	        if (_disposed) return;
	        _disposed = true;
	        _compressor.Dispose();
	        _decompressor.Dispose();
        }
    }
}
