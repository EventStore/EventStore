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
using System.Collections.Generic;
using System.Net;
using EventStore.Common.Exceptions;
using EventStore.Common.Utils;

namespace EventStore.Common.CommandLine
{
    public static class ArgumentsParser
    {
        public static IPAddress[] ParseIpsList(string ipsString)
        {
            IPAddress[] addresses;
            if (ipsString.IsEmptyString())
            {
                var list = new List<IPAddress>();
                foreach (var ipStr in ipsString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    IPAddress ip;
                    if (!IPAddress.TryParse(ipStr, out ip))
                    {
                        throw new ApplicationInitializationException(
                            string.Format("Could not parse IP address [{0}] provided in argument ManagersIPs list {1}.",
                                          ipStr,
                                          ipsString));
                    }
                    list.Add(ip);
                }
                addresses = list.ToArray();
            }
            else
            {
                addresses = new IPAddress[0];
            }
            return addresses;
        }
    }
}
