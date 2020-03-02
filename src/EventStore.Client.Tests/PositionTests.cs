using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EventStore.Client {
	public class PositionTests {
		[Fact]
		public void Equality() {
			var sut = new Position(6, 5);

			Assert.Equal(new Position(6, 5), sut);
		}

		[Fact]
		public void Inequality() {
			var sut = new Position(6, 5);

			Assert.NotEqual(new Position(7, 6), sut);
		}

		[Fact]
		public void EqualityOperator() {
			var sut = new Position(6, 5);

			Assert.True(new Position(6, 5) == sut);
		}

		[Fact]
		public void InequalityOperator() {
			var sut = new Position(6, 5);

			Assert.True(new Position(7, 6) != sut);
		}

		public static IEnumerable<object[]> ArgumentOutOfRangeTestCases() {
			const string commitPosition = nameof(commitPosition);
			const string preparePosition = nameof(preparePosition);

			yield return new object[] {5, 6, commitPosition};
			yield return new object[] {ulong.MaxValue - 1, 6, commitPosition};
			yield return new object[] {ulong.MaxValue, ulong.MaxValue - 1, preparePosition};
			yield return new object[] {(ulong)long.MaxValue + 1, long.MaxValue, commitPosition};
		}

		[Theory, MemberData(nameof(ArgumentOutOfRangeTestCases))]
		public void ArgumentOutOfRange(ulong commitPosition, ulong preparePosition, string name) {
			var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Position(commitPosition, preparePosition));
			Assert.Equal(name, ex.ParamName);
		}

		public static IEnumerable<object[]> GreaterThanTestCases() {
			yield return new object[] {new Position(6, 6), new Position(6, 5)};
			yield return new object[] {Position.End, new Position(ulong.MaxValue, 0),};
		}

		[Theory, MemberData(nameof(GreaterThanTestCases))]
		public void GreaterThan(Position left, Position right) => Assert.True(left > right);

		public static IEnumerable<object[]> GreaterThanOrEqualToTestCases()
			=> GreaterThanTestCases().Concat(new[] {new object[] {Position.Start, Position.Start}});

		[Theory, MemberData(nameof(GreaterThanOrEqualToTestCases))]
		public void GreaterThanOrEqualTo(Position left, Position right) => Assert.True(left >= right);

		public static IEnumerable<object[]> LessThanTestCases() {
			yield return new object[] {new Position(6, 5), new Position(6, 6)};
			yield return new object[] {new Position(ulong.MaxValue, 0), Position.End,};
		}

		[Theory, MemberData(nameof(LessThanTestCases))]
		public void LessThan(Position left, Position right) => Assert.True(left < right);

		public static IEnumerable<object[]> LessThanOrEqualToTestCases()
			=> LessThanTestCases().Concat(new[] {new object[] {Position.End, Position.End}});

		[Theory, MemberData(nameof(LessThanOrEqualToTestCases))]
		public void LessThanOrEqualTo(Position left, Position right) => Assert.True(left <= right);

		public static IEnumerable<object[]> CompareToTestCases() {
			yield return new object[] {new Position(6, 5), new Position(6, 6), -1};
			yield return new object[] {new Position(6, 6), new Position(6, 5), 1};
			yield return new object[] {new Position(6, 6), new Position(6, 6), 0};
		}

		[Theory, MemberData(nameof(CompareToTestCases))]
		public void CompareTo(Position left, Position right, int expected) =>
			Assert.Equal(expected, left.CompareTo(right));
	}
}
