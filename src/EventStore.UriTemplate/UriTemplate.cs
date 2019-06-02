using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Threading;

namespace EventStore.UriTemplate {
	public class UriTemplate {
		internal readonly int FirstOptionalSegment;

		internal readonly string OriginalTemplate;
		internal readonly Dictionary<string, UriTemplateQueryValue> Queries; // keys are original case specified in UriTemplate constructor, dictionary ignores case
		internal readonly List<UriTemplatePathSegment> Segments;
		internal const string WildcardPath = "*";
		private readonly Dictionary<string, string> _additionalDefaults; // keys are original case specified in UriTemplate constructor, dictionary ignores case
		private readonly string _fragment;

		private readonly bool _ignoreTrailingSlash;

		private const string NullableDefault = "null";
		private readonly WildcardInfo _wildcard;
		private IDictionary<string, string> _defaults;
		private ConcurrentDictionary<string, string> _unescapedDefaults;

		private VariablesCollection _variables;

		// constructors validates that template is well-formed
		public UriTemplate(string template)
			: this(template, false) {
		}
		public UriTemplate(string template, bool ignoreTrailingSlash)
			: this(template, ignoreTrailingSlash, null) {
		}
		public UriTemplate(string template, IDictionary<string, string> additionalDefaults)
			: this(template, false, additionalDefaults) {
		}
		public UriTemplate(string template, bool ignoreTrailingSlash, IDictionary<string, string> additionalDefaults) {
			if (template == null) {
				//throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("template");
				throw new ArgumentNullException(nameof(template));
			}
			OriginalTemplate = template;
			_ignoreTrailingSlash = ignoreTrailingSlash;
			Segments = new List<UriTemplatePathSegment>();
			Queries = new Dictionary<string, UriTemplateQueryValue>(StringComparer.OrdinalIgnoreCase);

			// parse it
			string pathTemplate;
			string queryTemplate;
			// ignore a leading slash
			if (template.StartsWith("/", StringComparison.Ordinal)) {
				template = template.Substring(1);
			}
			// pull out fragment
			var fragmentStart = template.IndexOf('#');
			if (fragmentStart == -1) {
				_fragment = "";
			} else {
				_fragment = template.Substring(fragmentStart + 1);
				template = template.Substring(0, fragmentStart);
			}
			// pull out path and query
			var queryStart = template.IndexOf('?');
			if (queryStart == -1) {
				queryTemplate = string.Empty;
				pathTemplate = template;
			} else {
				queryTemplate = template.Substring(queryStart + 1);
				pathTemplate = template.Substring(0, queryStart);
			}
			template = null; // to ensure we don't accidentally reference this variable any more

			// setup path template and validate
			if (!string.IsNullOrEmpty(pathTemplate)) {
				var startIndex = 0;
				while (startIndex < pathTemplate.Length) {
					// Identify the next segment
					var endIndex = pathTemplate.IndexOf('/', startIndex);
					string segment;
					if (endIndex != -1) {
						segment = pathTemplate.Substring(startIndex, endIndex + 1 - startIndex);
						startIndex = endIndex + 1;
					} else {
						segment = pathTemplate.Substring(startIndex);
						startIndex = pathTemplate.Length;
					}
					// Checking for wildcard segment ("*") or ("{*<var name>}")
					UriTemplatePartType wildcardType;
					if ((startIndex == pathTemplate.Length) &&
						UriTemplateHelpers.IsWildcardSegment(segment, out wildcardType)) {
						switch (wildcardType) {
							case UriTemplatePartType.Literal:
								_wildcard = new WildcardInfo(this);
								break;

							case UriTemplatePartType.Variable:
								_wildcard = new WildcardInfo(this, segment);
								break;

							default:
								throw new Exception("Error in identifying the type of the wildcard segment");
						}
					} else {
						Segments.Add(UriTemplatePathSegment.CreateFromUriTemplate(segment, this));
					}
				}
			}

			// setup query template and validate
			if (!string.IsNullOrEmpty(queryTemplate)) {
				var startIndex = 0;
				while (startIndex < queryTemplate.Length) {
					// Identify the next query part
					var endIndex = queryTemplate.IndexOf('&', startIndex);
					var queryPartStart = startIndex;
					int queryPartEnd;
					if (endIndex != -1) {
						queryPartEnd = endIndex;
						startIndex = endIndex + 1;
						if (startIndex >= queryTemplate.Length) {
							throw new InvalidOperationException("UTQueryCannotEndInAmpersand");
						}
					} else {
						queryPartEnd = queryTemplate.Length;
						startIndex = queryTemplate.Length;
					}
					// Checking query part type; identifying key and value
					var equalSignIndex = queryTemplate.IndexOf('=', queryPartStart, queryPartEnd - queryPartStart);
					string key;
					string value;
					if (equalSignIndex >= 0) {
						key = queryTemplate.Substring(queryPartStart, equalSignIndex - queryPartStart);
						value = queryTemplate.Substring(equalSignIndex + 1, queryPartEnd - equalSignIndex - 1);
					} else {
						key = queryTemplate.Substring(queryPartStart, queryPartEnd - queryPartStart);
						value = null;
					}
					if (string.IsNullOrEmpty(key)) {
						throw new InvalidOperationException("UTQueryCannotHaveEmptyName");
					}
					if (UriTemplateHelpers.IdentifyPartType(key) != UriTemplatePartType.Literal) {
						throw new ArgumentException($"UTQueryMustHaveLiteralNames {OriginalTemplate}");
					}
					// Adding a new entry to the queries dictionary
					key = UrlUtility.UrlDecode(key, Encoding.UTF8);
					if (Queries.ContainsKey(key)) {
						throw new InvalidOperationException("UTQueryNamesMustBeUnique");
					}
					Queries.Add(key, UriTemplateQueryValue.CreateFromUriTemplate(value, this));
				}
			}

			// Process additional defaults (if has some) :
			if (additionalDefaults != null) {
				if (_variables == null) {
					if (additionalDefaults.Count > 0) {
						_additionalDefaults = new Dictionary<string, string>(additionalDefaults, StringComparer.OrdinalIgnoreCase);
					}
				} else {
					foreach (var kvp in additionalDefaults) {
						var uppercaseKey = kvp.Key.ToUpperInvariant();
						if ((_variables.DefaultValues != null) && _variables.DefaultValues.ContainsKey(uppercaseKey)) {
							throw new ArgumentException($"UTAdditionalDefaultIsInvalid {kvp.Key} {OriginalTemplate}");
						}
						if (_variables.PathSegmentVariableNames.Contains(uppercaseKey)) {
							_variables.AddDefaultValue(uppercaseKey, kvp.Value);
						} else if (_variables.QueryValueVariableNames.Contains(uppercaseKey)) {
							throw new InvalidOperationException($"UTDefaultValueToQueryVarFromAdditionalDefaults OriginalTemplate:{OriginalTemplate},uppercaseKey:{uppercaseKey}"); //, OriginalTemplate,
						} else if (string.Compare(kvp.Value, NullableDefault, StringComparison.OrdinalIgnoreCase) == 0) {
							throw new InvalidOperationException($"UTNullableDefaultAtAdditionalDefaults OriginalTemplate:{OriginalTemplate},uppercaseKey:{uppercaseKey}"); 
						} else {
							if (_additionalDefaults == null) {
								_additionalDefaults = new Dictionary<string, string>(additionalDefaults.Count, StringComparer.OrdinalIgnoreCase);
							}
							_additionalDefaults.Add(kvp.Key, kvp.Value);
						}
					}
				}
			}

			// Validate defaults (if should)
			if ((_variables != null) && (_variables.DefaultValues != null)) {
				_variables.ValidateDefaults(out FirstOptionalSegment);
			} else {
				FirstOptionalSegment = Segments.Count;
			}
		}

