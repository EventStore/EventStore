using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests {
	public class SpecificationWithDirectoryPerTestFixture {
		protected internal string PathName;

		protected string GetTempFilePath() {
			var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
			return Path.Combine(PathName, string.Format("{0}-{1}", Guid.NewGuid(), typeName));
		}

		protected string GetFilePathFor(string fileName) {
			return Path.Combine(PathName, fileName);
		}

		[OneTimeSetUp]
		public virtual Task TestFixtureSetUp() {
			ThreadPool.SetMinThreads(100, 1000);
			var typeName = GetType().Name.Length > 30 ? GetType().Name.Substring(0, 30) : GetType().Name;
			PathName = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}", Guid.NewGuid(), typeName));
			Directory.CreateDirectory(PathName);

			return Task.CompletedTask;
		}

		[OneTimeTearDown]
		public virtual Task TestFixtureTearDown() {
			//kill whole tree
			try {
				ForceDeleteDirectory(PathName);
			} catch {

			}

			return Task.CompletedTask;
		}

		private static void ForceDeleteDirectory(string path) {
			var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };
			foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories)) {
				info.Attributes = FileAttributes.Normal;
			}

			directory.Delete(true);
		}
	}
}
