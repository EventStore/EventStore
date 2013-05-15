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
using System.Text;
using EventStore.ClientAPI;
using EventStore.Common.Utils;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class TestEvent
    {
        public static EventData NewTestEvent(int index)
        {
            var subIndex = (index % 50);
            var type = "TestEvent-" + subIndex.ToString();
            var body = new string('#', 1 + subIndex * subIndex);
            var encodedData = Helper.UTF8NoBom.GetBytes(string.Format("{0}-{1}-{2}", index, body.Length, body));

            return new EventData(Guid.NewGuid(), type, false, encodedData, new byte[0]);
        }

        public static void VerifyIfMatched(RecordedEvent evnt)
        {
            if (evnt.EventType.StartsWith("TestEvent"))
            {
                var data = Common.Utils.Helper.UTF8NoBom.GetString(evnt.Data);
                var atoms = data.Split('-');
                if (atoms.Length != 3)
                    throw new ApplicationException(string.Format("Invalid TestEvent object: currupted data format: {0}",
                                                                 RecordDetailsString(evnt)));

                var expectedLength = int.Parse(atoms[1]);
                if (expectedLength != atoms[2].Length)
                    throw new ApplicationException(string.Format("Invalid TestEvent object: not expected data length: {0}",
                                                                 RecordDetailsString(evnt)));

                if (new string('#', expectedLength) != atoms[2])
                    throw new ApplicationException(string.Format("Invalid TestEvent object: currupted data: {0}",
                                                                 RecordDetailsString(evnt)));
            }
        }

        private static string RecordDetailsString(RecordedEvent evnt)
        {
            var data = Common.Utils.Helper.UTF8NoBom.GetString(evnt.Data);
            return string.Format("[stream:{0}; eventNumber:{1}; type:{2}; data:{3}]",
                                                               evnt.EventStreamId,
                                                               evnt.EventNumber,
                                                               evnt.EventType,
                                                               data.Length > 12 ? (data.Substring(0, 12) + "...") : data);
        }
    }
}