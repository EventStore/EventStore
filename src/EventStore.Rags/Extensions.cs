using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace EventStore.Rags {
	/// <summary>
	/// Provides some reflection helpers in the form of extension methods for the MemberInfo type.
	/// </summary>
	public static class MemberInfoEx {
		static readonly Dictionary<string, object> cachedAttributes = new Dictionary<string, object>();

		/// <summary>
		/// Returns true if the given member has an attribute of the given type (including inherited types).
		/// </summary>
		/// <typeparam name="T">The type of attribute to test for (will return true for attributes that inherit from this type)</typeparam>
		/// <param name="info">The member to test</param>
		/// <returns>true if a matching attribute was found, false otherwise</returns>
		public static bool HasAttr<T>(this MemberInfo info) {
			return info.Attrs<T>().Count > 0;
		}

		/// <summary>
		/// Gets the attribute of the given type or null if the member does not have this attribute defined.  The standard reflection helper GetCustomAttributes will
		/// give you a new instance of the attribute every time you call it.  This helper caches it's results so if you ask for the same attibute twice you will actually
		/// get back the same attribute.  Note that the cache key is based off of the type T that you provide.  So asking for Attr() where T : BaseType> and then asking for Attr() where T : ConcreteType 
		/// will result in two different objects being returned.  If you ask for Attr() where T : BaseType and then Attr() where T :BaseType the caching will work and you'll get the same object back
		/// the second time.
		/// </summary>
		/// <typeparam name="T">The type of attribute to search for</typeparam>
		/// <param name="info">The member to inspect</param>
		/// <returns>The desired attribute or null if it is not present</returns>
		public static T Attr<T>(this MemberInfo info) {
			if (info.HasAttr<T>()) {
				return info.Attrs<T>()[0];
			} else {
				return default(T);
			}
		}

		/// <summary>
		/// Gets the attributes of the given type.  The standard reflection helper GetCustomAttributes will give you new instances of the attributes every time you call it.  
		/// This helper caches it's results so if you ask for the same attibutes twice you will actually get back the same attributes.  Note that the cache key is based off 
		/// of the type T that you provide.  So asking for Attrs() where T : BaseType and then asking for Attrs() where T : ConcreteType
		/// will result in two different sets of objects being returned.  If you ask for Attrs() where T : BaseType and then Attrs() where T : BaseType the caching will work and you'll get the
		/// same results back the second time.
		/// </summary>
		/// <typeparam name="T">The type of attribute to search for</typeparam>
		/// <param name="info">The member to inspect</param>
		/// <returns>The list of attributes that you asked for</returns>
		public static List<T> Attrs<T>(this MemberInfo info) {
			string cacheKey = (info is Type ? ((Type)info).FullName : info.DeclaringType.FullName + "." + info.Name) +
			                  "<" + typeof(T).FullName + ">";

			if (cachedAttributes.ContainsKey(cacheKey)) {
				var cachedValue = cachedAttributes[cacheKey] as List<T>;
				if (cachedValue != null) return cachedValue;
			}

			var freshValue = (from attr in info.GetCustomAttributes(true)
				where attr.GetType() == typeof(T) ||
				      attr.GetType().IsSubclassOf(typeof(T)) ||
				      attr.GetType().GetInterfaces().Contains(typeof(T))
				select (T)attr).ToList();

			if (cachedAttributes.ContainsKey(cacheKey)) {
				cachedAttributes[cacheKey] = freshValue;
			} else {
				cachedAttributes.Add(cacheKey, freshValue);
			}

			return freshValue;
		}
	}

	internal static class FieldInfoExtensions {
		internal static List<string> GetEnumShortcuts(this FieldInfo enumField) {
			if (enumField.DeclaringType.IsEnum == false)
				throw new ArgumentException("The given field '" + enumField.Name + "' is not an enum field.");

			var shortcutAttrs = enumField.Attrs<ArgShortcut>();
			var noShortcutPolicy = shortcutAttrs.SingleOrDefault(s => s.Shortcut == null);
			var shortcutVals = shortcutAttrs.Where(s => s.Shortcut != null).Select(s => s.Shortcut).ToList();

			if (noShortcutPolicy != null && shortcutVals.Count > 0)
				throw new InvalidArgDefinitionException(
					"You can't have an ArgShortcut attribute with a null shortcut and then define a second ArgShortcut attribute with a non-null value.");

			return shortcutVals;
		}
	}

	internal static class TypeExtensions {
		internal static List<string> GetEnumShortcuts(this Type enumType) {
			var ret = new List<string>();
			foreach (var field in enumType.GetFields().Where(f => f.IsSpecialName == false)) {
				ret.AddRange(field.GetEnumShortcuts());
			}

			return ret;
		}

		internal static bool TryMatchEnumShortcut(this Type enumType, string value, bool ignoreCase,
			out object enumResult) {
			if (ignoreCase) value = value.ToLower();
			foreach (var field in enumType.GetFields().Where(f => f.IsSpecialName == false)) {
				var shortcuts = field.GetEnumShortcuts();
				if (ignoreCase) shortcuts = shortcuts.Select(s => s.ToLower()).ToList();
				var match = (from s in shortcuts
					where s == value
					select s).SingleOrDefault();
				if (match == null) continue;
				enumResult = Enum.Parse(enumType, field.Name);
				return true;
			}

			enumResult = null;
			return false;
		}

		internal static void ValidateNoDuplicateEnumShortcuts(this Type enumType, bool ignoreCase) {
			if (enumType.IsEnum == false) throw new ArgumentException("Type " + enumType.Name + " is not an enum");

			var shortcutsSeenSoFar = new List<string>();
			foreach (var field in enumType.GetFields().Where(f => f.IsSpecialName == false)) {
				var shortcutsForThisField = field.GetEnumShortcuts();
				if (ignoreCase) shortcutsForThisField = shortcutsForThisField.Select(s => s.ToLower()).ToList();

				foreach (var shortcut in shortcutsForThisField) {
					if (shortcutsSeenSoFar.Contains(shortcut))
						throw new InvalidArgDefinitionException("Duplicate shortcuts defined for enum type '" +
						                                        enumType.Name + "'");
					shortcutsSeenSoFar.Add(shortcut);
				}
			}
		}
	}

	public static class IEnumerableExtensions {
		public static T ApplyTo<T>(this IEnumerable<OptionSource> source) where T : class, new() {
			return OptionApplicator.Get<T>(source);
		}

		public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> source) {
			return source.SelectMany(x => x);
		}

		public static IEnumerable<OptionSource> Normalize(this IEnumerable<OptionSource> source) {
			return source.Select(x => new OptionSource(x.Source, x.Name,
				new string[] {"+", "-", ""}.Contains(x.Value.ToString()) ? true : x.IsTyped,
				x.Value.ToString() == "+" ? true :
				x.Value.ToString() == "-" ? false :
				x.Value.ToString() == "" ? true : x.Value));
		}

		public static IEnumerable<OptionSource> UseAliases<TOptions>(this IEnumerable<OptionSource> optionSources)
			where TOptions : class {
			var properties = typeof(TOptions).GetProperties();
			foreach (var property in properties) {
				var aliases = property.HasAttr<ArgAliasAttribute>() ? property.Attr<ArgAliasAttribute>().Aliases : null;
				if (aliases != null) {
					foreach (var alias in aliases) {
						optionSources = optionSources.Select(x =>
							x.Name.Equals(alias, StringComparison.OrdinalIgnoreCase)
								? new OptionSource(x.Source, property.Name, x.IsTyped, x.Value)
								: new OptionSource(x.Source, x.Name, x.IsTyped, x.Value));
					}
				}
			}

			return optionSources;
		}
	}
}
