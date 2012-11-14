using System;
using EventStore.Transport.Http;

namespace EventStore.Core.Services.Transport.Http.Codecs
{
    public class TextCodec : ICodec
    {
        public string ContentType { get { return EventStore.Transport.Http.ContentType.PlainText; } }

        public bool CanParse(string format)
        {
            return string.Equals(ContentType, format, StringComparison.OrdinalIgnoreCase);
        }

        public bool SuitableForReponse(AcceptComponent component)
        {
            return component.MediaType == "*"
                   || (string.Equals(component.MediaType, "text", StringComparison.OrdinalIgnoreCase)
                       && (component.MediaSubtype == "*"
                           || string.Equals(component.MediaSubtype, "plain", StringComparison.OrdinalIgnoreCase)));
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