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
    public class ServiceDocument : IXmlSerializable
    {
        public List<WorkspaceElement> Workspaces { get; set; }

        public ServiceDocument()
        {
            Workspaces = new List<WorkspaceElement>();
        }

        public void AddWorkspace(WorkspaceElement workspace)
        {
            Ensure.NotNull(workspace, "workspace");
            Workspaces.Add(workspace);
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
            if (Workspaces.Count == 0)
                ThrowHelper.ThrowSpecificationViolation("An app:service element MUST contain one or more app:workspace elements.");

            writer.WriteStartElement("service", AtomSpecs.AtomPubV1Namespace);
            writer.WriteAttributeString("xmlns", "atom", null, AtomSpecs.AtomV1Namespace);
            Workspaces.ForEach(w => w.WriteXml(writer));

            writer.WriteEndElement();
        }
    }

    public class WorkspaceElement : IXmlSerializable
    {
        public string Title { get; set; }
        public List<CollectionElement> Collections { get; set; }

        public WorkspaceElement()
        {
            Collections = new List<CollectionElement>();
        }

        public void SetTitle(string title)
        {
            Ensure.NotNull(title, "title");
            Title = title;
        }

        public void AddCollection(CollectionElement collection)
        {
            Ensure.NotNull(collection, "collection");
            Collections.Add(collection);
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
                ThrowHelper.ThrowSpecificationViolation("The app:workspace element MUST contain one 'atom:title' element");

            writer.WriteStartElement("workspace");

            writer.WriteElementString("atom", "title", AtomSpecs.AtomV1Namespace, Title);
            Collections.ForEach(c => c.WriteXml(writer));

            writer.WriteEndElement();
        }
    }

    public class CollectionElement : IXmlSerializable
    {
        public string Title { get; set; }
        public string Uri { get; set; }

        public List<AcceptElement> Accepts { get; set; }

        public CollectionElement()
        {
            Accepts = new List<AcceptElement>();
        }

        public void SetTitle(string title)
        {
            Ensure.NotNull(title, "title");
            Title = title;
        }

        public void SetUri(string uri)
        {
            Ensure.NotNull(uri, "uri");
            Uri = uri;
        }

        public void AddAcceptType(string type)
        {
            Ensure.NotNull(type, "type");
            Accepts.Add(new AcceptElement(type));
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
                ThrowHelper.ThrowSpecificationViolation("The app: collection element MUST contain one atom:title element.");
            if (string.IsNullOrEmpty(Uri))
                ThrowHelper.ThrowSpecificationViolation("The app:collection element MUST contain an 'href' attribute, " +
                                                        "whose value gives the IRI of the Collection.");

            writer.WriteStartElement("collection");
            writer.WriteAttributeString("href", Uri);
            writer.WriteElementString("atom", "title", AtomSpecs.AtomV1Namespace, Title);
            Accepts.ForEach(a => a.WriteXml(writer));

            writer.WriteEndElement();
        }
    }

    public class AcceptElement : IXmlSerializable
    {
        public string Type { get; set; }

        public AcceptElement(string type)
        {
            Type = type;
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
            if (string.IsNullOrEmpty(Type))
                ThrowHelper.ThrowSpecificationViolation("atom:accept element MUST contain value");
            writer.WriteElementString("accept", Type);
        }
    }
}