using System;
using System.Collections.Specialized;
using System.Text;

namespace EventStore.UriTemplate {
	// This represents a Query value, which can either be Empty, a Literal or a Variable
	internal abstract class UriTemplateQueryValue {
		private readonly UriTemplatePartType _nature;
		private static UriTemplateQueryValue _empty = new EmptyUriTemplateQueryValue();

		protected UriTemplateQueryValue(UriTemplatePartType nature) {
			_nature = nature;
		}

		public static UriTemplateQueryValue Empty {
			get {
				return _empty;
			}
		}

		public UriTemplatePartType Nature {
			get {
				return _nature;
			}
		}
		public static UriTemplateQueryValue CreateFromUriTemplate(string value, UriTemplate template) {
			// Checking for empty value
			if (value == null) {
				return Empty;
			}
			// Identifying the type of value - Literal|Compound|Variable
			switch (UriTemplateHelpers.IdentifyPartType(value)) {
				case UriTemplatePartType.Literal:
					return UriTemplateLiteralQueryValue.CreateFromUriTemplate(value);

				case UriTemplatePartType.Compound:
					throw new InvalidOperationException("UTQueryCannotHaveCompoundValue");

				case UriTemplatePartType.Variable:
					return new UriTemplateVariableQueryValue(template.AddQueryVariable(value.Substring(1, value.Length - 2)));

				default:
					throw new Exception("Invalid value from IdentifyStringNature");
			}
		}

		public static bool IsNullOrEmpty(UriTemplateQueryValue utqv) {
			if (utqv == null) {
				return true;
			}
			if (utqv == Empty) {
				return true;
			}
			return false;
		}
		public abstract void Bind(string keyName, string[] values, ref int valueIndex, StringBuilder query);

		public abstract bool IsEquivalentTo(UriTemplateQueryValue other);
		public abstract void Lookup(string value, NameValueCollection boundParameters);

		private class EmptyUriTemplateQueryValue : UriTemplateQueryValue {
			public EmptyUriTemplateQueryValue()
				: base(UriTemplatePartType.Literal) {
			}
			public override void Bind(string keyName, string[] values, ref int valueIndex, StringBuilder query) {
				query.AppendFormat("&{0}", UrlUtility.UrlEncode(keyName, Encoding.UTF8));
			}

			public override bool IsEquivalentTo(UriTemplateQueryValue other) {
				return (other == Empty);
			}
			public override void Lookup(string value, NameValueCollection boundParameters) {
				if (string.IsNullOrEmpty(value) == false) throw new Exception("shouldn't have a value");
			}
		}
	}
}
