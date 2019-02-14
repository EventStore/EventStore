using System.Text;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Codecs {
	public static class Codec {
		public static readonly NoCodec NoCodec = new NoCodec();
		public static readonly ICodec[] NoCodecs = new ICodec[0];
		public static readonly ManualEncoding ManualEncoding = new ManualEncoding();

		public static readonly JsonCodec Json = new JsonCodec();
		public static readonly XmlCodec Xml = new XmlCodec();

		public static readonly CustomCodec ApplicationXml =
			new CustomCodec(Xml, ContentType.ApplicationXml, Helper.UTF8NoBom, false, false);

		public static readonly CustomCodec EventXml =
			new CustomCodec(Xml, ContentType.EventXml, Helper.UTF8NoBom, true, true);

		public static readonly CustomCodec EventJson =
			new CustomCodec(Json, ContentType.EventJson, Helper.UTF8NoBom, true, true);

		public static readonly CustomCodec EventsXml =
			new CustomCodec(Xml, ContentType.EventsXml, Helper.UTF8NoBom, true, true);

		public static readonly CustomCodec EventsJson =
			new CustomCodec(Json, ContentType.EventsJson, Helper.UTF8NoBom, true, true);

		public static readonly ICodec EventStoreXmlCodec =
			Codec.CreateCustom(Codec.Xml, ContentType.Atom, Helper.UTF8NoBom, false, false);

		public static readonly ICodec EventStoreJsonCodec =
			Codec.CreateCustom(Codec.Json, ContentType.AtomJson, Helper.UTF8NoBom, false, false);

		public static readonly CustomCodec DescriptionJson =
			new CustomCodec(Json, ContentType.DescriptionDocJson, Helper.UTF8NoBom, true, true);

		public static readonly CustomCodec CompetingXml =
			new CustomCodec(Xml, ContentType.Competing, Helper.UTF8NoBom, true, true);

		public static readonly CustomCodec CompetingJson =
			new CustomCodec(Json, ContentType.CompetingJson, Helper.UTF8NoBom, true, true);

		public static readonly CustomCodec Raw = new CustomCodec(NoCodec, ContentType.Raw, null, false, false);
		public static readonly TextCodec Text = new TextCodec();

		public static ICodec CreateCustom(ICodec codec, string contentType, Encoding encoding, bool hasEventIds,
			bool hasEventTypes) {
			return new CustomCodec(codec, contentType, encoding, hasEventIds, hasEventTypes);
		}
	}
}
