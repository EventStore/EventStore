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
using System.IO;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class prefix_filenaming_strategy
    {
        [Test]
        public void when_constructed_with_null_path_should_throws_argumentnullexception()
        {
            Assert.Throws<ArgumentNullException>(() => new PrefixFileNamingStrategy(null, "prefix"));
        }

        [Test]
        public void when_constructed_with_null_prefix_should_throws_argumentnullexception()
        {
            Assert.Throws<ArgumentNullException>(() => new PrefixFileNamingStrategy("path", null));
        }

        [Test]
        public void when_getting_file_for_positive_index_and_no_version_appends_index_to_name_with_no_version()
        {
            var strategy = new PrefixFileNamingStrategy("path", "prefix-");
            Assert.AreEqual("path" + Path.DirectorySeparatorChar + "prefix-1", strategy.GetFilenameFor(1));
        }

        [Test]
        public void when_getting_file_for_nonnegative_index_and_version_appends_just_value()
        {
            var strategy = new PrefixFileNamingStrategy("path", "prefix-");
            Assert.AreEqual("path" + Path.DirectorySeparatorChar + "prefix-1", strategy.GetFilenameFor(1, 7));
        }

        [Test]
        public void when_getting_file_for_negative_index_throws_argumentoutofrangeexception()
        {
            var strategy = new PrefixFileNamingStrategy("Path", "prefix-");
            Assert.Throws<ArgumentOutOfRangeException>(() => strategy.GetFilenameFor(-1));
        }

        [Test]
        public void when_getting_file_for_negative_version_throws_argumentoutofrangeexception()
        {
            var strategy = new PrefixFileNamingStrategy("Path", "prefix-");
            Assert.Throws<ArgumentOutOfRangeException>(() => strategy.GetFilenameFor(0, -1));
        }
    }
}