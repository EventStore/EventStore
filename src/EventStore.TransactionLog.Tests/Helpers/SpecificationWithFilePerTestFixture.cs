using System;
using System.IO;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests.Helpers {
	public class SpecificationWithFilePerTestFixture {
		protected string Filename;

		[OneTimeSetUp]
		public virtual void TestFixtureSetUp() {
			var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
			Filename = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}", Guid.NewGuid(), typeName));
		}

		[OneTimeTearDown]
		public virtual void TestFixtureTearDown() {
			if (File.Exists(Filename))
				File.Delete(Filename);
		}
	}
}
