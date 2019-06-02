using System;
using System.Collections.Specialized;
using System.Text;

namespace EventStore.UriTemplate {
	internal class UriTemplateVariablePathSegment : UriTemplatePathSegment {
		private readonly string _varName;

		public UriTemplateVariablePathSegment(string originalSegment, bool endsWithSlash, string varName)
			: base(originalSegment, UriTemplatePartType.Variable, endsWithSlash) {
			if (!string.IsNullOrEmpty(varName) == false) throw new Exception("bad variable segment");
			_varName = varName;
		}

		public string VarName {
			get {
				return _varName;
			}
		}
		public override void Bind(string[] values, ref int valueIndex, StringBuilder path) {
			if (valueIndex < values.Length == false) throw new Exception("Not enough values to bind");
			if (EndsWithSlash) {
				path.AppendFormat("{0}/", values[valueIndex++]);
			} else {
				path.Append(values[valueIndex++]);
			}
		}

		public override bool IsEquivalentTo(UriTemplatePathSegment other, bool ignoreTrailingSlash) {
			Ensure.NotNull(other, "why would we ever call this?");
			//if (other == null) {
			//	Fx.Assert("why would we ever call this?");
			//	return false;
			//}
			if (!ignoreTrailingSlash && (EndsWithSlash != other.EndsWithSlash)) {
				return false;
			}
			return (other.Nature == UriTemplatePartType.Variable);
		}
		public override bool IsMatch(UriTemplateLiteralPathSegment segment, bool ignoreTrailingSlash) {
			if (!ignoreTrailingSlash && (EndsWithSlash != segment.EndsWithSlash)) {
				return false;
			}
			return (!segment.IsNullOrEmpty());
		}
		public override void Lookup(string segment, NameValueCollection boundParameters) {
			Ensure.NotNullOrEmpty(segment, "How can that be? Lookup is expected to be called after IsMatch");
			boundParameters.Add(_varName, segment);
		}
	}
}
