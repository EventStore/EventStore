// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Transport.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Formatting = Newtonsoft.Json.Formatting;
using System.Linq;

namespace EventStore.Core.Services.Transport.Http
{
    public static class Codec
    {
        public static readonly NoCodec NoCodec = new NoCodec();
        public static readonly ICodec[] NoCodecs = new ICodec[0];
        public static readonly ManualEncoding ManualEncoding = new ManualEncoding();

        public static readonly JsonCodec Json = new JsonCodec();
        public static readonly XmlCodec Xml = new XmlCodec();
        public static readonly TextCodec Text = new TextCodec();

        public static ICodec CreateCustom(ICodec codec, string contentType, string format)
        {
            return new CustomCodec(codec, contentType, format);
        }
    }

    public class NoCodec : ICodec
    {
        public string ContentType
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public bool SuitableFor(string type)
        {
            throw new NotSupportedException();
        }

        public T From<T>(string text)
        {
            throw new NotSupportedException();
        }

        public string To<T>(T value)
        {
            throw new NotSupportedException();
        }
    }

    public class ManualEncoding : ICodec
    {
        public string ContentType
        {
            get
            {
                throw new InvalidOperationException();
            }
        }

        public bool SuitableFor(string type)
        {
            return true;
        }

        public T From<T>(string text)
        {
            throw new InvalidOperationException();
        }

        public string To<T>(T value)
        {
            throw new InvalidOperationException();
        }
    }

    public class CustomCodec : ICodec
    {
        public ICodec BaseCodec
        {
            get
            {
                return _codec;
            }
        }

        private readonly ICodec _codec;
        private readonly string _contentType;
        private readonly string _format;

        internal CustomCodec(ICodec codec, string contentType, string format)
        {
            Ensure.NotNull(codec, "codec");
            Ensure.NotNull(contentType, "contentType");
            Ensure.NotNull(format, "format");

            _codec = codec;
            _contentType = contentType;
            _format = format;
        }

        public string ContentType
        {
            get
            {
                return _contentType;
            }
        }

        public bool SuitableFor(string type)
        {
            return string.Equals(_contentType, type, StringComparison.InvariantCultureIgnoreCase) ||
                   string.Equals(_format, type, StringComparison.InvariantCultureIgnoreCase);
        }

        public T From<T>(string text)
        {
            return _codec.From<T>(text);
        }

        public string To<T>(T value)
        {
            return _codec.To(value);
        }
    }

    public class JsonCodec : ICodec
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<JsonCodec>();

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                TypeNameHandling = TypeNameHandling.None,
                Converters = new JsonConverter[]
                    {
                        new StringEnumConverter()
                    }
            };

        public string ContentType
        {
            get
            {
                return EventStore.Transport.Http.ContentType.Json;
            }
        }

        public bool SuitableFor(string type)
        {
            if (string.Equals("json", type, StringComparison.InvariantCultureIgnoreCase))
                return true;

            type = (type ?? string.Empty).Split(';').FirstOrDefault();
            return string.Equals(ContentType, type, StringComparison.InvariantCultureIgnoreCase);
        }

        public T From<T>(string text)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(text, JsonSettings);
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "'{0}' is not a valid serialized {1}", text, typeof(T).FullName);
                return default(T);
            }
        }

        public string To<T>(T value)
        {
            try
            {
#if DEBUG
                return JsonConvert.SerializeObject(value, Formatting.Indented, JsonSettings);
#else
                return JsonConvert.SerializeObject(value, JsonSettings);
#endif
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error serializing object {0}", value);
                return null;
            }
        }

        public string ToXmlUsingJson(object value)
        {
            try
            {
                var json = To(value);

                var doc = JsonConvert.DeserializeXmlNode(json, value.GetType().Name);
                var xml = new StringBuilder();

                using(var writer = XmlWriter.Create(xml))
                    doc.WriteTo(writer);

                return xml.ToString();
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "Error serializing object {0}", value);
                return null;
            }
        }
    }

    public class XmlCodec : ICodec
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<XmlCodec>();

        public string ContentType
        {
            get
            {
                return EventStore.Transport.Http.ContentType.Xml;
            }
        }

        public bool SuitableFor(string type)
        {
            if (string.Equals("xml", type, StringComparison.InvariantCultureIgnoreCase))
                return true;

            type = (type ?? string.Empty).Split(';').FirstOrDefault();

            if (string.Equals(ContentType, type, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (string.Equals("application/xml", type, StringComparison.InvariantCultureIgnoreCase))
                return true;

            return false;
        }

        public T From<T>(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return default(T);
            }

            try
            {
                using (var memory = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                {
                    return (T)new DataContractSerializer(typeof(T)).ReadObject(memory);
                }
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "'{0}' is not a valid serialized {1}", text, typeof(T).FullName);
                return default(T);
            }
        }

        public string To<T>(T value)
        {
            if ((object)value == null)
                return null;

            var serializable = value as IXmlSerializable;
            if (serializable != null)
                return ToSerializable(serializable);

            try
            {
                using (var memory = new MemoryStream())
                {
                    new DataContractSerializer(value.GetType()).WriteObject(memory, value);
                    memory.Flush();
                    memory.Seek(0L, SeekOrigin.Begin);
                    return Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Error serializing object {0}", value);
                return null;
            }
        }

        private string ToSerializable(IXmlSerializable serializable)
        {
            if (serializable == null)
            {
                return null;
            }

            try
            {
                using (var memory = new MemoryStream())
                using (var writer = XmlWriter.Create(memory))
                {
                    writer.WriteStartDocument();
                    serializable.WriteXml(writer);
                    writer.WriteEndDocument();
                    writer.Flush();

                    memory.Seek(0L, SeekOrigin.Begin);
                    return Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
                }
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "Error serializing object of type {0}", serializable.GetType().FullName);
                return null;
            }
        }
    }

    public class TextCodec : ICodec
    {
        public string ContentType
        {
            get
            {
                return EventStore.Transport.Http.ContentType.PlainText;
            }
        }

        public bool SuitableFor(string type)
        {
            return string.Equals(ContentType, type, StringComparison.InvariantCultureIgnoreCase) ||
                   string.Equals("text", type, StringComparison.InvariantCultureIgnoreCase);
        }

        public T From<T>(string text)
        {
            throw new NotSupportedException();
        }

        public string To<T>(T value)
        {
            return ((object) value) != null ? value.ToString() : null;
        }
    }
}