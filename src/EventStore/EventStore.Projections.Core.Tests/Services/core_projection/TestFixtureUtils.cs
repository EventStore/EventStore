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

using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    public static class TestFixtureUtils
    {
        public static IEnumerable<ClientMessage.WriteEvents> ToStream(
            this IEnumerable<ClientMessage.WriteEvents> self, string streamId)
        {
            return self.Where(v => v.EventStreamId == streamId);
        }

        public static List<ClientMessage.WriteEvents> ToStream(
            this List<ClientMessage.WriteEvents> self, string streamId)
        {
            return self.Where(v => v.EventStreamId == streamId).ToList();
        }

        public static IEnumerable<Event> OfEventType(
            this IEnumerable<ClientMessage.WriteEvents> self, string type)
        {
            return self.SelectMany(v => v.Events).Where(v => v.EventType == type);
        }

        public static IEnumerable<Event> ExceptOfEventType(
            this IEnumerable<ClientMessage.WriteEvents> self, string type)
        {
            return self.SelectMany(v => v.Events).Where(v => v.EventType != type);
        }

        public static List<Event> OfEventType(
            this List<ClientMessage.WriteEvents> self, string type)
        {
            return self.SelectMany(v => v.Events).Where(v => v.EventType == type).ToList();
        }

        public static List<ClientMessage.WriteEvents> WithEventType(
            this List<ClientMessage.WriteEvents> self, string type)
        {
            return self.Where(v => v.Events.Any(m => m.EventType == type)).ToList();
        }

        public static IEnumerable<T> OfTypes<T, T1, T2>(this IEnumerable<object> source) where T1 : T where T2 : T
        {
            return source.OfType<T>().Where(v => v is T1 || v is T2);
        }
    }
}
