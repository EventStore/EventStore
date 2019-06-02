using System;
using System.Collections.Generic;

namespace EventStore.UriTemplate {
	public class UriTemplateEquivalenceComparer : IEqualityComparer<UriTemplate> {
		static UriTemplateEquivalenceComparer _instance;
		internal static UriTemplateEquivalenceComparer Instance {
			get {
				if (_instance == null) {
					// lock-free, fine if we allocate more than one
					_instance = new UriTemplateEquivalenceComparer();
				}
				return _instance;
			}
		}

		public bool Equals(UriTemplate x, UriTemplate y) {
			if (x == null) {
				return y == null;
			}
			return x.IsEquivalentTo(y);
		}
		public int GetHashCode(UriTemplate obj) {
			if (obj == null) {
				throw new ArgumentNullException(nameof(obj));
			}
#pragma warning disable 56506 // obj.xxx is never null
			// prefer final literal segment (common literal prefixes are common in some scenarios)
			for (int i = obj.Segments.Count - 1; i >= 0; --i) {
				if (obj.Segments[i].Nature == UriTemplatePartType.Literal) {
					return obj.Segments[i].GetHashCode();
				}
			}
			return obj.Segments.Count + obj.Queries.Count;
#pragma warning restore 56506
		}
	}
}
