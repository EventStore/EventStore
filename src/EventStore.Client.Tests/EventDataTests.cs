using System;
using Xunit;

namespace EventStore.Client {
	public class EventDataTests {
		[Fact]
		public void EmptyEventIdThrows() {
			var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
				new EventData(Uuid.Empty, "-", Array.Empty<byte>()));
			Assert.Equal("eventId", ex.ParamName);
		}

		[Fact]
		public void NullTypeThrows() {
			var ex = Assert.Throws<ArgumentNullException>(
				() => new EventData(Uuid.NewUuid(), null, Array.Empty<byte>()));

			Assert.Equal("type", ex.ParamName);
		}

		[Fact]
		public void NullContentTypeThrows() {
			var ex = Assert.Throws<ArgumentNullException>(
				() => new EventData(Uuid.NewUuid(), "-", Array.Empty<byte>(), contentType: null));

			Assert.Equal("contentType", ex.ParamName);
		}

		[Fact]
		public void MalformedContentTypeThrows() {
			Assert.Throws<FormatException>(
				() => new EventData(Uuid.NewUuid(), "-", Array.Empty<byte>(), contentType: "application"));
		}

		[Fact]
		public void InvalidContentTypeThrows() {
			var ex = Assert.Throws<ArgumentOutOfRangeException>(
				() => new EventData(Uuid.NewUuid(), "-", Array.Empty<byte>(), contentType: "application/xml"));
			Assert.Equal("contentType", ex.ParamName);
		}
	}
}
