// Copyright (c) 2012, Event Store Ltd
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
// Neither the name of the Event Store Ltd nor the names of its
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
using System.Text;
using EventStore.Core.Data;

namespace EventStore.TestClient.Commands.DvuAdvanced.Workers
{
    public class TestProducer : IProducer
    {
        private const string StreamId = "test-producer-stream";

        private int _expected = -1;
        private int _shouldBe = 1;

        public VerificationEvent Next()
        {
            var expected = _expected == -1 ? _expected : _expected + 1;
            var shouldBe = _shouldBe;

            _expected++;
            _shouldBe++;

            var evnt = new Event(Guid.NewGuid(), "TEST", false,  Encoding.UTF8.GetBytes(string.Format("TEST-DATA exp: {0}. shd: {1}.", expected, shouldBe)), Encoding.UTF8.GetBytes(string.Format("TEST-METADATA exp: {0}. shd: {1}.", expected, shouldBe)));
            return new VerificationEvent(evnt, StreamId, expected, shouldBe);
        }
    }
}