		public IDictionary<string, string> Defaults {
			get {
				if (_defaults == null) {
					Interlocked.CompareExchange<IDictionary<string, string>>(ref _defaults, new UriTemplateDefaults(this), null);
				}
				return _defaults;
			}
		}
		public bool IgnoreTrailingSlash {
			get {
				return _ignoreTrailingSlash;
			}
		}
		public ReadOnlyCollection<string> PathSegmentVariableNames {
			get {
				if (_variables == null) {
					return VariablesCollection.EmptyCollection;
				} else {
					return _variables.PathSegmentVariableNames;
				}
			}
		}
		public ReadOnlyCollection<string> QueryValueVariableNames {
			get {
				if (_variables == null) {
					return VariablesCollection.EmptyCollection;
				} else {
					return _variables.QueryValueVariableNames;
				}
			}
		}

		internal bool HasNoVariables {
			get {
				return (_variables == null);
			}
		}
		internal bool HasWildcard {
			get {
				return (_wildcard != null);
			}
		}

		// make a Uri by subbing in the values, throw on bad input
		public Uri BindByName(Uri baseAddress, IDictionary<string, string> parameters) {
			return BindByName(baseAddress, parameters, false);
		}
		public Uri BindByName(Uri baseAddress, IDictionary<string, string> parameters, bool omitDefaults) {
			if (baseAddress == null) {
				throw new ArgumentNullException(nameof(baseAddress));
			}
			if (!baseAddress.IsAbsoluteUri) {
				throw new ArgumentException($"UTBadBaseAddress");
			}

			BindInformation bindInfo;
			if (_variables == null) {
				bindInfo = PrepareBindInformation(parameters, omitDefaults);
			} else {
				bindInfo = _variables.PrepareBindInformation(parameters, omitDefaults);
			}
			return Bind(baseAddress, bindInfo, omitDefaults);
		}
		public Uri BindByName(Uri baseAddress, NameValueCollection parameters) {
			return BindByName(baseAddress, parameters, false);
		}
		public Uri BindByName(Uri baseAddress, NameValueCollection parameters, bool omitDefaults) {
			if (baseAddress == null) {
				throw new ArgumentNullException(nameof(baseAddress));
			}
			if (!baseAddress.IsAbsoluteUri) {
				throw new ArgumentException($"UTBadBaseAddress {baseAddress}");
			}

			BindInformation bindInfo;
			if (_variables == null) {
				bindInfo = PrepareBindInformation(parameters, omitDefaults);
			} else {
				bindInfo = _variables.PrepareBindInformation(parameters, omitDefaults);
			}
			return Bind(baseAddress, bindInfo, omitDefaults);
		}
		public Uri BindByPosition(Uri baseAddress, params string[] values) {
			if (baseAddress == null) {
				throw new ArgumentNullException(nameof(baseAddress));
			}
			if (!baseAddress.IsAbsoluteUri) {
				throw new ArgumentException($"UTBadBaseAddress {baseAddress}");
			}

			BindInformation bindInfo;
			if (_variables == null) {
				if (values.Length > 0) {
					throw new FormatException($"UTBindByPositionNoVariables template: {OriginalTemplate} val_length: {values.Length}");
				}
				bindInfo = new BindInformation(_additionalDefaults);
			} else {
				bindInfo = _variables.PrepareBindInformation(values);
			}
			return Bind(baseAddress, bindInfo, false);
		}

		// A note about UriTemplate equivalency:
		//  The introduction of defaults and, more over, terminal defaults, broke the simple
		//  intuative notion of equivalency between templates. We will define equivalent
		//  templates as such based on the structure of them and not based on the set of uri
		//  that are matched by them. The result is that, even though they do not match the
		//  same set of uri's, the following templates are equivalent:
		//      - "/foo/{bar}"
		//      - "/foo/{bar=xyz}"
		//  A direct result from the support for 'terminal defaults' is that the IsPathEquivalentTo
		//  method, which was used both to determine the equivalence between templates, as 
		//  well as verify that all the templates, combined together in the same PathEquivalentSet, 
		//  are equivalent in thier path is no longer valid for both purposes. We will break 
		//  it to two distinct methods, each will be called in a different case.
		public bool IsEquivalentTo(UriTemplate other) {
			if (other == null) {
				return false;
			}
			if (other.Segments == null || other.Queries == null) {
				// they never are null, but PreSharp is complaining, 
				// and warning suppression isn't working
				return false;
			}
			if (!IsPathFullyEquivalent(other)) {
				return false;
			}
			if (!IsQueryEquivalent(other)) {
				return false;
			}

			Ensure.Equal(UriTemplateEquivalenceComparer.Instance.GetHashCode(this),
				UriTemplateEquivalenceComparer.Instance.GetHashCode(other), "bad GetHashCode impl");
			return true;
		}

		public UriTemplateMatch Match(Uri baseAddress, Uri candidate) {
			if (baseAddress == null) {
				throw new ArgumentNullException(nameof(baseAddress));
			}
			if (!baseAddress.IsAbsoluteUri) {
				throw new ArgumentException($"UTBadBaseAddress {baseAddress}");
			}
			if (candidate == null) {
				throw new ArgumentNullException(nameof(candidate));
			}

			// ensure that the candidate is 'under' the base address
			if (!candidate.IsAbsoluteUri) {
				return null;
			}
			var basePath = UriTemplateHelpers.GetUriPath(baseAddress);
			var candidatePath = UriTemplateHelpers.GetUriPath(candidate);
			if (candidatePath.Length < basePath.Length) {
				return null;
			}
			if (!candidatePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) {
				return null;
			}

			// Identifying the relative segments \ checking matching to the path :
			var numSegmentsInBaseAddress = baseAddress.Segments.Length;
			var candidateSegments = candidate.Segments;
			int numMatchedSegments;
			Collection<string> relativeCandidateSegments;
			if (!IsCandidatePathMatch(numSegmentsInBaseAddress, candidateSegments,
				out numMatchedSegments, out relativeCandidateSegments)) {
				return null;
			}
			// Checking matching to the query (if should) :
			NameValueCollection candidateQuery = null;
			if (!UriTemplateHelpers.CanMatchQueryTrivially(this)) {
				candidateQuery = UriTemplateHelpers.ParseQueryString(candidate.Query);
				if (!UriTemplateHelpers.CanMatchQueryInterestingly(this, candidateQuery, false)) {
					return null;
				}
			}

			// We matched; lets build the UriTemplateMatch
			return CreateUriTemplateMatch(baseAddress, candidate, null, numMatchedSegments,
				relativeCandidateSegments, candidateQuery);
		}

