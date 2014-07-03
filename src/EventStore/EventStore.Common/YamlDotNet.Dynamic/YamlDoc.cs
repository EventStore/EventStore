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

using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace YamlDotNet.Dynamic
{
	public static class YamlDoc
	{
		public static YamlNode LoadFromFile(string fileName)
		{
			return LoadFromTextReader(File.OpenText(fileName));
		}

		public static YamlNode LoadFromString(string yamlText)
		{
			return LoadFromTextReader(new StringReader(yamlText));
		}

		public static YamlNode LoadFromTextReader(TextReader reader)
		{
			var yaml = new YamlStream();
			yaml.Load(reader);

			return yaml.Documents.First().RootNode;
		}

		internal static bool TryMapValue(object value, out object result)
		{
			var node = value as YamlNode;
			if (node != null)
			{
				result = new DynamicYaml(node);
				return true;
			}

			result = null;
			return false;
		}
	}
}
