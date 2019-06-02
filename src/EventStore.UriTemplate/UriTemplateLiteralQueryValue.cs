using System;
using System.Collections.Specialized;
using System.Text;

namespace EventStore.UriTemplate {
	// thin wrapper around string; use type system to help ensure we
	// are doing canonicalization right/consistently
	internal class UriTemplateLiteralQueryValue : UriTemplateQueryValue, IComparable<UriTemplateLiteralQueryValue> {
		private readonly string _value; // an unescaped representation

		private UriTemplateLiteralQueryValue(string value)
			: base(UriTemplatePartType.Literal) {
			Ensure.NotNull(value, "bad literal value");
			_value = value;
		}
		public static UriTemplateLiteralQueryValue CreateFromUriTemplate(string value) {
			return new UriTemplateLiteralQueryValue(UrlUtility.UrlDecode(value, Encoding.UTF8));
		}

		public string AsEscapedString() {
			return UrlUtility.UrlEncode(_value, Encoding.UTF8);
		}
		public string AsRawUnescapedString() {
			return _value;
		}
		public override void Bind(string keyName, string[] values, ref int valueIndex, StringBuilder query) {
			query.AppendFormat("&{0}={1}", UrlUtility.UrlEncode(keyName, Encoding.UTF8), AsEscapedString());
		}

		public int CompareTo(UriTemplateLiteralQueryValue other) {
			return string.Compare(_value, other._value, StringComparison.Ordinal);
		}

		public override bool Equals(object obj) {
			var lqv = obj as UriTemplateLiteralQueryValue;
			Ensure.NotNull(lqv, "why would we ever call this?");
			//if (lqv == null) {
			//	Fx.Assert("why would we ever call this?");
			//	return false;
			//} else {
			return _value == lqv._value;
			//}
		}
		public override int GetHashCode() {
			return _value.GetHashCode();
		}

		public override bool IsEquivalentTo(UriTemplateQueryValue other) {
			Ensure.NotNull(other, "why would we ever call this?");
			//if (other == null) {
			//	Fx.Assert("why would we ever call this?");
			//	return false;
			//}
			if (other.Nature != UriTemplatePartType.Literal) {
				return false;
			}
			var otherAsLiteral = other as UriTemplateLiteralQueryValue;
			Ensure.NotNull(otherAsLiteral, "The nature requires that this will be OK");
			return CompareTo(otherAsLiteral) == 0;
		}
		public override void Lookup(string value, NameValueCollection boundParameters) {
			var ret = string.Compare(_value, value, StringComparison.Ordinal) == 0;
			if (ret == false) {
				throw new Exception("How can that be?");
			}
		}
	}
}