		public override string ToString() {
			return OriginalTemplate;
		}

		internal string AddPathVariable(UriTemplatePartType sourceNature, string varDeclaration) {
			bool hasDefaultValue;
			return AddPathVariable(sourceNature, varDeclaration, out hasDefaultValue);
		}
		internal string AddPathVariable(UriTemplatePartType sourceNature, string varDeclaration,
			out bool hasDefaultValue) {
			if (_variables == null) {
				_variables = new VariablesCollection(this);
			}
			return _variables.AddPathVariable(sourceNature, varDeclaration, out hasDefaultValue);
		}
		internal string AddQueryVariable(string varDeclaration) {
			if (_variables == null) {
				_variables = new VariablesCollection(this);
			}
			return _variables.AddQueryVariable(varDeclaration);
		}

		internal UriTemplateMatch CreateUriTemplateMatch(Uri baseUri, Uri uri, object data,
			int numMatchedSegments, Collection<string> relativePathSegments, NameValueCollection uriQuery) {
			var result = new UriTemplateMatch();
			result.RequestUri = uri;
			result.BaseUri = baseUri;
			if (uriQuery != null) {
				result.SetQueryParameters(uriQuery);
			}
			result.SetRelativePathSegments(relativePathSegments);
			result.Data = data;
			result.Template = this;
			for (var i = 0; i < numMatchedSegments; i++) {
				Segments[i].Lookup(result.RelativePathSegments[i], result.BoundVariables);
			}
			if (_wildcard != null) {
				_wildcard.Lookup(numMatchedSegments, result.RelativePathSegments,
					result.BoundVariables);
			} else if (numMatchedSegments < Segments.Count) {
				BindTerminalDefaults(numMatchedSegments, result.BoundVariables);
			}
			if (Queries.Count > 0) {
				foreach (var kvp in Queries) {
					kvp.Value.Lookup(result.QueryParameters[kvp.Key], result.BoundVariables);
					//UriTemplateHelpers.AssertCanonical(varName);
				}
			}
			if (_additionalDefaults != null) {
				foreach (var kvp in _additionalDefaults) {
					result.BoundVariables.Add(kvp.Key, UnescapeDefaultValue(kvp.Value));
				}
			}

			Ensure.Nonnegative(result.RelativePathSegments.Count - numMatchedSegments, "bad segment computation");
			result.SetWildcardPathSegmentsStart(numMatchedSegments);

			return result;
		}

		internal bool IsPathPartiallyEquivalentAt(UriTemplate other, int segmentsCount) {
			// Refer to the note on template equivalency at IsEquivalentTo
			// This method checks if any uri with given number of segments, which can be matched
			//  by this template, can be also matched by the other template.
			// original asserts 
			//Fx.Assert(segmentsCount >= this.firstOptionalSegment - 1, "How can that be? The Trie is constructed that way!");
			//Fx.Assert(segmentsCount <= this.segments.Count, "How can that be? The Trie is constructed that way!");
			//Fx.Assert(segmentsCount >= other.firstOptionalSegment - 1, "How can that be? The Trie is constructed that way!");
			//Fx.Assert(segmentsCount <= other.segments.Count, "How can that be? The Trie is constructed that way!");
			if (segmentsCount >= FirstOptionalSegment - 1 == false) 
				throw new Exception("How can that be? The Trie is constructed that way!");
			if (segmentsCount <= Segments.Count == false) 
				throw new Exception("How can that be? The Trie is constructed that way!");
			if (segmentsCount >= other.FirstOptionalSegment - 1 == false) 
				throw new Exception("How can that be? The Trie is constructed that way!");
			if (segmentsCount <= other.Segments.Count == false)
				throw new Exception("How can that be? The Trie is constructed that way!");
			for (var i = 0; i < segmentsCount; ++i) {
				if (!Segments[i].IsEquivalentTo(other.Segments[i],
					((i == segmentsCount - 1) && (_ignoreTrailingSlash || other._ignoreTrailingSlash)))) {
					return false;
				}
			}
			return true;
		}
		internal bool IsQueryEquivalent(UriTemplate other) {
			if (Queries.Count != other.Queries.Count) {
				return false;
			}
			foreach (var key in Queries.Keys) {
				var utqv = Queries[key];
				UriTemplateQueryValue otherUtqv;
				if (!other.Queries.TryGetValue(key, out otherUtqv)) {
					return false;
				}
				if (!utqv.IsEquivalentTo(otherUtqv)) {
					return false;
				}
			}
			return true;
		}

		internal static Uri RewriteUri(Uri uri, string host) {
			if (!string.IsNullOrEmpty(host)) {
				var originalHostHeader = uri.Host + ((!uri.IsDefaultPort) ? ":" + uri.Port.ToString(CultureInfo.InvariantCulture) : string.Empty);
				if (!String.Equals(originalHostHeader, host, StringComparison.OrdinalIgnoreCase)) {
					var sourceUri = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}://{1}", uri.Scheme, host));
					return (new UriBuilder(uri) { Host = sourceUri.Host, Port = sourceUri.Port }).Uri;
				}
			}
			return uri;
		}

