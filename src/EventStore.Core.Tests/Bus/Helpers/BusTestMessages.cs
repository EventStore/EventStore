// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Bus.Helpers;

public class TestMessage : Message {
}

public class TestMessage2 : Message {
}

public class TestMessage3 : Message {
}

public class ParentTestMessage : Message {
}

public class ChildTestMessage : ParentTestMessage {
}

public class GrandChildTestMessage : ChildTestMessage {
}

public class TestMessageWithId : Message {
	public readonly int Id;

	public TestMessageWithId(int id) {
		Id = id;
	}
}
