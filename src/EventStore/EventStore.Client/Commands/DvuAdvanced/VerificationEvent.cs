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
using System.Linq;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;

namespace EventStore.TestClient.Commands.DvuAdvanced
{
    public class VerificationEvent
    {
        public readonly Event Event;

        public readonly string EventStreamId;
        public readonly int ExpectedVersion;
        public readonly int ShouldBeVersion;

        public VerificationEvent(Event @event, 
                                 string eventStreamId, 
                                 int expectedVersion, 
                                 int shouldBeVersion)
        {
            Ensure.NotNull(@event, "event");
            Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");

            Event = @event;
            EventStreamId = eventStreamId;

            ExpectedVersion = expectedVersion;
            ShouldBeVersion = shouldBeVersion;
        }

        public VerificationResult VerifyThat(Event actualEvent, int actualVersion)
        {
            var sb = new StringBuilder();

            if (ShouldBeVersion != Core.Data.ExpectedVersion.Any && actualVersion != ShouldBeVersion)
                sb.AppendFormat("- Actual version is wrong. Expected: {0}, actual: {1}.\n", ShouldBeVersion, actualVersion);

            if (actualEvent == null)
            {
                sb.AppendFormat("- Actual event is NULL!\n");
                return new VerificationResult(ComparisonStatus.Fail, sb.ToString());
            }

            if (actualEvent.EventId != Event.EventId)
                sb.AppendFormat("- Wrong EventId. Expected: {0}, actual: {1}.\n", Event.EventId, actualEvent.EventId);
            if (actualEvent.EventType != Event.EventType)
                sb.AppendFormat("- Wrong EventType. Expected: {0}, actual: {1}.\n", Event.EventType, actualEvent.EventType);
            
            if (actualEvent.Data.Length != Event.Data.Length)
                sb.AppendFormat("- Wrong length of data. Expected length: {0}, actual length: {1}.\n", Event.Data.Length, actualEvent.Data.Length);
            else if (actualEvent.Data.Zip(Event.Data, Tuple.Create).Any(x => x.Item1 != x.Item2))
                sb.AppendFormat("- Wrong data.\n");

            if (actualEvent.Metadata.Length != Event.Metadata.Length)
                sb.AppendFormat("- Wrong length of metadata. Expected length: {0}, actual length: {1}.\n", Event.Metadata.Length, actualEvent.Metadata.Length);
            else if (actualEvent.Metadata.Zip(Event.Metadata, Tuple.Create).Any(x => x.Item1 != x.Item2))
                sb.AppendFormat("- Wrong metadata.\n");

            return new VerificationResult(sb.Length == 0 ? ComparisonStatus.Success : ComparisonStatus.Fail, sb.ToString());
        }
    }

    public class VerificationResult
    {
        public readonly ComparisonStatus Status;
        public readonly string Description;

        public VerificationResult(ComparisonStatus status, string description)
        {
            Status = status;
            Description = description;
        }
    }

    public enum ComparisonStatus
    {
        Success,
        Fail
    }
}