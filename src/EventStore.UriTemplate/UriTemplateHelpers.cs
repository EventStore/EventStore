using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;

namespace EventStore.UriTemplate {
	internal static class UriTemplateHelpers {
		private static UriTemplateQueryComparer _queryComparer = new UriTemplateQueryComparer();
		private static UriTemplateQueryKeyComparer _queryKeyComperar = new UriTemplateQueryKeyComparer();

		[Conditional("DEBUG")]
		public static void AssertCanonical(string s) {
			//Fx.Assert(s == s.ToUpperInvariant(), "non-canonicalized");
			Ensure.Equal(s, s.ToUpperInvariant(), "non-canonicalized");
		}
		public static bool CanMatchQueryInterestingly(UriTemplate ut, NameValueCollection query, bool mustBeEspeciallyInteresting) {
			if (ut.Queries.Count == 0) {
				return false; // trivial, not interesting
			}
			var queryKeys = query.AllKeys;
			foreach (var kvp in ut.Queries) {
				var queryKeyName = kvp.Key;
				if (kvp.Value.Nature == UriTemplatePartType.Literal) {
					var queryKeysContainsQueryVarName = false;
					for (var i = 0; i < queryKeys.Length; ++i) {
						if (StringComparer.OrdinalIgnoreCase.Equals(queryKeys[i], queryKeyName)) {
							queryKeysContainsQueryVarName = true;
							break;
						}
					}
					if (!queryKeysContainsQueryVarName) {
						return false;
					}
					if (kvp.Value == UriTemplateQueryValue.Empty) {
						if (!string.IsNullOrEmpty(query[queryKeyName])) {
							return false;
						}
					} else {
						if (((UriTemplateLiteralQueryValue)(kvp.Value)).AsRawUnescapedString() != query[queryKeyName]) {
							return false;
						}
					}
				} else {
					if (mustBeEspeciallyInteresting && Array.IndexOf(queryKeys, queryKeyName) == -1) {
						return false;
					}
				}
			}
			return true;
		}

		public static bool CanMatchQueryTrivially(UriTemplate ut) {
			return (ut.Queries.Count == 0);
		}

		public static void DisambiguateSamePath(UriTemplate[] array, int a, int b, bool allowDuplicateEquivalentUriTemplates) {
			// [a,b) all have same path
			// ensure queries make them unambiguous
			//Fx.Assert(b > a, "array bug");
			if (b > a == false) 
				throw new Exception("array bug");
			// sort empty queries to front
			Array.Sort<UriTemplate>(array, a, b - a, _queryComparer);
			if (b - a == 1) {
				return; // if only one, cannot be ambiguous
			}
			if (!allowDuplicateEquivalentUriTemplates) {
				// ensure at most one empty query and ignore it
				if (array[a].Queries.Count == 0) {
					a++;
				}
				if (array[a].Queries.Count == 0) {
					throw new InvalidOperationException($"UTTDuplicate {array[a]} {array[a - 1]}");
				}
				if (b - a == 1) {
					return; // if only one, cannot be ambiguous
				}
			} else {
				while (a < b && array[a].Queries.Count == 0)  // all equivalent
				{
					a++;
				}
				if (b - a <= 1) {
					return;
				}
			}
			if (b > a == false)
				throw new Exception("array bug");
			// now consider non-empty queries
			// more than one, so enforce that
			// forall
			//   exist set of querystringvars S where
			//     every op has literal value foreach var in S, and
			//     those literal tuples are different
			EnsureQueriesAreDistinct(array, a, b, allowDuplicateEquivalentUriTemplates);
		}

		public static IEqualityComparer<string> GetQueryKeyComparer() {
			return _queryKeyComperar;
		}

		public static string GetUriPath(Uri uri) {
			return uri.GetComponents(UriComponents.Path | UriComponents.KeepDelimiter, UriFormat.Unescaped);
		}
		public static bool HasQueryLiteralRequirements(UriTemplate ut) {
			foreach (var utqv in ut.Queries.Values) {
				if (utqv.Nature == UriTemplatePartType.Literal) {
					return true;
				}
			}
			return false;
		}

