using System;

namespace Lidgren.Network
{
	internal static class NetFragmentationHelper
	{
		internal static int WriteHeader(
			byte[] destination,
			int ptr,
			int group,
			int totalBits,
			int chunkByteSize,
			int chunkNumber)
		{
			uint num1 = (uint)group;
			while (num1 >= 0x80)
			{
				destination[ptr++] = (byte)(num1 | 0x80);
				num1 = num1 >> 7;
			}
			destination[ptr++] = (byte)num1;

			// write variable length fragment total bits
			uint num2 = (uint)totalBits;
			while (num2 >= 0x80)
			{
				destination[ptr++] = (byte)(num2 | 0x80);
				num2 = num2 >> 7;
			}
			destination[ptr++] = (byte)num2;

			// write variable length fragment chunk size
			uint num3 = (uint)chunkByteSize;
			while (num3 >= 0x80)
			{
				destination[ptr++] = (byte)(num3 | 0x80);
				num3 = num3 >> 7;
			}
			destination[ptr++] = (byte)num3;

			// write variable length fragment chunk number
			uint num4 = (uint)chunkNumber;
			while (num4 >= 0x80)
			{
				destination[ptr++] = (byte)(num4 | 0x80);
				num4 = num4 >> 7;
			}
			destination[ptr++] = (byte)num4;

			return ptr;
		}

		internal static bool TryReadHeader(
			byte[] buffer,
			int ptr,
			int endPtr,
			out int headerEnd,
			out int group,
			out int totalBits,
			out int chunkByteSize,
			out int chunkNumber)
		{
			headerEnd = ptr;
			group = 0;
			totalBits = 0;
			chunkByteSize = 0;
			chunkNumber = 0;

			return TryReadVariableInt(buffer, ref headerEnd, endPtr, out group)
				&& TryReadVariableInt(buffer, ref headerEnd, endPtr, out totalBits)
				&& TryReadVariableInt(buffer, ref headerEnd, endPtr, out chunkByteSize)
				&& TryReadVariableInt(buffer, ref headerEnd, endPtr, out chunkNumber);
		}

		private static bool TryReadVariableInt(byte[] buffer, ref int ptr, int endPtr, out int value)
		{
			uint result = 0;
			value = 0;

			for (int shift = 0; shift <= 28; shift += 7)
			{
				// Header out of the expected range so dump it.
				if (ptr >= endPtr)
					return false;

				byte next = buffer[ptr++];
				result |= (uint)(next & 0x7f) << shift;

				if ((next & 0x80) == 0)
				{
					if (result > int.MaxValue)
						return false;

					value = (int)result;
					return true;
				}
			}

			// If the header bytes are still going then dump it.
			return false;
		}

		internal static int GetFragmentationHeaderSize(int groupId, int totalBytes, int chunkByteSize, int numChunks)
		{
			int len = 4;

			// write variable length fragment group id
			uint num1 = (uint)groupId;
			while (num1 >= 0x80)
			{
				len++;
				num1 = num1 >> 7;
			}

			// write variable length fragment total bits
			uint num2 = (uint)(totalBytes * 8);
			while (num2 >= 0x80)
			{
				len++;
				num2 = num2 >> 7;
			}

			// write variable length fragment chunk byte size
			uint num3 = (uint)chunkByteSize;
			while (num3 >= 0x80)
			{
				len++;
				num3 = num3 >> 7;
			}

			// write variable length fragment chunk number
			uint num4 = (uint)numChunks;
			while (num4 >= 0x80)
			{
				len++;
				num4 = num4 >> 7;
			}

			return len;
		}

		internal static int GetBestChunkSize(int group, int totalBytes, int mtu)
		{
			int tryChunkSize = mtu - NetConstants.HeaderByteSize - 4; // naive approximation
			int est = GetFragmentationHeaderSize(group, totalBytes, tryChunkSize, totalBytes / tryChunkSize);
			tryChunkSize = mtu - NetConstants.HeaderByteSize - est; // slightly less naive approximation

			int headerSize = 0;
			do
			{
				tryChunkSize--; // keep reducing chunk size until it fits within MTU including header

				int numChunks = totalBytes / tryChunkSize;
				if (numChunks * tryChunkSize < totalBytes)
					numChunks++;

				headerSize = GetFragmentationHeaderSize(group, totalBytes, tryChunkSize, numChunks); // 4+ bytes

			} while (tryChunkSize + headerSize + NetConstants.HeaderByteSize + 1 >= mtu);

			return tryChunkSize;
		}
	}
}
