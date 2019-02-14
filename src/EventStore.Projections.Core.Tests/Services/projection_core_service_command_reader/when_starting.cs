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
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using System;
using EventStore.Projections.Core.Services.Processing;
using System.Linq;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class when_starting : specification_with_projection_core_service_command_reader {
		protected override IEnumerable<WhenStep> When() {
			var uniqueId = Guid.NewGuid();
			var startCore = new ProjectionCoreServiceMessage.StartCore(uniqueId);
			var startReader = CreateWriteEvent(ProjectionNamesBuilder.BuildControlStreamName(uniqueId),
				"$response-reader-started", "{}");
			yield return new WhenStep(startCore, startReader);
		}

		[Test]
		public void registers_core_service() {
			AssertLastEventIs("$projections-$master", "$projection-worker-started");
			AssertLastEventJson("$projections-$master", new {Id___exists = ""});
		}
	}
}
