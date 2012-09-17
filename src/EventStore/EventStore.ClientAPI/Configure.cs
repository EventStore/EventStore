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
using System.Net;

namespace EventStore.ClientAPI
{
    public class Configure
    {
        internal IPAddress _address;
        internal int _port;
        internal string _name;

        private Configure(string name, IPAddress ipaddress, int port)
        {
            _name = name;
            _address = ipaddress;
            _port = port;
        }

        public static Configure AsDefault()
        {
            return new Configure("Default", IPAddress.Loopback, 1113);
        }

        public static Configure Named(string name)
        {
            return new Configure(name, IPAddress.Loopback, 1113);
        }

        public Configure ToServerNamed(string name)
        {
            var addresses = Dns.GetHostAddresses(name);
            if (addresses.Length == 0) throw new HostNotFoundException(name);
            
            return new Configure(_name, addresses[0], _port);
        }
    }

    public class HostNotFoundException : Exception
    {
        public HostNotFoundException(string name) : base("Host with name " + name + " not found")
        {
        }
    }
}