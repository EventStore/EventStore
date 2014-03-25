using EventStore.Common.Utils;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Monitoring
{
    [TestFixture]
    public class FormatterTests
    {
        [Test]
        [TestCase(0L, Result = "0B")]
        [TestCase(500L, Result = "500B")]
        [TestCase(1023L, Result = "1023B")]
        [TestCase(1024L, Result = "1KiB")]
        [TestCase(2560L, Result = "2.5KiB")]
        [TestCase(1048576L, Result = "1MiB")]
        [TestCase(502792192L, Result = "479.5MiB")]
        [TestCase(1073741824L, Result = "1GiB")]
        [TestCase(79725330432L, Result = "74.25GiB")]
        [TestCase(1099511627776L, Result = "1TiB")]
        [TestCase(1125899906842624L, Result = "1024TiB")]
        [TestCase(long.MaxValue, Result = "8388608TiB")]
        [TestCase(-1L, Result = "-1B")]
        [TestCase(-1023L, Result = "-1023B")]
        [TestCase(-1024, Result = "-1KiB")]
        [TestCase(-1048576L, Result = "-1MiB")]
        public string test_size_multiple_cases_long(long bytes )
        {
            return bytes.ToFriendlySizeString();
        }

        [Test]
        [TestCase(0UL, Result = "0B")]
        [TestCase(500UL, Result = "500B")]
        [TestCase(1023UL, Result = "1023B")]
        [TestCase(1024UL, Result = "1KiB")]
        [TestCase(2560UL, Result = "2.5KiB")]
        [TestCase(1048576UL, Result = "1MiB")]
        [TestCase(502792192UL, Result = "479.5MiB")]
        [TestCase(1073741824UL, Result = "1GiB")]
        [TestCase(79725330432UL, Result = "74.25GiB")]
        [TestCase(1099511627776UL, Result = "1TiB")]
        [TestCase(1125899906842624UL, Result = "1024TiB")]
        [TestCase(ulong.MaxValue, Result = "more then long.MaxValue")] //16777215TiB
        public string test_size_multiple_cases_ulong(ulong bytes )
        {
            return bytes.ToFriendlySizeString();
        }



        [Test]
        [TestCase(0D, Result = "0B/s")]
        [TestCase(500L, Result = "500B/s")]
        [TestCase(1023L, Result = "1023B/s")]
        [TestCase(1024L, Result = "1KiB/s")]
        [TestCase(2560L, Result = "2.5KiB/s")]
        [TestCase(1048576L, Result = "1MiB/s")]
        [TestCase(502792192L, Result = "479.5MiB/s")]
        [TestCase(1073741824L, Result = "1GiB/s")]
        [TestCase(79725330432L, Result = "74.25GiB/s")]
        [TestCase(-1L, Result = "-1B/s")]
        [TestCase(-1023L, Result = "-1023B/s")]
        [TestCase(-1024, Result = "-1KiB/s")]
        [TestCase(-1048576L, Result = "-1MiB/s")]
        public string test_speed_multiple_cases(double speed )
        {
            return speed.ToFriendlySpeedString();
        }



        [Test]
        [TestCase(0L, Result = "0")]
        [TestCase(500L, Result = "500")]
        [TestCase(1023L, Result = "1023")]
        [TestCase(1024L, Result = "1K")]
        [TestCase(2560L, Result = "2.5K")]
        [TestCase(1048576L, Result = "1M")]
        [TestCase(502792192L, Result = "479.5M")]
        [TestCase(1073741824L, Result = "1G")]
        [TestCase(79725330432L, Result = "74.25G")]
        [TestCase(1099511627776L, Result = "1T")]
        [TestCase(1125899906842624L, Result = "1024T")]
        [TestCase(long.MaxValue, Result = "8388608T")]
        [TestCase(-1L, Result = "-1")]
        [TestCase(-1023L, Result = "-1023")]
        [TestCase(-1024, Result = "-1K")]
        [TestCase(-1048576L, Result = "-1M")]
        public string test_number_multiple_cases_long(long number )
        {
            return number.ToFriendlyNumberString();
        }

        [Test]
        [TestCase(0UL, Result = "0")]
        [TestCase(500UL, Result = "500")]
        [TestCase(1023UL, Result = "1023")]
        [TestCase(1024UL, Result = "1K")]
        [TestCase(2560UL, Result = "2.5K")]
        [TestCase(1048576UL, Result = "1M")]
        [TestCase(502792192UL, Result = "479.5M")]
        [TestCase(1073741824UL, Result = "1G")]
        [TestCase(79725330432UL, Result = "74.25G")]
        [TestCase(1099511627776UL, Result = "1T")]
        [TestCase(1125899906842624UL, Result = "1024T")]
        [TestCase(ulong.MaxValue, Result = "more then long.MaxValue")] //16777215TiB
        public string test_number_multiple_cases_ulong(ulong number )
        {
            return number.ToFriendlyNumberString();
        }




    }
}
