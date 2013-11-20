﻿// Copyright (c) 2012, Event Store LLP
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
using System.Net;

namespace EventStore.Core.Messages
{
    public partial class TcpClientMessageDto
    {
        public partial class ResolvedIndexedEvent
        {
            public ResolvedIndexedEvent(Data.EventRecord eventRecord, Data.EventRecord linkRecord)
                : this(eventRecord != null ? new EventRecord(eventRecord) : null,
                       linkRecord != null ? new EventRecord(linkRecord) : null)
            {
            }
        }

        public partial class ResolvedEvent
        {
            public ResolvedEvent(Data.ResolvedEvent pair)
                : this(new EventRecord(pair.Event),
                       pair.Link != null ? new EventRecord(pair.Link) : null,
                       pair.OriginalPosition.Value.CommitPosition,
                       pair.OriginalPosition.Value.PreparePosition)
            {
            }
        }

        public partial class EventRecord
        {
            public EventRecord(Data.EventRecord eventRecord)
            {
                EventStreamId = eventRecord.EventStreamId;
                EventNumber = eventRecord.EventNumber;
                EventId = eventRecord.EventId.ToByteArray();
                EventType = eventRecord.EventType;
                Data = eventRecord.Data;
                Metadata = eventRecord.Metadata;
            }
        }

        public partial class NotHandled
        {
            public partial class MasterInfo
            {
                public MasterInfo(IPEndPoint externalTcpEndPoint, IPEndPoint externalSecureTcpEndPoint, IPEndPoint externalHttpEndPoint)
                {
                    ExternalTcpAddress = externalTcpEndPoint.Address.ToString();
                    ExternalTcpPort = externalTcpEndPoint.Port;
                    ExternalSecureTcpAddress = externalSecureTcpEndPoint == null ? null : externalSecureTcpEndPoint.Address.ToString();
                    ExternalSecureTcpPort = externalSecureTcpEndPoint == null ? (int?)null : externalSecureTcpEndPoint.Port;
                    ExternalHttpAddress = externalHttpEndPoint.Address.ToString();
                    ExternalHttpPort = externalHttpEndPoint.Port;
                }
            }
        }
    }
}
