using System;
using System.IO;
using NUnit.Framework;

namespace EventStore.Core.Tests {
	public class SpecificationWithFile {
		protected string Filename;

		[SetUp]
		public virtual void SetUp() {
			var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
			Filename = Path.Combine(Path.GetTempPath(), string.Format("ES/ES-{0}-{1}", typeName, Guid.NewGuid()));
		}

		[TearDown]
		public virtual void TearDown() {
			if (File.Exists(Filename))
				File.Delete(Filename);
		}
	}
}