		public static UriTemplatePartType IdentifyPartType(string part) {
			// Identifying the nature of a string - Literal|Compound|Variable
			// Algorithem is based on the following steps:
			// - Finding the position of the first open curlly brace ('{') and close curlly brace ('}') 
			//    in the string
			// - If we don't find any this is a Literal
			// - otherwise, we validate that position of the close brace is at least two characters from 
			//    the position of the open brace
			// - Then we identify if we are dealing with a compound string or a single variable string
			//    + var name is not at the string start --> Compound
			//    + var name is shorter then the entire string (End < Length-2 or End==Length-2 
			//       and string ends with '/') --> Compound
			//    + otherwise --> Variable
			var varStartIndex = part.IndexOf("{", StringComparison.Ordinal);
			var varEndIndex = part.IndexOf("}", StringComparison.Ordinal);
			if (varStartIndex == -1) {
				if (varEndIndex != -1) {
					throw new FormatException($"UTInvalidFormatSegmentOrQueryPart {part}");
				}
				return UriTemplatePartType.Literal;
			} else {
				if (varEndIndex < varStartIndex + 2) {
					throw new FormatException($"UTInvalidFormatSegmentOrQueryPart {part}");
				}
				if (varStartIndex > 0) {
					return UriTemplatePartType.Compound;
				} else if ((varEndIndex < part.Length - 2) ||
					  ((varEndIndex == part.Length - 2) && !part.EndsWith("/", StringComparison.Ordinal))) {
					return UriTemplatePartType.Compound;
				} else {
					return UriTemplatePartType.Variable;
				}
			}
		}
		public static bool IsWildcardPath(string path) {
			if (path.IndexOf('/') != -1) {
				return false;
			}
			UriTemplatePartType partType;
			return IsWildcardSegment(path, out partType);
		}

		public static bool IsWildcardSegment(string segment, out UriTemplatePartType type) {
			type = IdentifyPartType(segment);
			switch (type) {
				case UriTemplatePartType.Literal:
					return (string.Compare(segment, UriTemplate.WildcardPath, StringComparison.Ordinal) == 0);

				case UriTemplatePartType.Compound:
					return false;

				case UriTemplatePartType.Variable:
					return ((segment.IndexOf(UriTemplate.WildcardPath, StringComparison.Ordinal) == 1) &&
						!segment.EndsWith("/", StringComparison.Ordinal) &&
						(segment.Length > UriTemplate.WildcardPath.Length + 2));

				default:
					throw new Exception("Bad part type identification !");
			}
		}
		public static NameValueCollection ParseQueryString(string query) {
			// We are adjusting the parsing of UrlUtility.ParseQueryString, which identify
			//  ?wsdl as a null key with wsdl as a value
			var result = UrlUtility.ParseQueryString(query);
			var nullKeyValuesString = result[(string)null];
			if (!string.IsNullOrEmpty(nullKeyValuesString)) {
				result.Remove(null);
				var nullKeyValues = nullKeyValuesString.Split(',');
				for (var i = 0; i < nullKeyValues.Length; i++) {
					result.Add(nullKeyValues[i], null);
				}
			}
			return result;
		}

		private static bool AllTemplatesAreEquivalent(IList<UriTemplate> array, int a, int b) {
			for (var i = a; i < b - 1; ++i) {
				if (!array[i].IsEquivalentTo(array[i + 1])) {
					return false;
				}
			}
			return true;
		}

