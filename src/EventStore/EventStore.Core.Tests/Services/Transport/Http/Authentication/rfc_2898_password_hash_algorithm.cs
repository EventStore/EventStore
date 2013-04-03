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
using EventStore.Core.Services.Transport.Http.Authentication;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Core.Tests.Services.Transport.Http.Authentication
{
    namespace rfc_2898_password_hash_algorithm
    {
        [TestFixture]
        public class when_hashing_a_password
        {
            private Rfc2898PasswordHashAlgorithm _algorithm;
            private string _password;
            private Tuple<string, string> _hashed;

            [SetUp]
            public void SetUp()
            {
                _password = "Pa55w0rd!";
                _algorithm = new Rfc2898PasswordHashAlgorithm();
                _hashed = _algorithm.Hash(_password);
            }

            [Test]
            public void verifies_correct_password()
            {
                Assert.That(_algorithm.Verify(_password, _hashed));
            }

            [Test]
            public void does_not_verify_incorrect_password()
            {
                Assert.That(!_algorithm.Verify(_password.ToUpper(), _hashed));
            }

        }

        [TestFixture]
        public class when_hashing_a_password_twice
        {
            private Rfc2898PasswordHashAlgorithm _algorithm;
            private string _password;
            private Tuple<string, string> _first;
            private Tuple<string, string> _second;

            [SetUp]
            public void SetUp()
            {
                _password = "Pa55w0rd!";
                _algorithm = new Rfc2898PasswordHashAlgorithm();
                _first = _algorithm.Hash(_password);
                _second = _algorithm.Hash(_password);
            }

            [Test]
            public void uses_different_salt()
            {
                Assert.That(!_first.Item2.SequenceEqual(_second.Item2));
            }

            [Test]
            public void generates_different_hashes()
            {
                Assert.That(!_first.Item1.SequenceEqual(_second.Item1));
            }

        }
    }
}
