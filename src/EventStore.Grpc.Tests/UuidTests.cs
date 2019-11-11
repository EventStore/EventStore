using System;
using System.Collections.Generic;
using Xunit;

namespace EventStore.Grpc {
	public class UuidTests {
		[Fact]
		public void Equality() {
			var sut = Uuid.NewUuid();
			Assert.Equal(new Uuid(sut.ToSpan()), sut);
		}

		[Fact]
		public void Inequality() {
			var sut = Uuid.NewUuid();
			Assert.NotEqual(Uuid.NewUuid(), sut);
		}

		[Fact]
		public void EqualityOperator() {
			var sut = Uuid.NewUuid();
			Assert.True(new Uuid(sut.ToSpan()) == sut);
		}

		[Fact]
		public void InequalityOperator() {
			var sut = Uuid.NewUuid();
			Assert.True(Uuid.NewUuid() != sut);
		}

		public static IEnumerable<object[]> ArgumentOutOfRangeTestCases() {
			yield return new object[] {Array.Empty<byte>()};
			yield return new object[] {new byte[15]};
			yield return new object[] {new byte[17]};
		}

		[Theory, MemberData(nameof(ArgumentOutOfRangeTestCases))]
		public void ArgumentOutOfRange(byte[] value) {
			var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Uuid(value));
			Assert.Equal(nameof(value), ex.ParamName);
		}

		[Fact]
		public void ArgumentNullException() {
			var ex = Assert.Throws<ArgumentNullException>(() => new Uuid(null));
			Assert.Equal("value", ex.ParamName);
		}

		[Fact]
		public void FromGuidReturnsExpectedResult() {
			var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
			
			var bytes = new byte[] {
				0x0,
				0x11,
				0x22,
				0x33,
				0x44,
				0x55,
				0x66,
				0x77,
				0x88,
				0x99,
				0xaa,
				0xbb,
				0xcc,
				0xdd,
				0xee,
				0xff
			};

			Assert.Equal(Uuid.FromGuid(guid), new Uuid(bytes));
		}

		[Fact]
		public void ToGuidReturnsExpectedResult() {
			var guid = Guid.NewGuid();
			var sut = Uuid.FromGuid(guid);

			Assert.Equal(sut.ToGuid(), guid);
		}

		[Fact]
		public void ToStringReturnsExpectedResult() {
			var guid = Guid.NewGuid();
			var sut = Uuid.FromGuid(guid);

			Assert.Equal(guid.ToString("n"), sut.ToString()); // the string representation of guid is in the correct order
		}
	}
}
