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
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using EventStore.Common.Log;
using EventStore.Transport.Http;

namespace EventStore.Core.Services.Transport.Http.Codecs
{
    public class XmlCodec : ICodec
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<XmlCodec>();
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding(false); // we use our own encoding which doesn't produce BOM

        public string ContentType { get { return EventStore.Transport.Http.ContentType.Xml; } }
        public Encoding Encoding { get { return Encoding.UTF8; } }

        public bool CanParse(MediaType format)
        {
            return format != null && format.Matches(ContentType, Encoding);
        }

        public bool SuitableForReponse(MediaType component)
        {
            return component.Type == "*"
                   || (string.Equals(component.Type, "text", StringComparison.OrdinalIgnoreCase)
                       && (component.Subtype == "*" 
                           || string.Equals(component.Subtype, "xml", StringComparison.OrdinalIgnoreCase)));
        }

        public T From<T>(string text)
        {
            if (string.IsNullOrEmpty(text))
                return default(T);

            try
            {
                using (var reader = new StringReader(text))
                {
                    return (T) new XmlSerializer(typeof (T)).Deserialize(reader);
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

            try
            {
                using (var memory = new MemoryStream())
                using (var writer = new XmlTextWriter(memory, UTF8))
                {
                    var serializable = value as IXmlSerializable;
                    if (serializable != null)
                    {
                        writer.WriteStartDocument();
                        serializable.WriteXml(writer);
                        writer.WriteEndDocument();
                    }
                    else
                    {
                        new XmlSerializer(typeof (T)).Serialize(writer, value);
                    }

                    writer.Flush();
                    return UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length);
                }
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error serializing object of type {0}", value.GetType().FullName);
                return null;
            }
        }
    }
}