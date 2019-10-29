using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace EventStore.Core.Tests.Infrastructure {
	public class ReflectionBasedEqualityComparer : IComparer {
		private static bool ReflectionEquals(object x, object y) {
			if (ReferenceEquals(x, y))
				return true;

			if (ReferenceEquals(x, null))
				return false;

			if (ReferenceEquals(y, null))
				return false;

			var type = x.GetType();

			if (type != y.GetType())
				return false;

			if (x == y)
				return true;

			if (type.IsValueType)
				return x.Equals(y);

			if (type == typeof(string)) {
				return x.Equals(y);
			}

			if (typeof(Array).IsAssignableFrom(type)) {
				var dx = (Array)x;
				var dy = (Array)y;
				if (dx.Length != dy.Length) {
					return false;
				}
				return ((IStructuralEquatable)dx).Equals(dy, StructuralComparisons.StructuralEqualityComparer);
			}

			if (typeof(IDictionary).IsAssignableFrom(type)) {
				var dx = (IDictionary)x;
				var dy = (IDictionary)y;

				if (dx.Count != dy.Count) {
					return false;
				}

				foreach (DictionaryEntry entry in dx)
				{
					if (!dy.Contains(entry.Key) || !Instance.Equals(entry.Value, dy[entry.Key])) {
						return false;
					}
				}

				return true;
			}

			var fieldValues =
				from field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)
				select new {
					member = (MemberInfo)field,
					x = field.GetValue(x),
					y = field.GetValue(y)
				};

			var propertyValues =
				from property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
				select new {
					member = (MemberInfo)property,
					x = property.GetValue(x),
					y = property.GetValue(y)
				};

			var values = fieldValues.Concat(propertyValues);

			var differences = values.Where(value => !ReflectionEquals(value.x, value.y)).ToList();

			return !differences.Any();
		}

		public new bool Equals(object x, object y) => ReflectionEquals(x, y);
		public int Compare(object x, object y) => ReflectionEquals(x, y) ? 0 : 1;
		public static readonly ReflectionBasedEqualityComparer Instance = new ReflectionBasedEqualityComparer();
	}
}
