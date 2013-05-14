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

using System.Text;
using EventStore.ClientAPI;

namespace EventStore.Core.Tests.ClientAPI.Helpers
{
    internal static class EventDataComparer
    {
        public static bool Equal(EventData expected, RecordedEvent actual)
        {
            if (expected.EventId != actual.EventId)
                return false;

            if (expected.Type != actual.EventType)
                return false;

            var expectedDataString = Encoding.UTF8.GetString(expected.Data ?? new byte[0]);
            var expectedMetadataString = Encoding.UTF8.GetString(expected.Metadata ?? new byte[0]);

            var actualDataString = Encoding.UTF8.GetString(actual.Data ?? new byte[0]);
            var actualMetadataDataString = Encoding.UTF8.GetString(actual.Metadata ?? new byte[0]);

            return expectedDataString == actualDataString && expectedMetadataString == actualMetadataDataString;
        }

        public static bool Equal(EventData[] expected, RecordedEvent[] actual)
        {
            if (expected.Length != actual.Length)
                return false;

            for (var i = 0; i < expected.Length; i++)
            {
                if (!Equal(expected[i], actual[i]))
                    return false;
            }

            return true;
        }
    }
}