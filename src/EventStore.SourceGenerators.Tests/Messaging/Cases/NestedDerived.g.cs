﻿// autogenerated
using System.Threading;

#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
namespace EventStore.SourceGenerators.Tests.Messaging.NestedDerived
{
	partial class B
	{
		private partial class A
		{
			public static string OriginalLabelStatic { get; } = "TestMessageGroup-NestedDerived-A";
			public static string LabelStatic { get; set; } = "TestMessageGroup-NestedDerived-A";
			public override string Label => LabelStatic;
		}

		public static string OriginalLabelStatic { get; } = "TestMessageGroup-NestedDerived-B";
		public static string LabelStatic { get; set; } = "TestMessageGroup-NestedDerived-B";
		public override string Label => LabelStatic;
	}
}
