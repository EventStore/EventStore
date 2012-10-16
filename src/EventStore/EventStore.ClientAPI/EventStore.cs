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
using System.Net;
using System.Threading.Tasks;

namespace EventStore.ClientAPI
{
    public static class EventStore
    {
        private static string _name;
        private static EventStoreConnection _connection;

        public static void Configure(Configure args)
        {
            _name = args._name;
            _connection = new EventStoreConnection(new IPEndPoint(args._address, args._port));
        }

        public static EventStreamSlice ReadEventStream(string stream, int start, int count)
        {
            return _connection.ReadEventStreamForward(stream, start, count);
        }

        public static Task<EventStreamSlice> ReadEventStreamAsync(string stream, int start, int count)
        {
            return _connection.ReadEventStreamForwardAsync(stream, start, count);
        }

        public static void CreateStream(string stream, byte[] metadata)
        {
            _connection.CreateStream(stream, metadata);
        }

        public static Task CreateStreamAsync(string stream, byte[] metadata)
        {
            return _connection.CreateStreamAsync(stream, metadata);
        }

        public static void AppendToStream(string stream, int expectedVersion, IEnumerable<IEvent> events)
        {
            _connection.AppendToStream(stream, expectedVersion, events);
        }

        public static Task AppendToStreamAsync(string stream, int expectedVersion, IEnumerable<IEvent> events)
        {
            return _connection.AppendToStreamAsync(stream, expectedVersion, events);
        }

        public static void DeleteStream(string stream, int expectedVersion)
        {
            _connection.DeleteStream(stream, expectedVersion); 
        }

        public static Task DeleteStreamAsync(string stream, int expectedVersion)
        {
            return _connection.DeleteStreamAsync(stream, expectedVersion);
        }

        public static void DeleteStream(string stream)
        {
            _connection.DeleteStream(stream, ExpectedVersion.Any);
        }

        public static Task DeleteStreamAsync(string stream)
        {
            return _connection.DeleteStreamAsync(stream, ExpectedVersion.Any);
        }

        public static void Close()
        {
            _connection.Close();
        }
    }
}