		private Uri Bind(Uri baseAddress, BindInformation bindInfo, bool omitDefaults) {
			var result = new UriBuilder(baseAddress);
			var parameterIndex = 0;
			var lastPathParameter = ((_variables == null) ? -1 : _variables.PathSegmentVariableNames.Count - 1);
			int lastPathParameterToBind;
			if (lastPathParameter == -1) {
				lastPathParameterToBind = -1;
			} else if (omitDefaults) {
				lastPathParameterToBind = bindInfo.LastNonDefaultPathParameter;
			} else {
				lastPathParameterToBind = bindInfo.LastNonNullablePathParameter;
			}
			var parameters = bindInfo.NormalizedParameters;
			var extraQueryParameters = bindInfo.AdditionalParameters;
			// Binding the path :
			var pathString = new StringBuilder(result.Path);
			if (pathString[pathString.Length - 1] != '/') {
				pathString.Append('/');
			}
			if (lastPathParameterToBind < lastPathParameter) {
				// Binding all the parameters we need
				var segmentIndex = 0;
				while (parameterIndex <= lastPathParameterToBind) {
					if (segmentIndex < Segments.Count == false)
						throw new Exception(
							"Calculation of LastNonDefaultPathParameter,lastPathParameter or parameterIndex failed");
					Segments[segmentIndex++].Bind(parameters, ref parameterIndex, pathString);
				}

				if (parameterIndex == lastPathParameterToBind + 1 == false)
					throw new Exception("That is the exit criteria from the loop");
				// Maybe we have some literals yet to bind
				if (segmentIndex < Segments.Count == false)
					throw new Exception(
						"Calculation of LastNonDefaultPathParameter,lastPathParameter or parameterIndex failed");
				while (Segments[segmentIndex].Nature == UriTemplatePartType.Literal) {
					Segments[segmentIndex++].Bind(parameters, ref parameterIndex, pathString);
					if (parameterIndex == lastPathParameterToBind + 1 == false)
						throw new Exception("We have moved the parameter index in a literal binding");
					if (segmentIndex < Segments.Count == false)
						throw new Exception(
							"Calculation of LastNonDefaultPathParameter,lastPathParameter or parameterIndex failed");
				}
				// We're done; skip to the beggining of the query parameters
				parameterIndex = lastPathParameter + 1;
			} else if (Segments.Count > 0 || _wildcard != null) {
				for (var i = 0; i < Segments.Count; i++) {
					Segments[i].Bind(parameters, ref parameterIndex, pathString);
				}
				if (_wildcard != null) {
					_wildcard.Bind(parameters, ref parameterIndex, pathString);
				}
			}
			if (_ignoreTrailingSlash && (pathString[pathString.Length - 1] == '/')) {
				pathString.Remove(pathString.Length - 1, 1);
			}
			result.Path = pathString.ToString();
			// Binding the query :
			if ((Queries.Count != 0) || (extraQueryParameters != null)) {
				var query = new StringBuilder("");
				foreach (var key in Queries.Keys) {
					Queries[key].Bind(key, parameters, ref parameterIndex, query);
				}
				if (extraQueryParameters != null) {
					foreach (var key in extraQueryParameters.Keys) {
						if (Queries.ContainsKey(key.ToUpperInvariant())) {
							// This can only be if the key passed has the same name as some literal key
							throw new ArgumentException($"UTBothLiteralAndNameValueCollectionKey {key}"); 
						}
						var value = extraQueryParameters[key];
						var escapedValue = (string.IsNullOrEmpty(value) ? string.Empty : UrlUtility.UrlEncode(value, Encoding.UTF8));
						query.AppendFormat("&{0}={1}", UrlUtility.UrlEncode(key, Encoding.UTF8), escapedValue);
					}
				}
				if (query.Length != 0) {
					query.Remove(0, 1); // remove extra leading '&'
				}
				result.Query = query.ToString();
			}
			// Adding the fragment (if needed)
			if (_fragment != null) {
				result.Fragment = _fragment;
			}

			return result.Uri;
		}

		private void BindTerminalDefaults(int numMatchedSegments, NameValueCollection boundParameters) {
			// original asserts
			//Fx.Assert(!HasWildcard, "There are no terminal default when ends with wildcard");
			//Fx.Assert(numMatchedSegments < Segments.Count, "Otherwise - no defaults to bind");
			//Fx.Assert(_variables != null, "Otherwise - no default values to bind");
			//Fx.Assert(_variables.DefaultValues != null, "Otherwise - no default values to bind");
			if (!HasWildcard == false) throw new Exception("There are no terminal default when ends with wildcard");
			if (numMatchedSegments < Segments.Count == false) throw new Exception("Otherwise - no defaults to bind");
			Ensure.NotNull(_variables, "Otherwise - no default values to bind");
			Ensure.NotNull(_variables.DefaultValues, "Otherwise - no default values to bind");
			for (var i = numMatchedSegments; i < Segments.Count; i++) {
				switch (Segments[i].Nature) {
					case UriTemplatePartType.Variable: {
							var vps = Segments[i] as UriTemplateVariablePathSegment;
							Ensure.NotNull(vps, "How can that be? That its nature");
							_variables.LookupDefault(vps.VarName, boundParameters);
						}
						break;

					default:
						throw new Exception("We only support terminal defaults on Variable segments");
				}
			}
		}

		private bool IsCandidatePathMatch(int numSegmentsInBaseAddress, string[] candidateSegments,
			out int numMatchedSegments, out Collection<string> relativeSegments) {
			var numRelativeSegments = candidateSegments.Length - numSegmentsInBaseAddress;
			Ensure.Nonnegative(numRelativeSegments, "bad segments num");
			relativeSegments = new Collection<string>();
			var isStillMatch = true;
			var relativeSegmentsIndex = 0;
			while (isStillMatch && (relativeSegmentsIndex < numRelativeSegments)) {
				var segment = candidateSegments[relativeSegmentsIndex + numSegmentsInBaseAddress];
				// Mathcing to next regular segment in the template (if there is one); building the wire segment representation
				if (relativeSegmentsIndex < Segments.Count) {
					var ignoreSlash = (_ignoreTrailingSlash && (relativeSegmentsIndex == numRelativeSegments - 1));
					var lps = UriTemplateLiteralPathSegment.CreateFromWireData(segment);
					if (!Segments[relativeSegmentsIndex].IsMatch(lps, ignoreSlash)) {
						isStillMatch = false;
						break;
					}
					var relPathSeg = Uri.UnescapeDataString(segment);
					if (lps.EndsWithSlash) {
						if (relPathSeg.EndsWith("/", StringComparison.Ordinal) == false)
							throw new Exception("problem with relative path segment");
						relPathSeg = relPathSeg.Substring(0, relPathSeg.Length - 1); // trim slash
					}
					relativeSegments.Add(relPathSeg);
				}
				// Checking if the template has a wild card ('*') or a final star var segment ("{*<var name>}"
				else if (HasWildcard) {
					break;
				} else {
					isStillMatch = false;
					break;
				}
				relativeSegmentsIndex++;
			}
			if (isStillMatch) {
				numMatchedSegments = relativeSegmentsIndex;
				// building the wire representation to segments that were matched to a wild card
				if (relativeSegmentsIndex < numRelativeSegments) {
					while (relativeSegmentsIndex < numRelativeSegments) {
						var relPathSeg = Uri.UnescapeDataString(candidateSegments[relativeSegmentsIndex + numSegmentsInBaseAddress]);
						if (relPathSeg.EndsWith("/", StringComparison.Ordinal)) {
							relPathSeg = relPathSeg.Substring(0, relPathSeg.Length - 1); // trim slash
						}
						relativeSegments.Add(relPathSeg);
						relativeSegmentsIndex++;
					}
				}
				// Checking if we matched all required segments already
				else if (numMatchedSegments < FirstOptionalSegment) {
					isStillMatch = false;
				}
			} else {
				numMatchedSegments = 0;
			}

			return isStillMatch;
		}

		private bool IsPathFullyEquivalent(UriTemplate other) {
			// Refer to the note on template equivalency at IsEquivalentTo
			// This method checks if both templates has a fully equivalent path.
			if (HasWildcard != other.HasWildcard) {
				return false;
			}
			if (Segments.Count != other.Segments.Count) {
				return false;
			}
			for (var i = 0; i < Segments.Count; ++i) {
				if (!Segments[i].IsEquivalentTo(other.Segments[i],
					(i == Segments.Count - 1) && !HasWildcard && (_ignoreTrailingSlash || other._ignoreTrailingSlash))) {
					return false;
				}
			}
			return true;
		}

