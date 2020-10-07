using Lidgren.Network;
using NUnit.Framework;

namespace UnitTests
{
    [TestOf(typeof(NetUtility))]
    [TestFixture]
    [Parallelizable]
    public class NetUtilityTests
    {
        // ReSharper disable once StringLiteralTypo
        [Test]
        [TestCase(new byte[]{0xDE, 0xAD, 0xBE, 0xEF}, "DEADBEEF")]
        public void TestToHexString(byte[] bytes, string expected)
        {
            Assert.That(NetUtility.ToHexString(bytes), Is.EqualTo(expected));
        }

        [Test]
        [TestCase((ulong) uint.MaxValue + 1, 33)]
        public void TestBitsToHoldUInt64(ulong num, int expectedBits)
        {
            Assert.That(NetUtility.BitsToHoldUInt64(num), Is.EqualTo(expectedBits));
        }
    }
}