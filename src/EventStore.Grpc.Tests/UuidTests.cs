using System;
using EventStore.Grpc.PersistentSubscriptions;
using Xunit;

namespace EventStore.Grpc {
	public class UuidTests {
		[Fact]
		public void Equality() {
			var sut = Uuid.NewUuid();
			Assert.Equal(Uuid.FromGuid(sut.ToGuid()), sut);
		}

		[Fact]
		public void Inequality() {
			var sut = Uuid.NewUuid();
			Assert.NotEqual(Uuid.NewUuid(), sut);
		}

		[Fact]
		public void EqualityOperator() {
			var sut = Uuid.NewUuid();
			Assert.True(Uuid.FromGuid(sut.ToGuid()) == sut);
		}

		[Fact]
		public void InequalityOperator() {
			var sut = Uuid.NewUuid();
			Assert.True(Uuid.NewUuid() != sut);
		}

		[Fact]
		public void ArgumentNullException() {
			var ex = Assert.Throws<ArgumentNullException>(() => Uuid.Parse(null));
			Assert.Equal("value", ex.ParamName);
		}

		[Fact]
		public void ToGuidReturnsExpectedResult() {
			var guid = Guid.NewGuid();
			var sut = Uuid.FromGuid(guid);

			Assert.Equal(sut.ToGuid(), guid);
		}

		[Fact]
		public void ToStringProducesExpectedResult() {
			var sut = Uuid.NewUuid();

			Assert.Equal(sut.ToGuid().ToString(),sut.ToString());
		}

		[Fact]
		public void ToFormattedStringProducesExpectedResult() {
			var sut = Uuid.NewUuid();

			Assert.Equal(sut.ToGuid().ToString("n"),sut.ToString("n"));
		}

		[Fact]
		public void ToPersistentSubscriptionDtoReturnsExpectedResult() {
			var msb = GetRandomInt64();
			var lsb = GetRandomInt64();

			var sut = Uuid.FromInt64(msb, lsb);

			var result = sut.ToPersistentSubscriptionsDto();

			Assert.NotNull(result.Structured);
			Assert.Equal(lsb, result.Structured.LeastSignificantBits);
			Assert.Equal(msb, result.Structured.MostSignificantBits);
		}

		[Fact]
		public void ToStreamsDtoReturnsExpectedResult() {
			var msb = GetRandomInt64();
			var lsb = GetRandomInt64();

			var sut = Uuid.FromInt64(msb, lsb);

			var result = sut.ToStreamsDto();

			Assert.NotNull(result.Structured);
			Assert.Equal(lsb, result.Structured.LeastSignificantBits);
			Assert.Equal(msb, result.Structured.MostSignificantBits);
		}

		[Fact]
		public void ParseReturnsExpectedResult() {
			var guid = Guid.NewGuid();

			var sut = Uuid.Parse(guid.ToString());

			Assert.Equal(Uuid.FromGuid(guid), sut);
		}

		[Fact]
		public void FromInt64ReturnsExpectedResult() {
			var guid = Guid.Parse("65678f9b-d139-4786-8305-b9166922b378");
			var sut = Uuid.FromInt64(7306966819824813958L, -9005588373953137800L);
			var expected = Uuid.FromGuid(guid);

			Assert.Equal(expected, sut);
		}

		private static long GetRandomInt64() {
			var buffer = new byte[sizeof(long)];

			new Random().NextBytes(buffer);

			return BitConverter.ToInt64(buffer, 0);
		}
	}
}