		private BindInformation PrepareBindInformation(IDictionary<string, string> parameters, bool omitDefaults) {
			if (parameters == null) {
				throw new ArgumentNullException(nameof(parameters));
			}

			IDictionary<string, string> extraParameters = new Dictionary<string, string>(UriTemplateHelpers.GetQueryKeyComparer());
			foreach (var kvp in parameters) {
				if (string.IsNullOrEmpty(kvp.Key)) {
					throw new ArgumentException($"UTBindByNameCalledWithEmptyKey");
				}

				extraParameters.Add(kvp);
			}
			BindInformation bindInfo;
			ProcessDefaultsAndCreateBindInfo(omitDefaults, extraParameters, out bindInfo);
			return bindInfo;
		}

		private BindInformation PrepareBindInformation(NameValueCollection parameters, bool omitDefaults) {
			if (parameters == null) {
				throw new ArgumentNullException(nameof(parameters));
			}

			IDictionary<string, string> extraParameters = new Dictionary<string, string>(UriTemplateHelpers.GetQueryKeyComparer());
			foreach (var key in parameters.AllKeys) {
				if (string.IsNullOrEmpty(key)) {
					throw new ArgumentException("UTBindByNameCalledWithEmptyKey");
				}

				extraParameters.Add(key, parameters[key]);
			}
			BindInformation bindInfo;
			ProcessDefaultsAndCreateBindInfo(omitDefaults, extraParameters, out bindInfo);
			return bindInfo;
		}

		private void ProcessDefaultsAndCreateBindInfo(bool omitDefaults, IDictionary<string, string> extraParameters,
			out BindInformation bindInfo) {
			Ensure.NotNull(extraParameters, "We are expected to create it at the calling PrepareBindInformation");
			if (_additionalDefaults != null) {
				if (omitDefaults) {
					foreach (var kvp in _additionalDefaults) {
						string extraParameter;
						if (extraParameters.TryGetValue(kvp.Key, out extraParameter)) {
							if (string.Compare(extraParameter, kvp.Value, StringComparison.Ordinal) == 0) {
								extraParameters.Remove(kvp.Key);
							}
						}
					}
				} else {
					foreach (var kvp in _additionalDefaults) {
						if (!extraParameters.ContainsKey(kvp.Key)) {
							extraParameters.Add(kvp.Key, kvp.Value);
						}
					}
				}
			}
			if (extraParameters.Count == 0) {
				extraParameters = null;
			}
			bindInfo = new BindInformation(extraParameters);
		}

		private string UnescapeDefaultValue(string escapedValue) {
			if (string.IsNullOrEmpty(escapedValue)) {
				return escapedValue;
			}
			if (_unescapedDefaults == null) {
				_unescapedDefaults = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
			}

			return _unescapedDefaults.GetOrAdd(escapedValue, Uri.UnescapeDataString);
		}

		private struct BindInformation {
			private IDictionary<string, string> _additionalParameters;
			private int _lastNonDefaultPathParameter;
			private int _lastNonNullablePathParameter;
			private string[] _normalizedParameters;

			public BindInformation(string[] normalizedParameters, int lastNonDefaultPathParameter,
				int lastNonNullablePathParameter, IDictionary<string, string> additionalParameters) {
				_normalizedParameters = normalizedParameters;
				_lastNonDefaultPathParameter = lastNonDefaultPathParameter;
				_lastNonNullablePathParameter = lastNonNullablePathParameter;
				_additionalParameters = additionalParameters;
			}
			public BindInformation(IDictionary<string, string> additionalParameters) {
				_normalizedParameters = null;
				_lastNonDefaultPathParameter = -1;
				_lastNonNullablePathParameter = -1;
				_additionalParameters = additionalParameters;
			}

			public IDictionary<string, string> AdditionalParameters {
				get {
					return _additionalParameters;
				}
			}
			public int LastNonDefaultPathParameter {
				get {
					return _lastNonDefaultPathParameter;
				}
			}
			public int LastNonNullablePathParameter {
				get {
					return _lastNonNullablePathParameter;
				}
			}
			public string[] NormalizedParameters {
				get {
					return _normalizedParameters;
				}
			}
		}

		private class UriTemplateDefaults : IDictionary<string, string> {
			private Dictionary<string, string> _defaults;
			private ReadOnlyCollection<string> _keys;
			private ReadOnlyCollection<string> _values;

			public UriTemplateDefaults(UriTemplate template) {
				_defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				if ((template._variables != null) && (template._variables.DefaultValues != null)) {
					foreach (var kvp in template._variables.DefaultValues) {
						_defaults.Add(kvp.Key, kvp.Value);
					}
				}
				if (template._additionalDefaults != null) {
					foreach (var kvp in template._additionalDefaults) {
						_defaults.Add(kvp.Key.ToUpperInvariant(), kvp.Value);
					}
				}
				_keys = new ReadOnlyCollection<string>(new List<string>(_defaults.Keys));
				_values = new ReadOnlyCollection<string>(new List<string>(_defaults.Values));
			}

			// ICollection<KeyValuePair<string, string>> Members
			public int Count {
				get {
					return _defaults.Count;
				}
			}
			public bool IsReadOnly {
				get {
					return true;
				}
			}

			// IDictionary<string, string> Members
			public ICollection<string> Keys {
				get {
					return _keys;
				}
			}
			public ICollection<string> Values {
				get {
					return _values;
				}
			}
			public string this[string key] {
				get {
					return _defaults[key];
				}
				set {
					throw new NotSupportedException("UTDefaultValuesAreImmutable");
				}
			}

			public void Add(string key, string value) {
				throw new NotSupportedException("UTDefaultValuesAreImmutable");
			}

			public void Add(KeyValuePair<string, string> item) {
				throw new NotSupportedException("UTDefaultValuesAreImmutable");
			}
			public void Clear() {
				throw new NotSupportedException("UTDefaultValuesAreImmutable");
			}
			public bool Contains(KeyValuePair<string, string> item) {
				return (_defaults as ICollection<KeyValuePair<string, string>>).Contains(item);
			}
			public bool ContainsKey(string key) {
				return _defaults.ContainsKey(key);
			}
			public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) {
				(_defaults as ICollection<KeyValuePair<string, string>>).CopyTo(array, arrayIndex);
			}

			// IEnumerable<KeyValuePair<string, string>> Members
			public IEnumerator<KeyValuePair<string, string>> GetEnumerator() {
				return _defaults.GetEnumerator();
			}
			public bool Remove(string key) {
				throw new NotSupportedException("UTDefaultValuesAreImmutable");
			}
			public bool Remove(KeyValuePair<string, string> item) {
				throw new NotSupportedException("UTDefaultValuesAreImmutable");
			}

