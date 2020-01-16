using System;
using Xunit;

namespace EventStore.Client {
	public class PrefixFilterExpressionTests {
		[Fact]
		public void Equality() {
			var sut = new PrefixFilterExpression("prefix");

			Assert.Equal(new PrefixFilterExpression("prefix"), sut);
		}

		[Fact]
		public void Inequality() {
			var sut = new PrefixFilterExpression("prefix");

			Assert.NotEqual(new PrefixFilterExpression("suffix"), sut);
		}

		[Fact]
		public void EqualityOperator() {
			var sut = new PrefixFilterExpression("prefix");

			Assert.True(new PrefixFilterExpression("prefix") == sut);
		}

		[Fact]
		public void InequalityOperator() {
			var sut = new PrefixFilterExpression("prefix");

			Assert.True(new PrefixFilterExpression("suffix") != sut);
		}

		[Fact]
		public void NullArgument() {
			var ex = Assert.Throws<ArgumentNullException>(() => new PrefixFilterExpression(null));
			Assert.Equal("value", ex.ParamName);
		}

		[Fact]
		public void ExplicitCastToStringReturnsExpectedResult() {
			var sut = new PrefixFilterExpression("prefix");
			var result = (string)sut;

			Assert.Equal("prefix", result);
		}

		[Fact]
		public void ImplicitCastToStringReturnsExpectedResult() {
			var sut = new PrefixFilterExpression("prefix");
			string result = sut;

			Assert.Equal("prefix", result);
		}

		[Fact]
		public void ToStringReturnsExpectedResult() {
			var sut = new PrefixFilterExpression("prefix");
			var result = sut.ToString();

			Assert.Equal("prefix", result);
		}
	}
}
