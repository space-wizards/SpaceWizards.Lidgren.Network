using Lidgren.Network;
using NUnit.Framework;
using System.Reflection;

namespace UnitTests;

[TestFixture]
[TestOf(typeof(NetFragmentationHelper))]
public sealed class NetFragmentationHelperTests
{
	[Test]
	public void TryReadHeaderReadsValidHeader()
	{
		var buffer = new byte[32];
		var end = NetFragmentationHelper.WriteHeader(buffer, 0, 7, 4096, 512, 3);

		var ok = TryReadHeader(
			buffer, 0, end,
			out var headerEnd,
			out var group,
			out var totalBits,
			out var chunkByteSize,
			out var chunkNumber);

		Assert.Multiple(() =>
		{
			Assert.That(ok, Is.True);
			Assert.That(headerEnd, Is.EqualTo(end));
			Assert.That(group, Is.EqualTo(7));
			Assert.That(totalBits, Is.EqualTo(4096));
			Assert.That(chunkByteSize, Is.EqualTo(512));
			Assert.That(chunkNumber, Is.EqualTo(3));
		});
	}

	[Test]
	public void TryReadHeaderRejectsTruncatedVarint()
	{
		var buffer = new byte[] { 1, 0x80 };

		var ok = TryReadHeader(
			buffer, 0, buffer.Length,
			out _,
			out _,
			out _,
			out _,
			out _);

		Assert.That(ok, Is.False);
	}

	[Test]
	public void TryReadHeaderRejectsTooLong()
	{
		var buffer = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80 };

		var ok = TryReadHeader(
			buffer, 0, buffer.Length,
			out _,
			out _,
			out _,
			out _,
			out _);

		Assert.That(ok, Is.False);
	}

	[Test]
	public void TryReadHeaderRejectsIntOverflow()
	{
		var buffer = new byte[] { 0xff, 0xff, 0xff, 0xff, 0x7f };

		var ok = TryReadHeader(
			buffer, 0, buffer.Length,
			out _,
			out _,
			out _,
			out _,
			out _);

		Assert.That(ok, Is.False);
	}

	[Test]
	public void TryReadHeaderDoesNotReadPastLogicalEnd()
	{
		var buffer = new byte[] { 1, 0x80, 0, 1, 0 };

		var ok = TryReadHeader(
			buffer, 0, 2,
			out _,
			out _,
			out _,
			out _,
			out _);

		Assert.That(ok, Is.False);
	}

	// Blursed code to read the actual private method without just copying the underlying code and not accounting for
	// new changes.
	private static bool TryReadHeader(
		byte[] buffer,
		int ptr,
		int endPtr,
		out int headerEnd,
		out int group,
		out int totalBits,
		out int chunkByteSize,
		out int chunkNumber)
	{

		var method = typeof(NetFragmentationHelper).GetMethod(
			"TryReadHeader",
			BindingFlags.Static | BindingFlags.NonPublic);

		Assert.That(method, Is.Not.Null, "NetFragmentationHelper.TryReadHeader is missing.");

		object?[] args = { buffer, ptr, endPtr, 0, 0, 0, 0, 0 };
		var ok = (bool)method!.Invoke(null, args)!;

		headerEnd = (int)args[3]!;
		group = (int)args[4]!;
		totalBits = (int)args[5]!;
		chunkByteSize = (int)args[6]!;
		chunkNumber = (int)args[7]!;
		return ok;
	}
}