		private static void EnsureQueriesAreDistinct(UriTemplate[] array, int a, int b, bool allowDuplicateEquivalentUriTemplates) {
			var queryVarNamesWithLiteralVals = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
			for (var i = a; i < b; ++i) {
				foreach (var kvp in array[i].Queries) {
					if (kvp.Value.Nature == UriTemplatePartType.Literal) {
						if (!queryVarNamesWithLiteralVals.ContainsKey(kvp.Key)) {
							queryVarNamesWithLiteralVals.Add(kvp.Key, 0);
						}
					}
				}
			}
			// now we have set of possibilities:
			// further refine to only those for whom all templates have literals
			var queryVarNamesAllLiterals = new Dictionary<string, byte>(queryVarNamesWithLiteralVals);
			for (var i = a; i < b; ++i) {
				foreach (var s in queryVarNamesWithLiteralVals.Keys) {
					if (!array[i].Queries.ContainsKey(s) || (array[i].Queries[s].Nature != UriTemplatePartType.Literal)) {
						queryVarNamesAllLiterals.Remove(s);
					}
				}
			}
			queryVarNamesWithLiteralVals = null; // ensure we don't reference this variable any more
												 // now we have the set of names that every operation has as a literal
			if (queryVarNamesAllLiterals.Count == 0) {
				if (allowDuplicateEquivalentUriTemplates && AllTemplatesAreEquivalent(array, a, b)) {
					// we're ok, do nothing
				} else {
					throw new InvalidOperationException($"UTTOtherAmbiguousQueries {array[a]}");
				}
			}
			// now just ensure that each template has a unique tuple of values for the names
			var upsLits = new string[b - a][];
			for (var i = 0; i < b - a; ++i) {
				upsLits[i] = GetQueryLiterals(array[i + a], queryVarNamesAllLiterals);
			}
			for (var i = 0; i < b - a; ++i) {
				for (var j = i + 1; j < b - a; ++j) {
					if (Same(upsLits[i], upsLits[j])) {
						if (!array[i + a].IsEquivalentTo(array[j + a])) {
							throw new InvalidOperationException($"UTTAmbiguousQueries {array[a + i]} {array[j + a]}");
						}
						if(array[i + a].IsEquivalentTo(array[j + a]) == false)
							throw new Exception("bad equiv logic");
						if (!allowDuplicateEquivalentUriTemplates) {
							throw new InvalidOperationException($"UTTDuplicate {array[a + i]} {array[j + a]}");
						}
					}
				}
			}
			// we're good.  whew!
		}

		private static string[] GetQueryLiterals(UriTemplate up, Dictionary<string, byte> queryVarNames) {
			var queryLitVals = new string[queryVarNames.Count];
			var i = 0;
			foreach (var queryVarName in queryVarNames.Keys) {
				if(up.Queries.ContainsKey(queryVarName) == false)
					throw new Exception("query doesn't have name");
				var utqv = up.Queries[queryVarName];
				if(utqv.Nature == UriTemplatePartType.Literal == false)
					throw new Exception("query for name is not literal");
				if (utqv == UriTemplateQueryValue.Empty) {
					queryLitVals[i] = null;
				} else {
					queryLitVals[i] = ((UriTemplateLiteralQueryValue)(utqv)).AsRawUnescapedString();
				}
				++i;
			}
			return queryLitVals;
		}

		private static bool Same(string[] a, string[] b) {
			if(a.Length == b.Length == false)
				throw new Exception("arrays not same length");
			for (var i = 0; i < a.Length; ++i) {
				if (a[i] != b[i]) {
					return false;
				}
			}
			return true;
		}

		private class UriTemplateQueryComparer : IComparer<UriTemplate> {
			public int Compare(UriTemplate x, UriTemplate y) {
				// sort the empty queries to the front
				return Comparer<int>.Default.Compare(x.Queries.Count, y.Queries.Count);
			}
		}

		private class UriTemplateQueryKeyComparer : IEqualityComparer<string> {
			public bool Equals(string x, string y) {
				return (string.Compare(x, y, StringComparison.OrdinalIgnoreCase) == 0);
			}
			public int GetHashCode(string obj) {
				if (obj == null) {
					throw new ArgumentNullException(nameof(obj));
				}
				return obj.ToUpperInvariant().GetHashCode();
			}
		}
	}
}
