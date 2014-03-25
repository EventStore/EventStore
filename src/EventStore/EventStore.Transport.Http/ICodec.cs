using System.Text;

namespace EventStore.Transport.Http
{
    public interface ICodec
    {
        string ContentType { get; }
        Encoding Encoding { get; }
        bool CanParse(MediaType format);
        bool SuitableForResponse(MediaType component);

        T From<T>(string text);
        string To<T>(T value);
    }
}