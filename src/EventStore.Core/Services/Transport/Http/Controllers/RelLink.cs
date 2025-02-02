// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public class RelLink(string href, string rel) : IXmlSerializable {
	public readonly string href = href;
	public readonly string rel = rel;

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
