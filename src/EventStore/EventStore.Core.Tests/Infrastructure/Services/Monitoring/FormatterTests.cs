// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using EventStore.Common.Utils;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Monitoring
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
