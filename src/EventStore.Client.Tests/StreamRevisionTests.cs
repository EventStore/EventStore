using System;
using System.Collections.Generic;
using Xunit;

namespace EventStore.Client {
	public class StreamRevisionTests {
		[Fact]
		public void Equality() {
			var sut = new StreamRevision(1);
			Assert.Equal(new StreamRevision(1), sut);
		}

		[Fact]
		public void Inequality() {
			var sut = new StreamRevision(1);
			Assert.NotEqual(new StreamRevision(2), sut);
		}

		[Fact]
		public void EqualityOperator() {
			var sut = new StreamRevision(1);
			Assert.True(new StreamRevision(1) == sut);
		}

		[Fact]
		public void InequalityOperator() {
			var sut = new StreamRevision(1);
			Assert.True(new StreamRevision(2) != sut);
		}

		public static IEnumerable<object[]> ArgumentOutOfRangeTestCases() {
			yield return new object[] { long.MaxValue + 1UL };
			yield return new object[] { ulong.MaxValue - 1UL };
		}

		[Theory, MemberData(nameof(ArgumentOutOfRangeTestCases))]
		public void ArgumentOutOfRange(ulong value) {
			var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new StreamRevision(value));
			Assert.Equal(nameof(value), ex.ParamName);
		}

		public static IEnumerable<object[]> ComparableTestCases() {
			yield return new object[] { StreamRevision.Start, StreamRevision.Start, 0 };
			yield return new object[] { StreamRevision.Start, StreamRevision.End, -1 };
			yield return new object[] { StreamRevision.End, StreamRevision.Start, 1 };
		}

		[Theory, MemberData(nameof(ComparableTestCases))]
		public void Comparability(StreamRevision left, StreamRevision right, int expected)
			=> Assert.Equal(expected, left.CompareTo(right));

		public static IEnumerable<object[]> Int64TestCases() {
			yield return new object[] { -1L, StreamRevision.End };
			yield return new object[] { 0L, StreamRevision.Start };
		}

		[Theory, MemberData(nameof(Int64TestCases))]
		public void FromInt64ExpectedResult(long value, StreamRevision expected)
			=> Assert.Equal(expected, StreamRevision.FromInt64(value));

		[Theory, MemberData(nameof(Int64TestCases))]
		public void ToInt64ExpectedResult(long expected, StreamRevision value)
			=> Assert.Equal(expected, value.ToInt64());

		[Fact]
		public void ExplicitConversionExpectedResult() {
			const ulong expected = 0UL;
			var actual = (ulong)new StreamRevision(expected);
			Assert.Equal(expected, actual);
		}

		[Fact]
		public void ImplicitConversionExpectedResult() {
			const ulong expected = 0UL;
			Assert.Equal(expected, new StreamRevision(expected));
		}

		[Fact]
		public void ToStringExpectedResult() {
			var expected = 0UL.ToString();

			Assert.Equal(expected, new StreamRevision(0UL).ToString());
		}
	}
}
