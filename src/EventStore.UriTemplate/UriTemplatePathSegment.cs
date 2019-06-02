using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;

namespace EventStore.UriTemplate {
	// This represents a Path segment, which can either be a Literal, a Variable or a Compound
	[DebuggerDisplay("Segment={_originalSegment} Nature={_nature}")]
	internal abstract class UriTemplatePathSegment {
		private readonly bool _endsWithSlash;
		private readonly UriTemplatePartType _nature;
		private readonly string _originalSegment;

		protected UriTemplatePathSegment(string originalSegment, UriTemplatePartType nature,
			bool endsWithSlash) {
			_originalSegment = originalSegment;
			_nature = nature;
			_endsWithSlash = endsWithSlash;
		}
		public bool EndsWithSlash {
			get {
				return _endsWithSlash;
			}
		}
		public UriTemplatePartType Nature {
			get {
				return _nature;
			}
		}

		public string OriginalSegment {
			get {
				return _originalSegment;
			}
		}
		public static UriTemplatePathSegment CreateFromUriTemplate(string segment, UriTemplate template) {
			// Identifying the type of segment - Literal|Compound|Variable
			switch (UriTemplateHelpers.IdentifyPartType(segment)) {
				case UriTemplatePartType.Literal:
					return UriTemplateLiteralPathSegment.CreateFromUriTemplate(segment, template);

				case UriTemplatePartType.Compound:
					return UriTemplateCompoundPathSegment.CreateFromUriTemplate(segment, template);

				case UriTemplatePartType.Variable:
					if (segment.EndsWith("/", StringComparison.Ordinal)) {
						var varName = template.AddPathVariable(UriTemplatePartType.Variable,
							segment.Substring(1, segment.Length - 3));
						return new UriTemplateVariablePathSegment(segment, true, varName);
					} else {
						var varName = template.AddPathVariable(UriTemplatePartType.Variable,
							segment.Substring(1, segment.Length - 2));
						return new UriTemplateVariablePathSegment(segment, false, varName);
					}

				default:
					throw new Exception("Invalid value from IdentifyStringNature");
			}
		}
		public abstract void Bind(string[] values, ref int valueIndex, StringBuilder path);

		public abstract bool IsEquivalentTo(UriTemplatePathSegment other, bool ignoreTrailingSlash);
		public bool IsMatch(UriTemplateLiteralPathSegment segment) {
			return IsMatch(segment, false);
		}
		public abstract bool IsMatch(UriTemplateLiteralPathSegment segment, bool ignoreTrailingSlash);
		public abstract void Lookup(string segment, NameValueCollection boundParameters);
	}
}
