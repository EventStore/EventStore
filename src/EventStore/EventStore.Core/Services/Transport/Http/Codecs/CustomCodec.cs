using System;
using EventStore.Common.Utils;
using EventStore.Transport.Http;

namespace EventStore.Core.Services.Transport.Http.Codecs
{
    public class CustomCodec : ICodec
    {
        public ICodec BaseCodec { get { return _codec; } }
        public string ContentType { get { return _contentType; } }

        private readonly ICodec _codec;
        private readonly string _contentType;
        private readonly string _type;
        private readonly string _subtype;

        internal CustomCodec(ICodec codec, string contentType)
        {
            Ensure.NotNull(codec, "codec");
            Ensure.NotNull(contentType, "contentType");

            _codec = codec;
            _contentType = contentType;
            var parts = contentType.Split(new[] {'/'}, 2);
            if (parts.Length != 2)
                throw new ArgumentException("contentType");
            _type = parts[0];
            _subtype = parts[1];
        }

        public bool CanParse(string format)
        {
            return string.Equals(format, _contentType, StringComparison.OrdinalIgnoreCase);
        }

        public bool SuitableForReponse(AcceptComponent component)
        {
            return component.MediaType == "*"
                   || (string.Equals(component.MediaType, _type, StringComparison.OrdinalIgnoreCase)
                       && (component.MediaSubtype == "*"
                           || string.Equals(component.MediaSubtype, _subtype, StringComparison.OrdinalIgnoreCase)));
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
}