using System;
using System.Linq;
using System.Xml.Linq;

namespace EventStore.Core.Tests.Http.Streams {
	internal static class XDocumentAtomExtensions {
		internal static readonly XNamespace AtomNamespace = "http://www.w3.org/2005/Atom";

		public static XElement[] GetEntries(this XDocument self) {
			var feed = self.Element(AtomNamespace + "feed");
			if (feed == null)
				return new XElement[0];

			return feed.Elements(AtomNamespace + "entry").ToArray();
		}

		public static XElement GetEntry(this XDocument self) {
			return self.Element(AtomNamespace + "entry");
		}

		public static string GetLink(this XElement self, string rel) {
			var matchingLinks = self.Elements(AtomNamespace + "link")
				.Where(e => e.Attribute("rel").Value == rel)
				.ToArray();

			if (matchingLinks.Length > 1)
				throw new ArgumentException("Multiple links found with rel '" + rel + "'", rel);
			if (matchingLinks.Length == 0)
				throw new ArgumentException("No link found with rel '" + rel + "'", rel);

			return matchingLinks[0].Attribute("href").Value;
		}
	}
}
