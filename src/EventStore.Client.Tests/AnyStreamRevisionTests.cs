using System;
using System.Collections.Generic;
using Xunit;

namespace EventStore.Client {
	public class AnyStreamRevisionTests {
		[Fact]
		public void Equality() {
			var sut = AnyStreamRevision.NoStream;
			Assert.Equal(AnyStreamRevision.NoStream, sut);
		}

		[Fact]
		public void Inequality() {
			var sut = AnyStreamRevision.NoStream;
			Assert.NotEqual(AnyStreamRevision.Any, sut);
		}

		[Fact]
		public void EqualityOperator() {
			var sut = AnyStreamRevision.NoStream;
			Assert.True(AnyStreamRevision.NoStream == sut);
		}

		[Fact]
		public void InequalityOperator() {
			var sut = AnyStreamRevision.NoStream;
			Assert.True(AnyStreamRevision.Any != sut);
		}

		public static IEnumerable<object[]> ArgumentOutOfRangeTestCases() {
			yield return new object[] { 0 };
			yield return new object[] { int.MaxValue };
			yield return new object[] { -3 };
		}

		[Theory, MemberData(nameof(ArgumentOutOfRangeTestCases))]
		public void ArgumentOutOfRange(int value) {
			var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new AnyStreamRevision(value));
			Assert.Equal(nameof(value), ex.ParamName);
		}


		public static IEnumerable<object[]> Int64TestCases() {
			yield return new object[] { -1L, AnyStreamRevision.NoStream };
			yield return new object[] { -2L, AnyStreamRevision.Any };
			yield return new object[] { -4L, AnyStreamRevision.StreamExists };
		}

		[Theory, MemberData(nameof(Int64TestCases))]
		public void FromInt64ExpectedResult(long value, AnyStreamRevision expected)
			=> Assert.Equal(expected, AnyStreamRevision.FromInt64(value));

		[Theory, MemberData(nameof(Int64TestCases))]
		public void ToInt64ExpectedResult(long expected, AnyStreamRevision value)
			=> Assert.Equal(expected, value.ToInt64());

		[Fact]
		public void ExplicitConversionExpectedResult() {
			const int expected = 1;
			var actual = (int)new AnyStreamRevision(expected);
			Assert.Equal(expected, actual);
		}

		[Fact]
		public void ImplicitConversionExpectedResult() {
			const int expected = 1;
			Assert.Equal(expected, new AnyStreamRevision(expected));
		}

		public static IEnumerable<object[]> ToStringTestCases() {
			yield return new object[] { AnyStreamRevision.Any, nameof(AnyStreamRevision.Any) };
			yield return new object[] { AnyStreamRevision.NoStream, nameof(AnyStreamRevision.NoStream) };
			yield return new object[] { AnyStreamRevision.StreamExists, nameof(AnyStreamRevision.StreamExists) };
		}

		[Theory, MemberData(nameof(ToStringTestCases))]
		public void ToStringExpectedResult(AnyStreamRevision sut, string expected) {
			Assert.Equal(expected, sut.ToString());
		}
	}
}
