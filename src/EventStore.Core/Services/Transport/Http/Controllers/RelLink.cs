using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public class RelLink : IXmlSerializable {
		public readonly string href;
		public readonly string rel;

		private RelLink() {
		}

		public RelLink(string href, string rel) {
			this.href = href;
			this.rel = rel;
		}

		public XmlSchema GetSchema() {
			return null;
		}

		public void ReadXml(XmlReader reader) {
			throw new NotImplementedException("Rel links not deserialized.");
		}

		public void WriteXml(XmlWriter writer) {
			if (string.IsNullOrEmpty(href))
				throw new Exception("null href when serializing a rel link");

			writer.WriteStartElement("link");
			writer.WriteAttributeString("href", href);

			if (rel != null)
				writer.WriteAttributeString("rel", rel);
			writer.WriteEndElement();
		}
	}
}
