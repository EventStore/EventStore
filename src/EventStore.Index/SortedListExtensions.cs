using System.Collections.Generic;

namespace EventStore.Core.Index {
	public static class SortedListExtensions {
		/// <summary>
		/// Returns the index of smallest (according to comparer) element greater than or equal to provided key.
		/// Returns -1 if all keys are smaller than provided key.
		/// </summary>
		public static int LowerBound<TKey, TValue>(this SortedList<TKey, TValue> list, TKey key) {
			if (list.Count == 0)
				return -1;

			var comparer = list.Comparer;
			if (comparer.Compare(list.Keys[list.Keys.Count - 1], key) < 0)
				return -1; // if all elements are smaller, then no lower bound

			int l = 0;
			int r = list.Count - 1;
			while (l < r) {
				int m = l + (r - l) / 2;
				if (comparer.Compare(list.Keys[m], key) >= 0)
					r = m;
				else
					l = m + 1;
			}

			return r;
		}

		/// <summary>
		/// Returns the index of largest (according to comparer) element less than or equal to provided key.
		/// Returns -1 if all keys are greater than provided key.
		/// </summary>
		public static int UpperBound<TKey, TValue>(this SortedList<TKey, TValue> list, TKey key) {
			if (list.Count == 0)
				return -1;

			var comparer = list.Comparer;
			if (comparer.Compare(key, list.Keys[0]) < 0)
				return -1; // if all elements are greater, then no upper bound

			int l = 0;
			int r = list.Count - 1;
			while (l < r) {
				int m = l + (r - l + 1) / 2;
				if (comparer.Compare(list.Keys[m], key) <= 0)
					l = m;
				else
					r = m - 1;
			}

			return l;
		}
	}
}
