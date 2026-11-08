using NUnit.Framework;

namespace AstroFlowJam.Tests
{
    public class CoinWalletTests
    {
        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(1000, "1k")]
        [TestCase(1400, "1.4k")]
        [TestCase(1426, "1.42k")]
        [TestCase(9999, "9.99k")]
        [TestCase(10000, "10k")]
        [TestCase(10450, "10.4k")]
        [TestCase(20600, "20.6k")]
        [TestCase(99999, "99.9k")]
        [TestCase(100123, "100k")]
        [TestCase(134999, "134k")]
        public void Short_RoundsDownAndDropsTrailingZeros(int value, string expected)
        {
            Assert.AreEqual(expected, CoinWallet.Short(value));
        }
    }
}
