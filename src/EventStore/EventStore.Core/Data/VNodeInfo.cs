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
using EventStore.Common.Utils;

namespace EventStore.Core.Data
{
    public class VNodeInfo
    {
        public readonly Guid InstanceId;
        public readonly IPEndPoint InternalTcp;
        public readonly IPEndPoint InternalSecureTcp;
        public readonly IPEndPoint ExternalTcp;
        public readonly IPEndPoint ExternalSecureTcp;
        public readonly IPEndPoint InternalHttp;
        public readonly IPEndPoint ExternalHttp;

        public VNodeInfo(Guid instanceId,
                         IPEndPoint internalTcp, IPEndPoint internalSecureTcp,
                         IPEndPoint externalTcp, IPEndPoint externalSecureTcp,
                         IPEndPoint internalHttp, IPEndPoint externalHttp)
        {
            Ensure.NotEmptyGuid(instanceId, "instanceId");
            Ensure.NotNull(internalTcp, "internalTcp");
            Ensure.NotNull(externalTcp, "externalTcp");
            Ensure.NotNull(internalHttp, "internalHttp");
            Ensure.NotNull(externalHttp, "externalHttp");

            InstanceId = instanceId;
            InternalTcp = internalTcp;
            InternalSecureTcp = internalSecureTcp;
            ExternalTcp = externalTcp;
            ExternalSecureTcp = externalSecureTcp;
            InternalHttp = internalHttp;
            ExternalHttp = externalHttp;
        }

        public bool Is(IPEndPoint endPoint)
        {
            return endPoint != null
                   && (InternalHttp.Equals(endPoint)
                       || ExternalHttp.Equals(endPoint)
                       || InternalTcp.Equals(endPoint)
                       || (InternalSecureTcp != null && InternalSecureTcp.Equals(endPoint))
                       || ExternalTcp.Equals(endPoint)
                       || (ExternalSecureTcp != null && ExternalSecureTcp.Equals(endPoint)));
        }

        public override string ToString()
        {
            return string.Format("InstanceId: {0:B}, InternalTcp: {1}, InternalSecureTcp: {2}, " +
                                 "ExternalTcp: {3}, ExternalSecureTcp: {4}, InternalHttp: {5}, ExternalHttp: {6}",
                                 InstanceId,
                                 InternalTcp,
                                 InternalSecureTcp,
                                 ExternalTcp,
                                 ExternalSecureTcp,
                                 InternalHttp,
                                 ExternalHttp);
        }
    }
}