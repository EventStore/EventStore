using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Rags {
	public static class TypeMap {
		private static Dictionary<Type, Func<string, string, object>> _translators;

		private static Dictionary<Type, Func<string, string, object>> Translators {
			get {
				if (_translators != null) return _translators;
				_translators = new Dictionary<Type, Func<string, string, object>>();
				LoadDefaultTranslators();
				return _translators;
			}
		}

		public static void Register<T>(Func<string, string, T> translator) {
			if (translator == null) throw new ArgumentNullException("translator");
			_translators.Add(typeof(T), (x, y) => translator(x, y));
		}

		internal static bool CanMap(Type t) {
			if (Translators.ContainsKey(t) ||
			    t.IsEnum ||
			    (t.GetInterfaces().Contains(typeof(IList)) && t.IsGenericType && CanMap(t.GetGenericArguments()[0])) ||
			    (t.IsArray && CanMap(t.GetElementType())))
				return true;

			return System.ComponentModel.TypeDescriptor.GetConverter(t).CanConvertFrom(typeof(string)) ||
			       Translators.ContainsKey(t);
		}

		public static object Translate(Type t, string name, string value) {
			if (t.IsArray == false && t.GetInterfaces().Contains(typeof(IList))) {
				var list = (IList)Activator.CreateInstance(t);
				// TODO - Maybe support custom delimiters via an attribute on the property
				// TODO - Maybe do a full parse of the value to check for quoted strings
				if (string.IsNullOrWhiteSpace(value)) return list;
				foreach (var element in value.Split(',')) {
					list.Add(Translate(t.GetGenericArguments()[0], name + "_element", element));
				}

				return list;
			}

			if (t.IsArray) {
				var elements = value.Split(',');

				if (string.IsNullOrWhiteSpace(value) != false) return Array.CreateInstance(t.GetElementType(), 0);
				var array = Array.CreateInstance(t.GetElementType(), elements.Length);
				for (var i = 0; i < array.Length; i++) {
					array.SetValue(Translate(t.GetElementType(), name + "[" + i + "]", elements[i]), i);
				}

				return array;
			}

			if (Translators.ContainsKey(t))
				return Translators[t].Invoke(name, value);
			if (System.ComponentModel.TypeDescriptor.GetConverter(t).CanConvertFrom(typeof(string)))
				return System.ComponentModel.TypeDescriptor.GetConverter(t).ConvertFromString(value);
			// Intentionally not an InvalidArgDefinitionException.  Other internal code should call 
			// CanRevive and this block should never be executed.
			throw new ArgumentException("Cannot revive type " + t.FullName +
			                            ". Callers should be calling CanRevive before calling Revive()");
		}

		private static void LoadDefaultTranslators() {
			Register(Rags.Translators.TranslateBool);
			Register(Rags.Translators.TranslateGuid);
			Register(Rags.Translators.TranslateByte);
			Register(Rags.Translators.TranslateInt);
			Register(Rags.Translators.TranslateLong);
			Register(Rags.Translators.TranslateDouble);
			Register((prop, val) => val);
			Register(Rags.Translators.TranslateDateTime);
			Register(Rags.Translators.TranslateUri);
			Register(Rags.Translators.TranlateIPAddress);
			Register(Rags.Translators.TranslateIPEndPoint);
		}
	}
}
