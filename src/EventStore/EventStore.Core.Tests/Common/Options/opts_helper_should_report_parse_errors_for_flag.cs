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

using System;
using Mono.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_should_report_parse_errors_for_flag : OptsHelperTestBase
    {
        public bool Flag { get { throw new InvalidOperationException(); } }
        
        [Test, Ignore("Mono.Options allows this situation and ignores the value provided.")]
        public void with_value_in_cmd_line()
        {
            Helper.RegisterFlag(() => Flag, "f|flag", "settings.flag", "FLAG");

            Helper.Parse("-f", "somevalue");
            Assert.Fail();
        }

        [Test, Ignore("Mono.Options allows this situation and ignores the value provided.")]
        public void if_flag_is_defined_more_than_once()
        {
            Helper.RegisterFlag(() => Flag, "f|flag", "settings.flag", "FLAG");

            Assert.Throws<OptionException>(() => Helper.Parse("-f-", "-f+"));
        }

        [Test]
        public void with_non_bool_value_in_env()
        {
            Helper.RegisterFlag(() => Flag, "f|flag", "settings.flag", "FLAG");
            SetEnv("FLAG", "NOTBOOL");

            Assert.Throws<OptionException>(() => Helper.Parse(),
                                           "Invalid value for flag in environment variable OPTSHELPER_FLAG: 'NOTBOOL', valid values are '0' and '1'.");
        }

        [Test]
        public void with_wrong_type_in_json()
        {
            Helper.RegisterFlag(() => Flag, "f|flag", "settings.flag", "FLAG");
            var cfg = WriteJsonConfig(new {settings = new {flag = "bla"}});

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }
    }
}