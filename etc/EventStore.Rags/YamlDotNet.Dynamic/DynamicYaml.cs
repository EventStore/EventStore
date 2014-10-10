//  This file is part of YamlDotNet - A .NET library for YAML.
//  Copyright (c) 2008, 2009, 2010, 2011, 2012, 2013 Antoine Aubry

//  Permission is hereby granted, free of charge, to any person obtaining a copy of
//  this software and associated documentation files (the "Software"), to deal in
//  the Software without restriction, including without limitation the rights to
//  use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
//  of the Software, and to permit persons to whom the Software is furnished to do
//  so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.

//  Credits for this class: https://github.com/imgen

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using EventStore.Rags.YamlDotNet.RepresentationModel;

namespace EventStore.Rags.YamlDotNet.Dynamic
{
	public class DynamicYaml : DynamicObject
	{
		private static readonly Type[] ConvertableBasicTypes =
		{
			typeof(DynamicYaml),
			typeof(object),
			typeof(string),
			typeof(char),
			typeof(int),
			typeof(long),
			typeof(float),
			typeof(double),
			typeof(decimal),
			typeof(bool)
		};


		private static readonly Type[] ConvertableGenericCollectionTypes =
		{
			typeof(IEnumerable<>),
			typeof(ICollection<>),
			typeof(IList<>),
			typeof(List<>)
		};

		private static readonly Type[] ConvertableGenericDictionaryTypes =
		{
			typeof(IDictionary<,>),
			typeof(Dictionary<,>)
		};

		private static readonly Type[] ConvertableCollectionTypes =
			ConvertableGenericCollectionTypes.SelectMany(
				type => ConvertableBasicTypes.Select(
					basicType => type.MakeGenericType(basicType))).ToArray();

		private static readonly Type[] ConvertableDictionaryTypes =
			ConvertableGenericDictionaryTypes.SelectMany(
				type => ConvertableBasicTypes.SelectMany(
					valueType => ConvertableBasicTypes.Select(
						keyType => type.MakeGenericType(keyType, valueType)))).ToArray();

		private static readonly Type[] ConvertableArrayTypes =
			ConvertableBasicTypes.Select(type => type.MakeArrayType()).ToArray();

		private YamlMappingNode mappingNode;
		private YamlSequenceNode sequenceNode;
		private YamlScalarNode scalarNode;
		private YamlNode yamlNode;

		public DynamicYaml(YamlNode node)
		{
			Reload(node);
		}

		public DynamicYaml(TextReader reader)
			: this(YamlDoc.LoadFromTextReader(reader))
		{
		}

		public DynamicYaml(string yaml)
			: this(YamlDoc.LoadFromString(yaml))
		{
		}

		public void Reload(YamlNode node)
		{
			yamlNode = node;
			mappingNode = yamlNode as YamlMappingNode;
			sequenceNode = yamlNode as YamlSequenceNode;
			scalarNode = yamlNode as YamlScalarNode;
			children = null;
		}

		public void Reload(TextReader reader)
		{
			Reload(YamlDoc.LoadFromTextReader(reader));
		}

		public void Reload(string yaml)
		{
			Reload(YamlDoc.LoadFromString(yaml));
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			return TryGetValueByKeyAndType(binder.Name, binder.ReturnType, out result);
		}

		private static bool FailToGetValue(out object result)
		{
			result = null;
			return false;
		}

		private static bool SuccessfullyGetValue(object value, out object result)
		{
			result = value;
			return true;
		}

		private bool TryGetValueByKeyAndType(string key, Type type, out object result)
		{
			if (mappingNode == null)
			{
				return FailToGetValue(out result);
			}

			// try and get an exact match to the key first
			if (TryGetValueByYamlKeyAndType(key, type, out result))
			{
				return true;
			}

			// otherwise try and match the key with a different cased first character
			var yamlKey = new YamlScalarNode(key.InverseFirstCapital());
			if (TryGetValueByYamlKeyAndType(yamlKey, type, out result))
			{
				return true;
			}

			return IsNullableType(type) ? SuccessfullyGetValue(new DynamicYaml((YamlNode)null), out result) : FailToGetValue(out result);
		}

