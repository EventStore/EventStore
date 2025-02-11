// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

namespace EventStore.SourceGenerators.Tests.Messaging.NestedDerived {
	[DerivedMessage(TestMessageGroup.NestedDerived)]
	partial class B : Message {
		[DerivedMessage(TestMessageGroup.NestedDerived)]
		private partial class A : B {
		} 
	}
}
