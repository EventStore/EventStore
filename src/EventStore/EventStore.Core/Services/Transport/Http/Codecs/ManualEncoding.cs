using System;
using EventStore.Transport.Http;

namespace EventStore.Core.Services.Transport.Http.Codecs
{
    public class ManualEncoding : ICodec
    {
        public string ContentType { get { throw new InvalidOperationException(); } }

        public bool CanParse(string format)
        {
            return true;
        }

        public bool SuitableForReponse(AcceptComponent component)
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
}