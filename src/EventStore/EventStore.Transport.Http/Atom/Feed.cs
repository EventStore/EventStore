// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Atom
{
    public class FeedElement : IXmlSerializable
    {
        public string Title { get; set; }
        public string Id { get; set; }
        public string Updated { get; set; }
        public PersonElement Author { get; set; }

        public List<LinkElement> Links { get; set; }
        public List<EntryElement> Entries { get; set; }

        public FeedElement()
        {
            Links = new List<LinkElement>();
            Entries = new List<EntryElement>();
        }

        public void SetTitle(string title)
        {
            Ensure.NotNull(title, "title");
            Title = title;
        }

        public void SetId(string id)
        {
            Ensure.NotNull(id, "id");
            Id = id;
        }

        public void SetUpdated(DateTime dateTime)
        {
            Updated = XmlConvert.ToString(dateTime, XmlDateTimeSerializationMode.Utc);
        }

        public void SetAuthor(string name)
        {
            Ensure.NotNull(name, "name");
            Author = new PersonElement(name);
        }

        public void AddLink(string relation, string uri, string contentType = null)
        {
            Ensure.NotNull(uri, "uri");
            Links.Add(new LinkElement(uri, relation, contentType));
        }

        public void AddEntry(EntryElement entry)
        {
            Ensure.NotNull(entry, "entry");
            Entries.Add(entry);
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            throw new NotImplementedException();
        }

        public void WriteXml(XmlWriter writer)
        {
            if (string.IsNullOrEmpty(Title))
                ThrowHelper.ThrowSpecificationViolation("atom:feed elements MUST contain exactly one atom:title element.");
            if (string.IsNullOrEmpty(Id))
                ThrowHelper.ThrowSpecificationViolation("atom:feed elements MUST contain exactly one atom:id element.");
            if (string.IsNullOrEmpty(Updated))
                ThrowHelper.ThrowSpecificationViolation("atom:feed elements MUST contain exactly one atom:updated element.");
            if (Author == null)
                ThrowHelper.ThrowSpecificationViolation("atom:feed elements MUST contain one or more atom:author elements");
            if (Links.Count == 0)
                ThrowHelper.ThrowSpecificationViolation("atom:feed elements SHOULD contain one atom:link element with a " 
                                                        + "rel attribute value of 'self'.This is the preferred URI for retrieving Atom Feed Documents                                                                   representing this Atom feed.");

            writer.WriteStartElement("feed", AtomSpecs.AtomV1Namespace);

            writer.WriteElementString("title", Title);
            writer.WriteElementString("id", Id);
            writer.WriteElementString("updated", Updated);
            Author.WriteXml(writer);

            Links.ForEach(link => link.WriteXml(writer));
            Entries.ForEach(entry => entry.WriteXml(writer));

            writer.WriteEndElement();
        }
    }

    public class EntryElement : IXmlSerializable
    {
        public string Title { get; set; }
        public string Id { get; set; }
        public string Updated { get; set; }
        public PersonElement Author { get; set; }
        public string Summary { get; set; }

        public List<LinkElement> Links { get; set; }

        public EntryElement()
        {
            Links = new List<LinkElement>();
        }

        public void SetTitle(string title)
        {
            Ensure.NotNull(title, "title");
            Title = title;
        }

        public void SetId(string id)
        {
            Ensure.NotNull(id, "id");
            Id = id;
        }

        public void SetUpdated(DateTime dateTime)
        {
            Updated = XmlConvert.ToString(dateTime, XmlDateTimeSerializationMode.Utc);
        }

        public void SetAuthor(string name)
        {
            Ensure.NotNull(name, "name");
            Author = new PersonElement(name);
        }

        public void SetSummary(string summary)
        {
            Ensure.NotNull(summary, "summary");
            Summary = summary;
        }

        public void AddLink(string relation, string uri, string type = null)
        {
            Ensure.NotNull(uri, "uri");
            Links.Add(new LinkElement(uri, relation, type));
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement("entry");

            Title = reader.ReadElementString("title");
            Id = reader.ReadElementString("id");
            Updated = reader.ReadElementString("updated");
            Author.ReadXml(reader);
            Summary = reader.ReadElementString("summary");
            Links.ForEach(l => l.ReadXml(reader));

            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            if (string.IsNullOrEmpty(Title))
                 ThrowHelper.ThrowSpecificationViolation("atom:entry elements MUST contain exactly one atom:title element.");
            if (string.IsNullOrEmpty(Id))
                 ThrowHelper.ThrowSpecificationViolation("atom:entry elements MUST contain exactly one atom:id element.");
            if (string.IsNullOrEmpty(Updated))
                 ThrowHelper.ThrowSpecificationViolation("atom:entry elements MUST contain exactly one atom:updated element.");
            if (Author == null)
                 ThrowHelper.ThrowSpecificationViolation("atom:entry elements MUST contain one or more atom:author elements");
            if (string.IsNullOrEmpty(Summary))
                ThrowHelper.ThrowSpecificationViolation("atom:entry elements MUST contain an atom:summary element");

            writer.WriteStartElement("entry");

            writer.WriteElementString("title", Title);
            writer.WriteElementString("id", Id);
            writer.WriteElementString("updated", Updated);
            Author.WriteXml(writer);
            writer.WriteElementString("summary", Summary);
            Links.ForEach(link => link.WriteXml(writer));

            writer.WriteEndElement();
        }
    }

    public class LinkElement : IXmlSerializable
    {
        public string Uri { get; set; }
        public string Relation { get; set; }
        public string Type { get; set; }

        public LinkElement(string uri) : this(uri, null, null)
        {
        }

        public LinkElement(string uri, string relation):this(uri, relation, null)
        {
        }

        public LinkElement(string uri, string relation, string type)
        {
            Uri = uri;
            Relation = relation;
            Type = type;
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement("link");

            Uri = reader.GetAttribute("href");
            Relation = reader.GetAttribute("rel");
            Type = reader.GetAttribute("type");

            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            if (string.IsNullOrEmpty(Uri))
                ThrowHelper.ThrowSpecificationViolation("atom:link elements MUST have an href attribute, whose value MUST be a IRI reference");

            writer.WriteStartElement("link");
            writer.WriteAttributeString("href", Uri);

            if (Relation != null)
                writer.WriteAttributeString("rel", Relation);
            if (Type != null)
                writer.WriteAttributeString("type", Type);

            writer.WriteEndElement();
        }
    }

    public class PersonElement : IXmlSerializable
    {
        public string Name { get; set; }

        public PersonElement(string name)
        {
            Name = name;
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            reader.ReadStartElement("author");
            Name = reader.ReadElementString("name");
            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            if (string.IsNullOrEmpty(Name))
                ThrowHelper.ThrowSpecificationViolation("Person constructs MUST contain exactly one 'atom:name' element.");

            writer.WriteStartElement("author");
            writer.WriteElementString("name", Name);
            writer.WriteEndElement();
        }
    }
}
