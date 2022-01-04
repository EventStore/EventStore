using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture]
	public class versioned_pattern_filenaming_strategy : SpecificationWithDirectory {
		[Test]
		public void when_constructed_with_null_path_should_throws_argumentnullexception() {
			Assert.Throws<ArgumentNullException>(() => new VersionedPatternFileNamingStrategy(null, "prefix"));
		}

		[Test]
		public void when_constructed_with_null_prefix_should_throws_argumentnullexception() {
			Assert.Throws<ArgumentNullException>(() => new VersionedPatternFileNamingStrategy("path", null));
		}

		[Test]
		public void when_getting_file_for_positive_index_and_no_version_appends_index_to_name_with_zero_version() {
			var strategy = new VersionedPatternFileNamingStrategy("path", "prefix-");
			Assert.AreEqual("path" + Path.DirectorySeparatorChar + "prefix-000001.000000",
				strategy.GetFilenameFor(1, 0));
		}

		[Test]
		public void when_getting_file_for_nonnegative_index_and_version_appends_value_and_provided_version() {
			var strategy = new VersionedPatternFileNamingStrategy("path", "prefix-");
			Assert.AreEqual("path" + Path.DirectorySeparatorChar + "prefix-000001.000007",
				strategy.GetFilenameFor(1, 7));
		}

		[Test]
		public void when_getting_file_for_negative_index_throws_argumentoutofrangeexception() {
			var strategy = new VersionedPatternFileNamingStrategy("Path", "prefix");
			Assert.Throws<ArgumentOutOfRangeException>(() => strategy.GetFilenameFor(-1, 0));
		}

		[Test]
		public void when_getting_file_for_negative_version_throws_argumentoutofrangeexception() {
			var strategy = new VersionedPatternFileNamingStrategy("Path", "prefix");
			Assert.Throws<ArgumentOutOfRangeException>(() => strategy.GetFilenameFor(0, -1));
		}

		[Test]
		public void returns_all_existing_versions_of_the_same_chunk_in_descending_order_of_versions() {
			File.Create(GetFilePathFor("foo")).Close();
			File.Create(GetFilePathFor("bla")).Close();

			File.Create(GetFilePathFor("chunk-000001.000000")).Close();
			File.Create(GetFilePathFor("chunk-000002.000000")).Close();
			File.Create(GetFilePathFor("chunk-000003.000000")).Close();

			File.Create(GetFilePathFor("chunk-000005.000000")).Close();
			File.Create(GetFilePathFor("chunk-000005.000007")).Close();
			File.Create(GetFilePathFor("chunk-000005.000002")).Close();
			File.Create(GetFilePathFor("chunk-000005.000005")).Close();

			var strategy = new VersionedPatternFileNamingStrategy(PathName, "chunk-");
			var versions = strategy.GetAllVersionsFor(5);
			Assert.AreEqual(4, versions.Length);
			Assert.AreEqual(GetFilePathFor("chunk-000005.000007"), versions[0]);
			Assert.AreEqual(GetFilePathFor("chunk-000005.000005"), versions[1]);
			Assert.AreEqual(GetFilePathFor("chunk-000005.000002"), versions[2]);
			Assert.AreEqual(GetFilePathFor("chunk-000005.000000"), versions[3]);
		}

		[Test]
		public void returns_all_existing_not_temporary_files_with_correct_pattern() {
			File.Create(GetFilePathFor("foo")).Close();
			File.Create(GetFilePathFor("bla")).Close();
			File.Create(GetFilePathFor("chunk-000001.000000.tmp")).Close();
			File.Create(GetFilePathFor("chunk-001.000")).Close();

			File.Create(GetFilePathFor("chunk-000001.000000")).Close();
			File.Create(GetFilePathFor("chunk-000002.000000")).Close();
			File.Create(GetFilePathFor("chunk-000003.000000")).Close();

			File.Create(GetFilePathFor("chunk-000005.000000")).Close();
			File.Create(GetFilePathFor("chunk-000005.000007")).Close();
			File.Create(GetFilePathFor("chunk-000005.000002")).Close();
			File.Create(GetFilePathFor("chunk-000005.000005")).Close();

			var strategy = new VersionedPatternFileNamingStrategy(PathName, "chunk-");
			var versions = strategy.GetAllPresentFiles();
			Array.Sort(versions, StringComparer.CurrentCultureIgnoreCase);
			Assert.AreEqual(7, versions.Length);
			Assert.AreEqual(GetFilePathFor("chunk-000001.000000"), versions[0]);
			Assert.AreEqual(GetFilePathFor("chunk-000002.000000"), versions[1]);
			Assert.AreEqual(GetFilePathFor("chunk-000003.000000"), versions[2]);
			Assert.AreEqual(GetFilePathFor("chunk-000005.000000"), versions[3]);
			Assert.AreEqual(GetFilePathFor("chunk-000005.000002"), versions[4]);
			Assert.AreEqual(GetFilePathFor("chunk-000005.000005"), versions[5]);
			Assert.AreEqual(GetFilePathFor("chunk-000005.000007"), versions[6]);
		}

		[Test]
		public void returns_all_temp_files_in_directory() {
			File.Create(GetFilePathFor("bla")).Close();
			File.Create(GetFilePathFor("bla.tmp")).Close();
			File.Create(GetFilePathFor("bla.temp")).Close();

			File.Create(GetFilePathFor("chunk-000005.000007.tmp")).Close();

			File.Create(GetFilePathFor("foo.tmp")).Close();

			var strategy = new VersionedPatternFileNamingStrategy(PathName, "chunk-");
			var tempFiles = strategy.GetAllTempFiles();

			Assert.AreEqual(3, tempFiles.Length);
			Assert.Contains(GetFilePathFor("bla.tmp"), tempFiles);
			Assert.Contains(GetFilePathFor("chunk-000005.000007.tmp"), tempFiles);
			Assert.Contains(GetFilePathFor("foo.tmp"), tempFiles);
		}

		[Test]
		public void does_not_return_temp_file_that_is_named_as_chunk() {
			File.Create(GetFilePathFor("chunk-000000.000000.tmp")).Close();

			var strategy = new VersionedPatternFileNamingStrategy(PathName, "chunk-");
			var tempFiles = strategy.GetAllVersionsFor(0);

			Assert.AreEqual(0, tempFiles.Length);
		}

		[Test]
		public void returns_temp_filenames_detectable_by_getalltempfiles_method() {
			var strategy = new VersionedPatternFileNamingStrategy(PathName, "chunk-");
			Assert.AreEqual(0, strategy.GetAllTempFiles().Length);

			var tmp1 = strategy.GetTempFilename();
			var tmp2 = strategy.GetTempFilename();
			File.Create(tmp1).Close();
			File.Create(tmp2).Close();
			var tmp = new[] { tmp1, tmp2 };
			Array.Sort(tmp);

			var tempFiles = strategy.GetAllTempFiles();
			Array.Sort(tempFiles);
			Assert.AreEqual(2, tempFiles.Length);
			Assert.AreEqual(tmp[0], tempFiles[0]);
			Assert.AreEqual(tmp[1], tempFiles[1]);
		}

		[Test]
		public void enumerates_files_with_correct_callback_order() {
			File.Create(GetFilePathFor("foo")).Close(); // should be ignored
			File.Create(GetFilePathFor("bla")).Close(); // should be ignored
			File.Create(GetFilePathFor("chunk-000001.000000.tmp")).Close(); // should be ignored
			File.Create(GetFilePathFor("chunk-001.000")).Close(); // should be ignored

			// chunk 0 is missing
			File.Create(GetFilePathFor("chunk-000001.000000")).Close(); // chunks 1 - 1 (latest)
			File.Create(GetFilePathFor("chunk-000002.000001")).Close(); // chunks 2 - 2 (latest)
			// chunks 3 & 4 are missing
			File.Create(GetFilePathFor("chunk-000005.000000")).Close(); // chunks 5 - 5 (old)
			File.Create(GetFilePathFor("chunk-000005.000001")).Close(); // chunks 5 - 6 (old)
			File.Create(GetFilePathFor("chunk-000005.000002")).Close(); // chunks 5 - 7 (latest)
			File.Create(GetFilePathFor("chunk-000006.000000")).Close(); // chunks 6 - 6 (old)
			// chunk 7 is not missing - it's merged with chunk 5
			File.Create(GetFilePathFor("chunk-000008.000007")).Close(); // chunks 8 - 8 (latest)
			// chunk 9 is missing
			File.Create(GetFilePathFor("chunk-000010.000005")).Close(); // chunks 10 - 10 (latest)

			var strategy = new VersionedPatternFileNamingStrategy(PathName, "chunk-");
			var result = new List<string>();
			int GetNextFileNumber(string chunk) {
				return Path.GetFileName(chunk) switch {
					"chunk-000001.000000" => 2,
					"chunk-000002.000001" => 3,
					"chunk-000005.000000" => 6,
					"chunk-000005.000001" => 7,
					"chunk-000005.000002" => 8,
					"chunk-000006.000000" => 7,
					"chunk-000008.000007" => 9,
					"chunk-000010.000005" => 11,
					_ => throw new Exception($"Unexpected file: {chunk}")
				};
			}

			strategy.EnumerateAllFiles(GetNextFileNumber,
				onLatestVersionFound: (chunk, start, end) => result.Add($"latest {Path.GetFileName(chunk)} {start}-{end}"),
				onOldVersionFound: (chunk, start) => result.Add($"old {Path.GetFileName(chunk)} {start}"),
				onFileMissing: (chunk, start) => result.Add($"missing {Path.GetFileName(chunk)} {start}")
			);

			var expectedResult = new List<string> {
				"missing chunk-000000.000000 0",
				"latest chunk-000001.000000 1-1",
				"latest chunk-000002.000001 2-2",
				"missing chunk-000003.000000 3",
				"missing chunk-000004.000000 4",
				"old chunk-000005.000000 5",
				"old chunk-000005.000001 5",
				"latest chunk-000005.000002 5-7",
				"old chunk-000006.000000 6",
				"latest chunk-000008.000007 8-8",
				"missing chunk-000009.000000 9",
				"latest chunk-000010.000005 10-10"
			};
			Assert.AreEqual(expectedResult, result);
		}
	}
}
