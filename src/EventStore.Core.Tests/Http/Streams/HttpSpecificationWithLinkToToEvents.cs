// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common;
using EventStore.Core.Tests.ClientAPI.Helpers;

namespace EventStore.Core.Tests.Http.Streams;

public abstract class HttpSpecificationWithLinkToToEvents : HttpBehaviorSpecification {
	protected string LinkedStreamName;
	protected string StreamName;
	protected string Stream2Name;

	protected override async Task Given() {
		var creds = DefaultData.AdminCredentials;
		LinkedStreamName = Guid.NewGuid().ToString();
		StreamName = Guid.NewGuid().ToString();
		Stream2Name = Guid.NewGuid().ToString();
		using (var conn = TestConnection.Create(_node.TcpEndPoint)) {
			await conn.ConnectAsync();
			await conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, creds,
					new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
						new byte[0]));
			await conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, creds,
					new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
						new byte[0]));
			await conn.AppendToStreamAsync(Stream2Name, ExpectedVersion.Any, creds,
				new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
					new byte[0]));
			await conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
				new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo, false,
					Encoding.UTF8.GetBytes("0@" + Stream2Name), new byte[0]));
			await conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
				new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo, false,
					Encoding.UTF8.GetBytes("1@" + StreamName), new byte[0]));
		}
	}
}