		private bool TryGetValueByYamlKeyAndType(YamlScalarNode yamlKey, Type type, out object result)
		{
			if (mappingNode.Children.ContainsKey(yamlKey))
			{
				var value = mappingNode.Children[yamlKey];
				if (YamlDoc.TryMapValue(value, out result))
				{
					return true;
				}
			}
			
			return FailToGetValue(out result);
		}

		private static bool IsNullableType(Type type)
		{
			return type != null && (!type.IsValueType || Nullable.GetUnderlyingType(type) != null);
		}

		private bool TryGetValueByIndex(int index, out object result)
		{
			if (sequenceNode == null)
			{
				return FailToGetValue(out result);
			}

			if (index >= sequenceNode.Count())
			{
				throw new IndexOutOfRangeException();
			}

			return YamlDoc.TryMapValue(sequenceNode.ToArray()[index], out result);
		}

		public override bool TryGetIndex(GetIndexBinder binder, object[] indices, out object result)
		{
			var stringKey = indices[0] as string;
			if (stringKey != null)
			{
				if (TryGetValueByKeyAndType(stringKey, binder.ReturnType, out result))
				{
					return indices.Length <= 1 || TryGetIndex(binder, indices.Skip(1).ToArray(), out result);
				}

				return FailToGetValue(out result);
			}

			var intKey = indices[0] as int?;
			if (intKey != null)
			{
				if (TryGetValueByIndex(intKey.Value, out result))
				{
					if (indices.Length > 1)
					{
						if (result is DynamicYaml)
						{
							return ((DynamicYaml)result).TryGetIndex(binder, indices.Skip(1).ToArray(), out result);
						}
						return FailToGetValue(out result);
					}

					return true;
				}

				return FailToGetValue(out result);
			}

			return base.TryGetIndex(binder, indices, out result);
		}

		private bool TryConvertToBasicType(Type type, bool isNullable, out object result)
		{
			if (type == typeof(object) || type == typeof(DynamicYaml))
			{
				return SuccessfullyGetValue(this, out result);
			}
			if (scalarNode == null)
			{
				return isNullable? SuccessfullyGetValue(null, out result): FailToGetValue(out result);
			}
			if (type == typeof(string))
			{
				return SuccessfullyGetValue(scalarNode.Value, out result);
			}
			if (type == typeof(char))
			{
				char charResult;
				var success = char.TryParse(scalarNode.Value, out charResult);
				result = success ? (object)charResult : null;
				return success;
			}
			if (type == typeof(int))
			{
				int intResult;
				var success = int.TryParse(scalarNode.Value, out intResult);
				result = success ? (object)intResult : null;
				return success;
			}
			if (type == typeof(long))
			{
				long longResult;
				var success = long.TryParse(scalarNode.Value, out longResult);
				result = success ? (object)longResult : null;
				return success;
			}
			if (type == typeof(float))
			{
				float floatResult;
				var success = float.TryParse(scalarNode.Value, out floatResult);
				result = success ? (object)floatResult : null;
				return success;
			}
			if (type == typeof(double))
			{
				double doubleResult;
				var success = double.TryParse(scalarNode.Value, out doubleResult);
				result = success ? (object)doubleResult : null;
				return success;
			}
			if (type == typeof(decimal))
			{
				decimal decimalResult;
				var success = decimal.TryParse(scalarNode.Value, out decimalResult);
				result = success ? (object)decimalResult : null;
				return success;
			}
			if (type == typeof(bool))
			{
				bool boolResult;
				var success = bool.TryParse(scalarNode.Value, out boolResult);
				result = success ? (object)boolResult : null;
				return success;
			}
			if (type.IsEnum)
			{
				long longResult;
				if (long.TryParse(scalarNode.Value, out longResult))
				{
					result = longResult;
					return true;
				}

				try
				{
					result = Enum.Parse(type, scalarNode.Value);
					return true;
				}
				catch
				{
					return FailToGetValue(out result);
				}
			}

			return FailToGetValue(out result);
		}

		public override bool TryConvert(ConvertBinder binder, out object result)
		{
			var type = binder.ReturnType;

			return TryConvertToType(type, out result);
		}

		private bool IsGenericEnumCollection(Type type)
		{
			if (!type.IsGenericType)
			{
				return false;
			}

			var genericTypeArgs = type.GetGenericArguments();
			if (genericTypeArgs.Length != 1)
			{
				return false;
			}

			var elementType = genericTypeArgs.First();

			return elementType.IsEnum && ConvertableGenericCollectionTypes.Any(
				genericType => genericType.MakeGenericType(elementType) == type);
		}

