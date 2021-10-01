using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests {
	public class SpecificationWithDirectory {
		protected string PathName;

		protected string GetTempFilePath() {
			return Path.Combine(PathName, string.Format("{0}-{1}", Guid.NewGuid(), GetType().FullName));
		}

		protected string GetFilePathFor(string fileName) {
			return Path.Combine(PathName, fileName);
		}

		[SetUp]
		public virtual Task SetUp() {
			var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
			PathName = Path.Combine(Path.GetTempPath(), string.Format("ES/ES-{0}-{1}", typeName, Guid.NewGuid()));
			Directory.CreateDirectory(PathName);

			return Task.CompletedTask;
		}

		[TearDown]
		public virtual async Task TearDown() {
			// kill whole tree
			await DirectoryDeleter.TryForceDeleteDirectoryAsync(PathName, retries: 10);
		}
	}
}
