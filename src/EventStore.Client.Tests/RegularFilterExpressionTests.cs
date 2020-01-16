using System;
using System.Text.RegularExpressions;
using Xunit;

namespace EventStore.Client {
	public class RegularFilterExpressionTests {
		[Fact]
		public void Equality() {
			var sut = new RegularFilterExpression("^");

			Assert.Equal(new RegularFilterExpression("^"), sut);
		}

		[Fact]
		public void Inequality() {
			var sut = new RegularFilterExpression("^");

			Assert.NotEqual(new RegularFilterExpression("$"), sut);
		}

		[Fact]
		public void EqualityOperator() {
			var sut = new RegularFilterExpression("^");

			Assert.True(new RegularFilterExpression("^") == sut);
		}

		[Fact]
		public void InequalityOperator() {
			var sut = new RegularFilterExpression("^");

			Assert.True(new RegularFilterExpression("$") != sut);
		}

		[Fact]
		public void NullStringArgument() {
			var ex = Assert.Throws<ArgumentNullException>(() => new RegularFilterExpression((string)null));
			Assert.Equal("value", ex.ParamName);
		}

		[Fact]
		public void NullRegularExpressionArgument() {
			var ex = Assert.Throws<ArgumentNullException>(() => new RegularFilterExpression((Regex)null));
			Assert.Equal("value", ex.ParamName);
		}

		[Fact]
		public void ExplicitCastToStringReturnsExpectedResult() {
			var sut = new RegularFilterExpression("^");
			var result = (string)sut;

			Assert.Equal("^", result);
		}

		[Fact]
		public void ImplicitCastToStringReturnsExpectedResult() {
			var sut = new RegularFilterExpression("^");
			string result = sut;

			Assert.Equal("^", result);
		}

		[Fact]
		public void ToStringReturnsExpectedResult() {
			var sut = new RegularFilterExpression("^");
			var result = sut.ToString();

			Assert.Equal("^", result);
		}
	}
}