		private bool IsLegalElementType(Type type)
		{
			return type.IsEnum || ConvertableBasicTypes.Contains(type);
		}

		private bool IsGenericEnumDictionary(Type type)
		{
			if (!type.IsGenericType)
			{
				return false;
			}

			var genericTypeArgs = type.GetGenericArguments();
			if (genericTypeArgs.Length != 2)
			{
				return false;
			}
			Type keyType = genericTypeArgs[0], valueType = genericTypeArgs[1];
			return (keyType.IsEnum || valueType.IsEnum) &&
				ConvertableGenericDictionaryTypes.
				Any(genericType => genericType.MakeGenericType(keyType, valueType) == type) &&
				IsLegalElementType(keyType) && IsLegalElementType(valueType);
		}

		private bool TryConvertToType(Type type, out object result)
		{
			if (type.IsArray &&
				(ConvertableArrayTypes.Contains(type) ||
				type.GetElementType().IsSubclassOf(typeof(Enum))))
			{
				return TryConvertToArray(type, out result);
			}
			if (ConvertableCollectionTypes.Contains(type) ||
				IsGenericEnumCollection(type))
			{
				return TryConvertToCollection(type, out result);
			}
			if (ConvertableDictionaryTypes.Contains(type) ||
				IsGenericEnumDictionary(type))
			{
				return TryConvertToDictionary(type, out result);
			}

			var underlyingType = Nullable.GetUnderlyingType(type);
			bool isNullableType = IsNullableType(type);
			if (underlyingType != null)
			{
				type = underlyingType;
			}
			return TryConvertToBasicType(type, isNullableType, out result);
		}

		private bool TryConvertToDictionary(Type type, out object result)
		{
			if (mappingNode == null)
			{
				return FailToGetValue(out result);
			}

			var genericTypeArgs = type.GetGenericArguments();
			Type keyType = genericTypeArgs[0],
				 valueType = genericTypeArgs[1];

			var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
			var dict = Activator.CreateInstance(dictType) as IDictionary;

			if (dict != null)
			{
				foreach (var pair in mappingNode.Children)
				{
					object key;
					if (!new DynamicYaml(pair.Key).TryConvertToType(keyType, out key))
					{
						return FailToGetValue(out result);
					}

					object value;
					if (!new DynamicYaml(pair.Value).TryConvertToType(valueType, out value))
					{
						return FailToGetValue(out result);
					}

					dict.Add(key, value);
				}
			}

			return SuccessfullyGetValue(dict, out result);
		}

		private bool TryConvertToCollection(Type type, out object result)
		{
			var elementType = type.GetGenericArguments().First();
			var listType = typeof(List<>).MakeGenericType(elementType);
			var list = Activator.CreateInstance(listType) as IList;

			if (list != null)
			{
				foreach (var child in Children)
				{
					object result2;
					if (!child.TryConvertToType(elementType, out result2))
					{
						return FailToGetValue(out result);
					}

					list.Add(result2);
				}
			}

			return SuccessfullyGetValue(list, out result);
		}

		private bool TryConvertToArray(Type type, out object result)
		{
			if (Children == null)
			{
				return FailToGetValue(out result);
			}
			var elementType = type.GetElementType();
			var arrayResult = Array.CreateInstance(elementType, Children.Count);
			var index = 0;
			foreach (var child in Children)
			{
				object result2;
				if (!child.TryConvertToType(elementType, out result2))
				{
					return FailToGetValue(out result);
				}
				arrayResult.SetValue(result2, index);
				index++;
			}

			return SuccessfullyGetValue(arrayResult, out result);
		}

		private IList<DynamicYaml> GetChildren()
		{
			if (mappingNode != null)
			{
				return mappingNode.Children.Values.Select(node => new DynamicYaml(node)).ToList();
			}

			if (sequenceNode != null)
			{
				return sequenceNode.Select(node => new DynamicYaml(node)).ToList();
			}

			return new List<DynamicYaml>();
		}

		private IList<DynamicYaml> children;
		public IList<DynamicYaml> Children
		{
			get { return children ?? (children = GetChildren()); }
		}

		public int Count
		{
			get
			{
				return Children != null ? Children.Count : 0;
			}
		}
	}
}
