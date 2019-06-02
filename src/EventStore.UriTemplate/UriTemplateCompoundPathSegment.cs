using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace EventStore.UriTemplate {
	// Thin wrapper around formatted string; use type system to help ensure we
	// are doing canonicalization right/consistently - the literal sections are held in an
	// un-escaped format
	// We are assuming that the string will be always built as Lit{Var}Lit[{Var}Lit[{Var}Lit[...]]],
	// when the first and last literals may be empty
	internal class UriTemplateCompoundPathSegment : UriTemplatePathSegment, IComparable<UriTemplateCompoundPathSegment> {
		private readonly string _firstLiteral;
		private readonly List<VarAndLitPair> _varLitPairs;

		private CompoundSegmentClass _csClass;

		private UriTemplateCompoundPathSegment(string originalSegment, bool endsWithSlash, string firstLiteral)
			: base(originalSegment, UriTemplatePartType.Compound, endsWithSlash) {
			_firstLiteral = firstLiteral;
			_varLitPairs = new List<VarAndLitPair>();
		}
		public static new UriTemplateCompoundPathSegment CreateFromUriTemplate(string segment, UriTemplate template) {
			var origSegment = segment;
			var endsWithSlash = segment.EndsWith("/", StringComparison.Ordinal);
			if (endsWithSlash) {
				segment = segment.Remove(segment.Length - 1);
			}

			var nextVarStart = segment.IndexOf("{", StringComparison.Ordinal);
			Ensure.Nonnegative(nextVarStart,
				"The method is only called after identifying a '{' character in the segment");
			var firstLiteral = ((nextVarStart > 0) ? segment.Substring(0, nextVarStart) : string.Empty);
			if (firstLiteral.IndexOf(UriTemplate.WildcardPath, StringComparison.Ordinal) != -1) {
				throw new FormatException($"UTInvalidWildcardInVariableOrLiteral {template.OriginalTemplate} {UriTemplate.WildcardPath}");
			}
			var result = new UriTemplateCompoundPathSegment(origSegment, endsWithSlash,
				((firstLiteral != string.Empty) ? Uri.UnescapeDataString(firstLiteral) : string.Empty));
			do {
				var nextVarEnd = segment.IndexOf("}", nextVarStart + 1, StringComparison.Ordinal);
				if (nextVarEnd < nextVarStart + 2) {
					throw new FormatException($"UTInvalidFormatSegmentOrQueryPart {segment}");
				}
				bool hasDefault;
				var varName = template.AddPathVariable(UriTemplatePartType.Compound,
					segment.Substring(nextVarStart + 1, nextVarEnd - nextVarStart - 1), out hasDefault);
				if (hasDefault) {
					throw new InvalidOperationException(
						$"UTDefaultValueToCompoundSegmentVar template:{template},origSegment:{origSegment},varName:{varName}");
				}
				nextVarStart = segment.IndexOf("{", nextVarEnd + 1, StringComparison.Ordinal);
				string literal;
				if (nextVarStart > 0) {
					if (nextVarStart == nextVarEnd + 1) {
						throw new ArgumentException($"UTDoesNotSupportAdjacentVarsInCompoundSegment template:{template},segment:{segment}"); 
					}
					literal = segment.Substring(nextVarEnd + 1, nextVarStart - nextVarEnd - 1);
				} else if (nextVarEnd + 1 < segment.Length) {
					literal = segment.Substring(nextVarEnd + 1);
				} else {
					literal = string.Empty;
				}
				if (literal.IndexOf(UriTemplate.WildcardPath, StringComparison.Ordinal) != -1) {
					throw new FormatException($"UTInvalidWildcardInVariableOrLiteral {template.OriginalTemplate} {UriTemplate.WildcardPath}");
				}
				if (literal.IndexOf('}') != -1) {
					throw new FormatException($"UTInvalidFormatSegmentOrQueryPart {segment}");
				}
				result._varLitPairs.Add(new VarAndLitPair(varName, ((literal == string.Empty) ? string.Empty : Uri.UnescapeDataString(literal))));
			} while (nextVarStart > 0);

			if (string.IsNullOrEmpty(result._firstLiteral)) {
				if (string.IsNullOrEmpty(result._varLitPairs[result._varLitPairs.Count - 1].Literal)) {
					result._csClass = CompoundSegmentClass.HasNoPrefixNorSuffix;
				} else {
					result._csClass = CompoundSegmentClass.HasOnlySuffix;
				}
			} else {
				if (string.IsNullOrEmpty(result._varLitPairs[result._varLitPairs.Count - 1].Literal)) {
					result._csClass = CompoundSegmentClass.HasOnlyPrefix;
				} else {
					result._csClass = CompoundSegmentClass.HasPrefixAndSuffix;
				}
			}

			return result;
		}

		public override void Bind(string[] values, ref int valueIndex, StringBuilder path) {
			if (valueIndex + _varLitPairs.Count <= values.Length == false)
				throw new Exception("Not enough values to bind");
			path.Append(_firstLiteral);
			for (var pairIndex = 0; pairIndex < _varLitPairs.Count; pairIndex++) {
				path.Append(values[valueIndex++]);
				path.Append(_varLitPairs[pairIndex].Literal);
			}
			if (EndsWithSlash) {
				path.Append("/");
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
			var otherAsCompound = other as UriTemplateCompoundPathSegment;
			if (otherAsCompound == null) {
				// if other can't be cast as a compound then it can't be equivalent
				return false;
			}
			if (_varLitPairs.Count != otherAsCompound._varLitPairs.Count) {
				return false;
			}
			if (StringComparer.OrdinalIgnoreCase.Compare(_firstLiteral, otherAsCompound._firstLiteral) != 0) {
				return false;
			}
			for (var pairIndex = 0; pairIndex < _varLitPairs.Count; pairIndex++) {
				if (StringComparer.OrdinalIgnoreCase.Compare(_varLitPairs[pairIndex].Literal,
					otherAsCompound._varLitPairs[pairIndex].Literal) != 0) {
					return false;
				}
			}

			return true;
		}
		public override bool IsMatch(UriTemplateLiteralPathSegment segment, bool ignoreTrailingSlash) {
			if (!ignoreTrailingSlash && (EndsWithSlash != segment.EndsWithSlash)) {
				return false;
			}
			return TryLookup(segment.AsUnescapedString(), null);
		}
		public override void Lookup(string segment, NameValueCollection boundParameters) {
			if (!TryLookup(segment, boundParameters)) {
				//Fx.Assert("How can that be? Lookup is expected to be called after IsMatch");
				throw new InvalidOperationException("UTCSRLookupBeforeMatch How can that be? Lookup is expected to be called after IsMatch");
			}
		}

		private bool TryLookup(string segment, NameValueCollection boundParameters) {
			var segmentPosition = 0;
			if (!string.IsNullOrEmpty(_firstLiteral)) {
				if (segment.StartsWith(_firstLiteral, StringComparison.Ordinal)) {
					segmentPosition = _firstLiteral.Length;
				} else {
					return false;
				}
			}
			for (var pairIndex = 0; pairIndex < _varLitPairs.Count - 1; pairIndex++) {
				var nextLiteralPosition = segment.IndexOf(_varLitPairs[pairIndex].Literal, segmentPosition, StringComparison.Ordinal);
				if (nextLiteralPosition < segmentPosition + 1) {
					return false;
				}
				if (boundParameters != null) {
					var varValue = segment.Substring(segmentPosition, nextLiteralPosition - segmentPosition);
					boundParameters.Add(_varLitPairs[pairIndex].VarName, varValue);
				}
				segmentPosition = nextLiteralPosition + _varLitPairs[pairIndex].Literal.Length;
			}
			if (segmentPosition < segment.Length) {
				if (string.IsNullOrEmpty(_varLitPairs[_varLitPairs.Count - 1].Literal)) {
					if (boundParameters != null) {
						boundParameters.Add(_varLitPairs[_varLitPairs.Count - 1].VarName,
							segment.Substring(segmentPosition));
					}
					return true;
				} else if ((segmentPosition + _varLitPairs[_varLitPairs.Count - 1].Literal.Length < segment.Length) &&
					  segment.EndsWith(_varLitPairs[_varLitPairs.Count - 1].Literal, StringComparison.Ordinal)) {
					if (boundParameters != null) {
						boundParameters.Add(_varLitPairs[_varLitPairs.Count - 1].VarName,
							segment.Substring(segmentPosition, segment.Length - segmentPosition - _varLitPairs[_varLitPairs.Count - 1].Literal.Length));
					}
					return true;
				} else {
					return false;
				}
			} else {
				return false;
			}
		}

		// A note about comparing compound segments:
		//  We are using this for generating the sorted collections at the nodes of the UriTemplateTrieNode.
		//  The idea is that we are sorting the segments based on preferred matching, when we have two
		//  compound segments matching the same wire segment, we will give preference to the preceding one.
		//  The order is based on the following concepts:
		//   - We are defining four classes of compound segments: prefix+suffix, prefix-only, suffix-only 
		//      and none
		//   - Whenever we are comparing segments from different class the preferred one is the segment with
		//      the prefared class, based on the order we defined them (p+s \ p \ s \ n).
		//   - Within each class the preference is based on the prefix\suffix, while prefix has precedence 
		//      over suffix if both exists.
		//   - If after comparing the class, as well as the prefix\suffix, we didn't reach to a conclusion,
		//      the preference is given to the segment with more variables parts.
		//  This order mostly follows the intuitive common sense; the major issue comes from preferring the
		//  prefix over the suffix in the case where both exist. This is derived from the problematic of any
		//  other type of solution that don't prefere the prefix over the suffix or vice versa. To better 
		//  understanding lets considered the following example:
		//   In comparing 'foo{x}bar' and 'food{x}ar', unless we are preferring prefix or suffix, we have
		//   to state that they have the same order. So is the case with 'foo{x}babar' and 'food{x}ar', which
		//   will lead us to claiming the 'foo{x}bar' and 'foo{x}babar' are from the same order, which they
		//   clearly are not. 
		//  Taking other approaches to this problem results in similar cases. The only solution is preferring
		//  either the prefix or the suffix over the other; since we already preferred prefix over suffix
		//  implicitly (we preferred the prefix only class over the suffix only, we also prefared literal
		//  over variable, if in the same path segment) that still maintain consistency.
		//  Therefore:
		//    - 'food{var}' should be before 'foo{var}'; '{x}.{y}.{z}' should be before '{x}.{y}'.
		//    - the order between '{var}bar' and '{var}qux' is not important
		//    - '{x}.{y}' and '{x}_{y}' should have the same order
		//    - 'foo{x}bar' is less preferred than 'food{x}ar'
		//  In the above third case - if we are opening the table with allowDuplicate=false, we will throw;
		//  if we are opening it with allowDuplicate=true we will let it go and might match both templates
		//  for certain wire candidates.
		int IComparable<UriTemplateCompoundPathSegment>.CompareTo(UriTemplateCompoundPathSegment other) {
			Ensure.NotNull(other, "We are only expected to get here for comparing real compound segments");

			switch (_csClass) {
				case CompoundSegmentClass.HasPrefixAndSuffix:
					switch (other._csClass) {
						case CompoundSegmentClass.HasPrefixAndSuffix:
							return CompareToOtherThatHasPrefixAndSuffix(other);

						case CompoundSegmentClass.HasOnlyPrefix:
						case CompoundSegmentClass.HasOnlySuffix:
						case CompoundSegmentClass.HasNoPrefixNorSuffix:
							return -1;

						default:
							throw new Exception("Invalid other.CompoundSegmentClass");
					}

				case CompoundSegmentClass.HasOnlyPrefix:
					switch (other._csClass) {
						case CompoundSegmentClass.HasPrefixAndSuffix:
							return 1;

						case CompoundSegmentClass.HasOnlyPrefix:
							return CompareToOtherThatHasOnlyPrefix(other);

						case CompoundSegmentClass.HasOnlySuffix:
						case CompoundSegmentClass.HasNoPrefixNorSuffix:
							return -1;

						default:
							throw new Exception("Invalid other.CompoundSegmentClass");
					}

				case CompoundSegmentClass.HasOnlySuffix:
					switch (other._csClass) {
						case CompoundSegmentClass.HasPrefixAndSuffix:
						case CompoundSegmentClass.HasOnlyPrefix:
							return 1;

						case CompoundSegmentClass.HasOnlySuffix:
							return CompareToOtherThatHasOnlySuffix(other);

						case CompoundSegmentClass.HasNoPrefixNorSuffix:
							return -1;

						default:
							throw new Exception("Invalid other.CompoundSegmentClass");
					}

				case CompoundSegmentClass.HasNoPrefixNorSuffix:
					switch (other._csClass) {
						case CompoundSegmentClass.HasPrefixAndSuffix:
						case CompoundSegmentClass.HasOnlyPrefix:
						case CompoundSegmentClass.HasOnlySuffix:
							return 1;

						case CompoundSegmentClass.HasNoPrefixNorSuffix:
							return CompareToOtherThatHasNoPrefixNorSuffix(other);

						default:
							throw new Exception("Invalid other.CompoundSegmentClass");
					}

				default:
					throw new Exception("Invalid this.CompoundSegmentClass");
			}
		}

		private int CompareToOtherThatHasPrefixAndSuffix(UriTemplateCompoundPathSegment other) {
			if (_csClass == CompoundSegmentClass.HasPrefixAndSuffix == false)
				throw new Exception("Otherwise, how did we got here?");
			if (other._csClass == CompoundSegmentClass.HasPrefixAndSuffix == false)
				throw new Exception("Otherwise, how did we got here?");

			// In this case we are determining the order based on the prefix of the two segments,
			//  then by their suffix and then based on the number of variables
			var prefixOrder = ComparePrefixToOtherPrefix(other);
			if (prefixOrder == 0) {
				var suffixOrder = CompareSuffixToOtherSuffix(other);
				if (suffixOrder == 0) {
					return (other._varLitPairs.Count - _varLitPairs.Count);
				} else {
					return suffixOrder;
				}
			} else {
				return prefixOrder;
			}
		}

		private int CompareToOtherThatHasOnlyPrefix(UriTemplateCompoundPathSegment other) {
			if (_csClass == CompoundSegmentClass.HasOnlyPrefix == false)
				throw new Exception("Otherwise, how did we got here?");
			if (other._csClass == CompoundSegmentClass.HasOnlyPrefix == false)
				throw new Exception("Otherwise, how did we got here?");

			// In this case we are determining the order based on the prefix of the two segments,
			//  then based on the number of variables
			var prefixOrder = ComparePrefixToOtherPrefix(other);
			if (prefixOrder == 0) {
				return (other._varLitPairs.Count - _varLitPairs.Count);
			} else {
				return prefixOrder;
			}
		}

		private int CompareToOtherThatHasOnlySuffix(UriTemplateCompoundPathSegment other) {
			if (_csClass == CompoundSegmentClass.HasOnlySuffix == false)
				throw new Exception("Otherwise, how did we got here?");
			if (other._csClass == CompoundSegmentClass.HasOnlySuffix == false)
				throw new Exception("Otherwise, how did we got here?");

			// In this case we are determining the order based on the suffix of the two segments,
			//  then based on the number of variables
			var suffixOrder = CompareSuffixToOtherSuffix(other);
			if (suffixOrder == 0) {
				return (other._varLitPairs.Count - _varLitPairs.Count);
			} else {
				return suffixOrder;
			}
		}

		private int CompareToOtherThatHasNoPrefixNorSuffix(UriTemplateCompoundPathSegment other) {
			if (_csClass == CompoundSegmentClass.HasNoPrefixNorSuffix == false)
				throw new Exception("Otherwise, how did we got here?");
			if (other._csClass == CompoundSegmentClass.HasNoPrefixNorSuffix == false)
				throw new Exception("Otherwise, how did we got here?");

			// In this case the order is determined by the number of variables
			return (other._varLitPairs.Count - _varLitPairs.Count);
		}

		private int ComparePrefixToOtherPrefix(UriTemplateCompoundPathSegment other) {
			return string.Compare(other._firstLiteral, _firstLiteral, StringComparison.OrdinalIgnoreCase);
		}

		private int CompareSuffixToOtherSuffix(UriTemplateCompoundPathSegment other) {
			var reversedSuffix = ReverseString(_varLitPairs[_varLitPairs.Count - 1].Literal);
			var reversedOtherSuffix = ReverseString(other._varLitPairs[other._varLitPairs.Count - 1].Literal);
			return string.Compare(reversedOtherSuffix, reversedSuffix, StringComparison.OrdinalIgnoreCase);
		}

		private static string ReverseString(string stringToReverse) {
			var reversedString = new char[stringToReverse.Length];
			for (var i = 0; i < stringToReverse.Length; i++) {
				reversedString[i] = stringToReverse[stringToReverse.Length - i - 1];
			}
			return new string(reversedString);
		}

		private enum CompoundSegmentClass {
			Undefined,
			HasPrefixAndSuffix,
			HasOnlyPrefix,
			HasOnlySuffix,
			HasNoPrefixNorSuffix
		}

		private struct VarAndLitPair {
			private readonly string _literal;
			private readonly string _varName;

			public VarAndLitPair(string varName, string literal) {
				_varName = varName;
				_literal = literal;
			}

			public string Literal {
				get {
					return _literal;
				}
			}
			public string VarName {
				get {
					return _varName;
				}
			}
		}
	}
}
