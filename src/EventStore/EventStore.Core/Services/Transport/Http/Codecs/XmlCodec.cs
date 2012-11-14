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

        public bool CanParse(string format)
        {
            return string.Equals(ContentType, format, StringComparison.OrdinalIgnoreCase);
        }

        public bool SuitableForReponse(AcceptComponent component)
        {
            return component.MediaType == "*"
                   || (string.Equals(component.MediaType, "text", StringComparison.OrdinalIgnoreCase)
                       && (component.MediaSubtype == "*" 
                           || string.Equals(component.MediaSubtype, "xml", StringComparison.OrdinalIgnoreCase)));
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