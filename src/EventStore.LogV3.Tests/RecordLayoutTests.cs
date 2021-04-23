using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ObjectLayoutInspector;
using Xunit;

namespace EventStore.LogV3.Tests {
	public class RecordLayoutTests {
		void AssertSize<T>(int expectedSize) {
			Assert.Equal(expectedSize, Unsafe.SizeOf<T>());
			Assert.Equal(expectedSize, Marshal.SizeOf<T>());

			var layout = TypeLayout.GetLayout<T>();

			// check that fields are aligned
			foreach (var field in layout.Fields) {
				if ($"{field}".Contains("padding"))
					continue;
				Assert.True(field.Offset % field.Size == 0, $"Field {field} is not aligned");
			}

			// check that fields don't overlap
			var minOffset = 0;
			foreach (var field in layout.Fields) {
				Assert.True(field.Offset >= minOffset, $"Field {field.Offset} overlaps");
				minOffset = field.Offset + field.Size;
			}

			// note that we aren't checking whether fields are efficiently arranged...
			// also not currently checking if the whole struct is correctly aligned, only the fields within.
		}

		[Fact] public void RecordHeaderLayout() => AssertSize<Raw.RecordHeader>(Raw.RecordHeader.Size);
		[Fact] public void EpochRecordHeaderLayout() => AssertSize<Raw.EpochHeader>(Raw.EpochHeader.Size);
		[Fact] public void EventHeaderLayout() => AssertSize<Raw.EventHeader>(Raw.EventHeader.Size);
		[Fact] public void PartitionHeaderLayout() => AssertSize<Raw.PartitionHeader>(Raw.PartitionHeader.Size);
		[Fact] public void PartitionTypeHeaderLayout() => AssertSize<Raw.PartitionTypeHeader>(Raw.PartitionTypeHeader.Size);
		[Fact] public void StreamTypeHeaderLayout() => AssertSize<Raw.StreamTypeHeader>(Raw.StreamTypeHeader.Size);
		[Fact] public void StreamWriteHeaderLayout() => AssertSize<Raw.StreamWriteHeader>(Raw.StreamWriteHeader.Size);
		[Fact] public void EventTypeHeaderLayout() => AssertSize<Raw.EventTypeHeader>(Raw.EventTypeHeader.Size);
		[Fact] public void ContentTypeHeaderLayout() => AssertSize<Raw.ContentTypeHeader>(Raw.ContentTypeHeader.Size);
	}
}
