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
using EventStore.ClientAPI.Common.Utils;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using EventStore.Projections.Core.Services.Processing;
using System;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	public abstract class specification_with_projection_core_service_command_reader_started
		: specification_with_projection_core_service_command_reader {
		protected string _serviceId;
		protected Guid _uniqueStreamId;

		protected override IEnumerable<WhenStep> PreWhen() {
			_uniqueStreamId = Guid.NewGuid();
			var startCore = new ProjectionCoreServiceMessage.StartCore(_uniqueStreamId);
			var startReader = CreateWriteEvent(ProjectionNamesBuilder.BuildControlStreamName(_uniqueStreamId),
				"$response-reader-started", "{}");
			yield return new WhenStep(startCore, startReader);
			List<EventRecord> stream;
			_streams.TryGetValue("$projections-$master", out stream);
			Assume.That(stream != null);
			var lastEvent = stream.Last();
			var parsed = lastEvent.Data.ParseJson<JObject>();
			_serviceId = (string)((JValue)parsed.GetValue("id")).Value;
			Assume.That(!string.IsNullOrEmpty(_serviceId));
		}
	}
}
