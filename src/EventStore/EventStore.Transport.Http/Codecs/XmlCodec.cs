using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Codecs
{
    public class XmlCodec : ICodec
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<XmlCodec>();

        public string ContentType { get { return EventStore.Transport.Http.ContentType.Xml; } }
        public Encoding Encoding { get { return Helper.UTF8NoBom; } }
        public bool HasEventIds { get { return false; }}
        public bool HasEventTypes { get { return false; }}
        
        public bool CanParse(MediaType format)
        {
            return format != null && format.Matches(ContentType, Encoding);
        }

        public bool SuitableForResponse(MediaType component)
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
                using (var writer = new XmlTextWriter(memory, Helper.UTF8NoBom))
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
                    return Helper.UTF8NoBom.GetString(memory.GetBuffer(), 0, (int)memory.Length);
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