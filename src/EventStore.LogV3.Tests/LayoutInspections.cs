using System;
using ObjectLayoutInspector;
using Xunit;
using Xunit.Abstractions;

namespace EventStore.LogV3.Tests {
	// For easily inspecting the layout of the structs
	public class LayoutInspections {
		private readonly ITestOutputHelper _output;

		public LayoutInspections(ITestOutputHelper output) {
			_output = output;
		}

		[Theory]
		[InlineData(typeof(Raw.RecordHeader))]
		[InlineData(typeof(Raw.EpochHeader))]
		[InlineData(typeof(Raw.EventHeader))]
		[InlineData(typeof(Raw.PartitionHeader))]
		[InlineData(typeof(Raw.PartitionTypeHeader))]
		[InlineData(typeof(Raw.StreamTypeHeader))]
		[InlineData(typeof(Raw.StreamWriteHeader))]
		[InlineData(typeof(Raw.EventTypeHeader))]
		[InlineData(typeof(Raw.ContentTypeHeader))]
		public void InspectLayout(Type t) {
			var layout = TypeLayout.GetLayout(t);
			_output.WriteLine($"{layout}");
		}
	}
}
