using System.Collections.Generic;

namespace EventStore.UriTemplate {
	internal class EmptyArray<T> {
		private static T[] _instance;

		private EmptyArray() {
		}

		internal static T[] Instance {
			get {
				if (_instance == null)
					_instance = new T[0];
				return _instance;
			}
		}

		internal static T[] Allocate(int n) {
			if (n == 0)
				return Instance;
			else
				return new T[n];
		}

		internal static T[] ToArray(IList<T> collection) {
			if (collection.Count == 0) {
				return Instance;
			} else {
				var array = new T[collection.Count];
				collection.CopyTo(array, 0);
				return array;
			}
		}
	}

	internal class EmptyArray {
		private static object[] _instance = new object[0];

		private EmptyArray() {
		}

		internal static object[] Instance {
			get {
				return _instance;
			}
		}

		internal static object[] Allocate(int n) {
			if (n == 0)
				return Instance;
			else
				return new object[n];
		}
	}
}
