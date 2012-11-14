using System;
using EventStore.Transport.Http;

namespace EventStore.Core.Services.Transport.Http.Codecs
{
    public class NoCodec : ICodec
    {
        public string ContentType { get { throw new NotSupportedException(); } }

        public bool CanParse(string format)
        {
            return false;
        }

        public bool SuitableForReponse(AcceptComponent component)
        {
            return false;
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
}