			// IEnumerable Members
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
				return _defaults.GetEnumerator();
			}
			public bool TryGetValue(string key, out string value) {
				return _defaults.TryGetValue(key, out value);
			}
		}

		private class VariablesCollection {
			private readonly UriTemplate _owner;
			private static ReadOnlyCollection<string> _emptyStringCollection = null;
			private Dictionary<string, string> _defaultValues; // key is the variable name (in uppercase; as appear in the variable names lists)
			private int _firstNullablePathVariable;
			private List<string> _pathSegmentVariableNames; // ToUpperInvariant, in order they occur in the original template string
			private ReadOnlyCollection<string> _pathSegmentVariableNamesSnapshot = null;
			private List<UriTemplatePartType> _pathSegmentVariableNature;
			private List<string> _queryValueVariableNames; // ToUpperInvariant, in order they occur in the original template string
			private ReadOnlyCollection<string> _queryValueVariableNamesSnapshot = null;

			public VariablesCollection(UriTemplate owner) {
				_owner = owner;
				_pathSegmentVariableNames = new List<string>();
				_pathSegmentVariableNature = new List<UriTemplatePartType>();
				_queryValueVariableNames = new List<string>();
				_firstNullablePathVariable = -1;
			}

			public static ReadOnlyCollection<string> EmptyCollection {
				get {
					if (_emptyStringCollection == null) {
						_emptyStringCollection = new ReadOnlyCollection<string>(new List<string>());
					}
					return _emptyStringCollection;
				}
			}

			public Dictionary<string, string> DefaultValues {
				get {
					return _defaultValues;
				}
			}
			public ReadOnlyCollection<string> PathSegmentVariableNames {
				get {
					if (_pathSegmentVariableNamesSnapshot == null) {
						Interlocked.CompareExchange<ReadOnlyCollection<string>>(ref _pathSegmentVariableNamesSnapshot, new ReadOnlyCollection<string>(
							_pathSegmentVariableNames), null);
					}
					return _pathSegmentVariableNamesSnapshot;
				}
			}
			public ReadOnlyCollection<string> QueryValueVariableNames {
				get {
					if (_queryValueVariableNamesSnapshot == null) {
						Interlocked.CompareExchange<ReadOnlyCollection<string>>(ref _queryValueVariableNamesSnapshot, new ReadOnlyCollection<string>(
							_queryValueVariableNames), null);
					}
					return _queryValueVariableNamesSnapshot;
				}
			}

			public void AddDefaultValue(string varName, string value) {
				var varIndex = _pathSegmentVariableNames.IndexOf(varName);
				Ensure.Nonnegative(varIndex, "Adding default value is restricted to path variables");
				if ((_owner._wildcard != null) && _owner._wildcard.HasVariable &&
					(varIndex == _pathSegmentVariableNames.Count - 1)) {
					throw new InvalidOperationException(
						$"UTStarVariableWithDefaultsFromAdditionalDefaults OriginalTemplate:{_owner.OriginalTemplate},varName:{varName}");
				}
				if (_pathSegmentVariableNature[varIndex] != UriTemplatePartType.Variable) {
					throw new InvalidOperationException(
						$"UTDefaultValueToCompoundSegmentVarFromAdditionalDefaults OriginalTemplate:{_owner.OriginalTemplate},varName:{varName}");
				}
				if (string.IsNullOrEmpty(value) ||
					(string.Compare(value, NullableDefault, StringComparison.OrdinalIgnoreCase) == 0)) {
					value = null;
				}
				if (_defaultValues == null) {
					_defaultValues = new Dictionary<string, string>();
				}
				_defaultValues.Add(varName, value);
			}

			public string AddPathVariable(UriTemplatePartType sourceNature, string varDeclaration, out bool hasDefaultValue) {
				if (sourceNature != UriTemplatePartType.Literal == false)
					throw new Exception("Literal path segments can't be the source for path variables");
				string varName;
				string defaultValue;
				ParseVariableDeclaration(varDeclaration, out varName, out defaultValue);
				hasDefaultValue = (defaultValue != null);
				if (varName.IndexOf(WildcardPath, StringComparison.Ordinal) != -1) {
					throw new FormatException(
						$"UTInvalidWildcardInVariableOrLiteral owner.OriginalTemplate: {_owner.OriginalTemplate} WildcardPath: {WildcardPath}");
				}
				var uppercaseVarName = varName.ToUpperInvariant();
				if (_pathSegmentVariableNames.Contains(uppercaseVarName) ||
					_queryValueVariableNames.Contains(uppercaseVarName)) {
					throw new InvalidOperationException(
						$"UTVarNamesMustBeUnique OriginalTemplate:{_owner.OriginalTemplate},varName:{varName}");
				}
				_pathSegmentVariableNames.Add(uppercaseVarName);
				_pathSegmentVariableNature.Add(sourceNature);
				if (hasDefaultValue) {
					if (defaultValue == string.Empty) {
						throw new InvalidOperationException(
							$"UTInvalidDefaultPathValue OriginalTemplate:{_owner.OriginalTemplate},varDeclaration:{varDeclaration},varName:{varName}");
					}
					if (string.Compare(defaultValue, NullableDefault, StringComparison.OrdinalIgnoreCase) == 0) {
						defaultValue = null;
					}
					if (_defaultValues == null) {
						_defaultValues = new Dictionary<string, string>();
					}
					_defaultValues.Add(uppercaseVarName, defaultValue);
				}
				return uppercaseVarName;
			}
			public string AddQueryVariable(string varDeclaration) {
				string varName;
				string defaultValue;
				ParseVariableDeclaration(varDeclaration, out varName, out defaultValue);
				if (varName.IndexOf(WildcardPath, StringComparison.Ordinal) != -1) {
					throw new FormatException(
						$"UTInvalidWildcardInVariableOrLiteral {_owner.OriginalTemplate} {WildcardPath}");
				}
				if (defaultValue != null) {
					throw new FormatException(
						$"UTDefaultValueToQueryVar {_owner.OriginalTemplate} {varDeclaration} {varName}");
				}
				var uppercaseVarName = varName.ToUpperInvariant();
				if (_pathSegmentVariableNames.Contains(uppercaseVarName) ||
					_queryValueVariableNames.Contains(uppercaseVarName)) {
					throw new InvalidOperationException($"UTVarNamesMustBeUnique OriginalTemplate:{_owner.OriginalTemplate},varName:{varName}"); //, , )));
				}
				_queryValueVariableNames.Add(uppercaseVarName);
				return uppercaseVarName;
			}

			public void LookupDefault(string varName, NameValueCollection boundParameters) {
				if (_defaultValues.ContainsKey(varName) == false)
					throw new Exception("Otherwise, we don't have a value to bind");
				boundParameters.Add(varName, _owner.UnescapeDefaultValue(_defaultValues[varName]));
			}

			public BindInformation PrepareBindInformation(IDictionary<string, string> parameters, bool omitDefaults) {
				if (parameters == null) {
					throw new ArgumentNullException(nameof(parameters));
				}

				var normalizedParameters = PrepareNormalizedParameters();
				IDictionary<string, string> extraParameters = null;
				foreach (var key in parameters.Keys) {
					ProcessBindParameter(key, parameters[key], normalizedParameters, ref extraParameters);
				}
				BindInformation bindInfo;
				ProcessDefaultsAndCreateBindInfo(omitDefaults, normalizedParameters, extraParameters, out bindInfo);
				return bindInfo;
			}
			public BindInformation PrepareBindInformation(NameValueCollection parameters, bool omitDefaults) {
				if (parameters == null) {
					throw new ArgumentNullException(nameof(parameters));
				}

				var normalizedParameters = PrepareNormalizedParameters();
				IDictionary<string, string> extraParameters = null;
				foreach (var key in parameters.AllKeys) {
					ProcessBindParameter(key, parameters[key], normalizedParameters, ref extraParameters);
				}
				BindInformation bindInfo;
				ProcessDefaultsAndCreateBindInfo(omitDefaults, normalizedParameters, extraParameters, out bindInfo);
				return bindInfo;
			}
			public BindInformation PrepareBindInformation(params string[] parameters) {
				if (parameters == null) {
					throw new ArgumentNullException(nameof(parameters));
				}
				if ((parameters.Length < _pathSegmentVariableNames.Count) ||
					(parameters.Length > _pathSegmentVariableNames.Count + _queryValueVariableNames.Count)) {
					throw new FormatException($"UTBindByPositionWrongCount {_owner.OriginalTemplate} {_pathSegmentVariableNames.Count} {_queryValueVariableNames.Count} {parameters.Length}"); 
				}

				string[] normalizedParameters;
				if (parameters.Length == _pathSegmentVariableNames.Count + _queryValueVariableNames.Count) {
					normalizedParameters = parameters;
				} else {
					normalizedParameters = new string[_pathSegmentVariableNames.Count + _queryValueVariableNames.Count];
					parameters.CopyTo(normalizedParameters, 0);
					for (var i = parameters.Length; i < normalizedParameters.Length; i++) {
						normalizedParameters[i] = null;
					}
				}
				int lastNonDefaultPathParameter;
				int lastNonNullablePathParameter;
				LoadDefaultsAndValidate(normalizedParameters, out lastNonDefaultPathParameter,
					out lastNonNullablePathParameter);
				return new BindInformation(normalizedParameters, lastNonDefaultPathParameter,
					lastNonNullablePathParameter, _owner._additionalDefaults);
			}
			public void ValidateDefaults(out int firstOptionalSegment) {
				Ensure.NotNull(_defaultValues, "We are checking this condition from the c'tor");
				Ensure.Positive(_pathSegmentVariableNames.Count, "Otherwise, how can we have default values");
				// Finding the first valid nullable defaults
				for (var i = _pathSegmentVariableNames.Count - 1; (i >= 0) && (_firstNullablePathVariable == -1); i--) {
					var varName = _pathSegmentVariableNames[i];
					string defaultValue;
					if (!_defaultValues.TryGetValue(varName, out defaultValue)) {
						_firstNullablePathVariable = i + 1;
					} else if (defaultValue != null) {
						_firstNullablePathVariable = i + 1;
					}
				}
				if (_firstNullablePathVariable == -1) {
					_firstNullablePathVariable = 0;
				}
				// Making sure that there are no nullables to the left of the first valid nullable
				if (_firstNullablePathVariable > 1) {
					for (var i = _firstNullablePathVariable - 2; i >= 0; i--) {
						var varName = _pathSegmentVariableNames[i];
						string defaultValue;
						if (_defaultValues.TryGetValue(varName, out defaultValue)) {
							if (defaultValue == null) {
								throw new InvalidOperationException(
									$"UTNullableDefaultMustBeFollowedWithNullables OriginalTemplate:{_owner.OriginalTemplate},varName{varName},pathSegmentVariableName:{_pathSegmentVariableNames[i + 1]}");
							}
						}
					}
				}
				// Making sure that there are no Literals\WildCards to the right
				// Based on the fact that only Variable Path Segments support default values,
				//  if firstNullablePathVariable=N and pathSegmentVariableNames.Count=M then
				//  the nature of the last M-N path segments should be StringNature.Variable; otherwise,
				//  there was a literal segment in between. Also, there shouldn't be a wildcard.
				if (_firstNullablePathVariable < _pathSegmentVariableNames.Count) {
					if (_owner.HasWildcard) {
						throw new InvalidOperationException(
							$"UTNullableDefaultMustNotBeFollowedWithWildcard OriginalTemplate:{_owner.OriginalTemplate},pathSegmentVariableName:{_pathSegmentVariableNames[_firstNullablePathVariable]}");
					}
					for (var i = _pathSegmentVariableNames.Count - 1; i >= _firstNullablePathVariable; i--) {
						var segmentIndex = _owner.Segments.Count - (_pathSegmentVariableNames.Count - i);
						if (_owner.Segments[segmentIndex].Nature != UriTemplatePartType.Variable) {
							throw new InvalidOperationException(
								$"UTNullableDefaultMustNotBeFollowedWithLiteral OriginalTemplate:{_owner.OriginalTemplate},pathSegmentVariableName:{_pathSegmentVariableNames[_firstNullablePathVariable]},OriginalSegment:{_owner.Segments[segmentIndex].OriginalSegment}");
						}
					}
				}
				// Now that we have the firstNullablePathVariable set, lets calculate the firstOptionalSegment.
				//  We already knows that the last M-N path segments (when M=pathSegmentVariableNames.Count and
				//  N=firstNullablePathVariable) are optional (see the previos comment). We will start there and
				//  move to the left, stopping at the first segment, which is not a variable or is a variable
				//  and doesn't have a default value.
				var numNullablePathVariables = (_pathSegmentVariableNames.Count - _firstNullablePathVariable);
				firstOptionalSegment = _owner.Segments.Count - numNullablePathVariables;
				if (!_owner.HasWildcard) {
					while (firstOptionalSegment > 0) {
						var ps = _owner.Segments[firstOptionalSegment - 1];
						if (ps.Nature != UriTemplatePartType.Variable) {
							break;
						}
						var vps = (ps as UriTemplateVariablePathSegment);
						Ensure.NotNull(vps, "Should be; that's his nature");
						if (!_defaultValues.ContainsKey(vps.VarName)) {
							break;
						}
						firstOptionalSegment--;
					}
				}
			}

			private void AddAdditionalDefaults(ref IDictionary<string, string> extraParameters) {
				if (extraParameters == null) {
					extraParameters = _owner._additionalDefaults;
				} else {
					foreach (var kvp in _owner._additionalDefaults) {
						if (!extraParameters.ContainsKey(kvp.Key)) {
							extraParameters.Add(kvp.Key, kvp.Value);
						}
					}
				}
			}

			private void LoadDefaultsAndValidate(string[] normalizedParameters, out int lastNonDefaultPathParameter,
				out int lastNonNullablePathParameter) {
				// First step - loading defaults
				for (var i = 0; i < _pathSegmentVariableNames.Count; i++) {
					if (string.IsNullOrEmpty(normalizedParameters[i]) && (_defaultValues != null)) {
						_defaultValues.TryGetValue(_pathSegmentVariableNames[i], out normalizedParameters[i]);
					}
				}
				// Second step - calculating bind constrains
				lastNonDefaultPathParameter = _pathSegmentVariableNames.Count - 1;
				if ((_defaultValues != null) &&
					(_owner.Segments[_owner.Segments.Count - 1].Nature != UriTemplatePartType.Literal)) {
					var foundNonDefaultPathParameter = false;
					while (!foundNonDefaultPathParameter && (lastNonDefaultPathParameter >= 0)) {
						string defaultValue;
						if (_defaultValues.TryGetValue(_pathSegmentVariableNames[lastNonDefaultPathParameter],
							out defaultValue)) {
							if (string.Compare(normalizedParameters[lastNonDefaultPathParameter],
								defaultValue, StringComparison.Ordinal) != 0) {
								foundNonDefaultPathParameter = true;
							} else {
								lastNonDefaultPathParameter--;
							}
						} else {
							foundNonDefaultPathParameter = true;
						}
					}
				}
				if (_firstNullablePathVariable > lastNonDefaultPathParameter) {
					lastNonNullablePathParameter = _firstNullablePathVariable - 1;
				} else {
					lastNonNullablePathParameter = lastNonDefaultPathParameter;
				}
				// Third step - validate
				for (var i = 0; i <= lastNonNullablePathParameter; i++) {
					// Skip validation for terminating star variable segment :
					if (_owner.HasWildcard && _owner._wildcard.HasVariable &&
						(i == _pathSegmentVariableNames.Count - 1)) {
						continue;
					}
					// Validate
					if (string.IsNullOrEmpty(normalizedParameters[i])) {
						throw new ArgumentException($"BindUriTemplateToNullOrEmptyPathParam {_pathSegmentVariableNames[i]}"); 
					}
				}
			}

			private void ParseVariableDeclaration(string varDeclaration, out string varName, out string defaultValue) {
				if ((varDeclaration.IndexOf('{') != -1) || (varDeclaration.IndexOf('}') != -1)) {
					throw new FormatException($"UTInvalidVarDeclaration {_owner.OriginalTemplate} {varDeclaration}");
				}
				var equalSignIndex = varDeclaration.IndexOf('=');
				switch (equalSignIndex) {
					case -1:
						varName = varDeclaration;
						defaultValue = null;
						break;

					case 0:
						throw new FormatException($"UTInvalidVarDeclaration {_owner.OriginalTemplate} {varDeclaration}");

					default:
						varName = varDeclaration.Substring(0, equalSignIndex);
						defaultValue = varDeclaration.Substring(equalSignIndex + 1);
						if (defaultValue.IndexOf('=') != -1) {
							throw new FormatException($"UTInvalidVarDeclaration {_owner.OriginalTemplate} {varDeclaration}");
						}
						break;
				}
			}

			private string[] PrepareNormalizedParameters() {
				var normalizedParameters = new string[_pathSegmentVariableNames.Count + _queryValueVariableNames.Count];
				for (var i = 0; i < normalizedParameters.Length; i++) {
					normalizedParameters[i] = null;
				}
				return normalizedParameters;
			}

			private void ProcessBindParameter(string name, string value, string[] normalizedParameters,
				ref IDictionary<string, string> extraParameters) {
				if (string.IsNullOrEmpty(name)) {
					throw new ArgumentException("UTBindByNameCalledWithEmptyKey"); 
				}

				var uppercaseVarName = name.ToUpperInvariant();
				var pathVarIndex = _pathSegmentVariableNames.IndexOf(uppercaseVarName);
				if (pathVarIndex != -1) {
					normalizedParameters[pathVarIndex] = (string.IsNullOrEmpty(value) ? string.Empty : value);
					return;
				}
				var queryVarIndex = _queryValueVariableNames.IndexOf(uppercaseVarName);
				if (queryVarIndex != -1) {
					normalizedParameters[_pathSegmentVariableNames.Count + queryVarIndex] = (string.IsNullOrEmpty(value) ? string.Empty : value);
					return;
				}
				if (extraParameters == null) {
					extraParameters = new Dictionary<string, string>(UriTemplateHelpers.GetQueryKeyComparer());
				}
				extraParameters.Add(name, value);
			}

			private void ProcessDefaultsAndCreateBindInfo(bool omitDefaults, string[] normalizedParameters,
				IDictionary<string, string> extraParameters, out BindInformation bindInfo) {
				int lastNonDefaultPathParameter;
				int lastNonNullablePathParameter;
				LoadDefaultsAndValidate(normalizedParameters, out lastNonDefaultPathParameter,
					out lastNonNullablePathParameter);
				if (_owner._additionalDefaults != null) {
					if (omitDefaults) {
						RemoveAdditionalDefaults(ref extraParameters);
					} else {
						AddAdditionalDefaults(ref extraParameters);
					}
				}
				bindInfo = new BindInformation(normalizedParameters, lastNonDefaultPathParameter,
					lastNonNullablePathParameter, extraParameters);
			}

			private void RemoveAdditionalDefaults(ref IDictionary<string, string> extraParameters) {
				if (extraParameters == null) {
					return;
				}

				foreach (var kvp in _owner._additionalDefaults) {
					string extraParameter;
					if (extraParameters.TryGetValue(kvp.Key, out extraParameter)) {
						if (string.Compare(extraParameter, kvp.Value, StringComparison.Ordinal) == 0) {
							extraParameters.Remove(kvp.Key);
						}
					}
				}
				if (extraParameters.Count == 0) {
					extraParameters = null;
				}
			}
		}

		private class WildcardInfo {
			private readonly UriTemplate _owner;
			private readonly string _varName;

			public WildcardInfo(UriTemplate owner) {
				_varName = null;
				_owner = owner;
			}
			public WildcardInfo(UriTemplate owner, string segment) {
				if (!segment.EndsWith("/", StringComparison.Ordinal) == false)
					throw new Exception("We are expecting to check this earlier");

				bool hasDefault;
				_varName = owner.AddPathVariable(UriTemplatePartType.Variable,
					segment.Substring(1 + WildcardPath.Length, segment.Length - 2 - WildcardPath.Length),
					out hasDefault);
				// Since this is a terminating star segment there shouldn't be a default
				if (hasDefault) {
					throw new InvalidOperationException(
						$"UTStarVariableWithDefaults OriginalTemplate:{owner.OriginalTemplate},segment:{segment},varName:{_varName}");
				}
				_owner = owner;
			}

			internal bool HasVariable {
				get {
					return (!string.IsNullOrEmpty(_varName));
				}
			}

			public void Bind(string[] values, ref int valueIndex, StringBuilder path) {
				if (HasVariable) {
					if (valueIndex < values.Length == false) throw new Exception("Not enough values to bind");
					if (string.IsNullOrEmpty(values[valueIndex])) {
						valueIndex++;
					} else {
						path.Append(values[valueIndex++]);
					}
				}
			}
			public void Lookup(int numMatchedSegments, Collection<string> relativePathSegments,
				NameValueCollection boundParameters) {
				Ensure.Equal(numMatchedSegments, _owner.Segments.Count, "We should have matched the other segments");
				if (HasVariable) {
					var remainingPath = new StringBuilder();
					for (var i = numMatchedSegments; i < relativePathSegments.Count; i++) {
						if (i < relativePathSegments.Count - 1) {
							remainingPath.AppendFormat("{0}/", relativePathSegments[i]);
						} else {
							remainingPath.Append(relativePathSegments[i]);
						}
					}
					boundParameters.Add(_varName, remainingPath.ToString());
				}
			}
		}
	}
}
