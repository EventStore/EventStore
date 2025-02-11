// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

namespace EventStore.SourceGenerators.Tests.Messaging.FileScopedNamespace;

[DerivedMessage(TestMessageGroup.FileScopedNamespace)]
public partial class A : Message {
}

[DerivedMessage(TestMessageGroup.FileScopedNamespace)]
public partial class B : Message {
}
