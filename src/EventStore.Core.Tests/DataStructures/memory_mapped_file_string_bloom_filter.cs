using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter;
using NUnit.Framework;

namespace EventStore.Core.Tests.DataStructures {
	public class memory_mapped_file_string_bloom_filter : SpecificationWithDirectoryPerTestFixture {
		private static string GenerateCharset() {
			var charset = "";
			for (var c = 'a'; c <= 'z'; c++) {
				charset += c;
			}
			for (var c = 'A'; c <= 'Z'; c++) {
				charset += c;
			}
			for (var c = '0'; c <= '9'; c++) {
				charset += c;
			}
			charset += "!@#$%^&*()-_";
			return charset;
		}

		private static string GenerateRandomString(int length, string charset, Random random) {
			string s = "";
			for (var j = 0; j < length; j++) {
				s += charset[random.Next() % charset.Length];
			}

			return s;
		}

		private static string[] GenerateRandomStrings(int count, int maxLength) {
			var random = new Random();
			var strings = new List<string>();
			var charset = GenerateCharset();

			for (int i = 0; i < count; i++) {
				int length = 1 + random.Next() % maxLength;
				strings.Add(GenerateRandomString(length, charset, random));
			}

			return strings.ToArray();
		}

		[TestFixture]
		private class with_fixed_size_filter : memory_mapped_file_string_bloom_filter {
			private MemoryMappedFileStringBloomFilter _filter;
			private string _path;

			[SetUp]
			public void SetUp() {
				_path = GetTempFilePath();
				_filter = new MemoryMappedFileStringBloomFilter(_path, MemoryMappedFileBloomFilter.MinSizeKB * 1000, 1, 1);
			}

			[TearDown]
			public void Teardown() => _filter?.Dispose();

			[Test]
			public void creates_the_file_on_disk() => Assert.That(File.Exists(_path));

			[Test]
			public void can_close_and_reopen() {
				_filter.Add("hello");
				_filter.Dispose();
				using var newFilter = new MemoryMappedFileStringBloomFilter(_path, MemoryMappedFileBloomFilter.MinSizeKB * 1000, 1, 1);
				Assert.IsTrue(newFilter.MayExist("hello"));
			}

			[Test]
			public void creates_correct_header() {
				_filter.Dispose();
				using var fileStream = File.Open(_path, FileMode.Open);
				var binaryReader = new BinaryReader(fileStream);

				var version = binaryReader.ReadByte();
				var numBits = binaryReader.ReadInt64();
				Assert.AreEqual( 0x01, version);
				Assert.AreEqual( MemoryMappedFileBloomFilter.MinSizeKB * 1000 * 8, numBits);
			}

			[Test]
			public void supports_adding_long_strings() {
				var charset = GenerateCharset();
				var random = new Random();
				var longString = GenerateRandomString(10000, charset, random);

				Assert.IsFalse(_filter.MayExist(longString));
				_filter.Add(longString);
				Assert.IsTrue(_filter.MayExist(longString));
			}

			[Test]
			public void has_false_positives_with_probability_p() {
				var n = (int) _filter.OptimalMaxItems;
				var p = _filter.FalsePositiveProbability;

				var random = new Random();
				var charset = GenerateCharset();

				var list = new List<string>();

				//generate 2n items
				for (int i = 0; i < 2 * n; i++) {
					var length = 1 + random.Next() % 10;
					list.Add(GenerateRandomString(length, charset, random));
				}

				//add n items to the filter
				for (int i = 0; i < n; i++) {
					_filter.Add(list[i]);
				}

				//expected number of false positives
				var expectedFalsePositives = Convert.ToInt32(Math.Ceiling(n * p));

				//none of these items should exist but there may be some false positives
				var falsePositives = 0;
				for (var i = n ; i < 2*n; i ++) {
					if (_filter.MayExist(list[i])) {
						falsePositives++;
					}
				}

				if (falsePositives > 0)
					Console.Out.WriteLine("n: {0}, p:{1}. Found {2} false positives. Expected false positives: {3}",
						n, p, falsePositives, expectedFalsePositives);

				Assert.LessOrEqual(falsePositives, expectedFalsePositives);
			}
		}

		[Test, Category("LongRunning")]
		public void always_returns_true_when_an_item_was_added([Range(10_000, 100_000, 13337)] long size) {
			using var filter = new MemoryMappedFileStringBloomFilter(GetTempFilePath(), size, 1, 1);
			var strings = GenerateRandomStrings((int)filter.OptimalMaxItems, 100);

			//no items added yet
			foreach (var s in strings) {
				Assert.IsFalse(filter.MayExist(s));
			}

			//add the items and verify their existence
			foreach (var s in strings) {
				filter.Add(s);
				Assert.IsTrue(filter.MayExist(s));
			}

			//all the items should exist
			foreach (var s in strings) {
				Assert.IsTrue(filter.MayExist(s));
			}
		}

		[Test]
		public void throws_argument_out_of_range_exception_when_given_negative_size() {
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new MemoryMappedFileStringBloomFilter(GetTempFilePath(), -1, 1, 1));
		}

		[Test]
		public void throws_argument_out_of_range_exception_when_given_zero_size() {
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new MemoryMappedFileStringBloomFilter(GetTempFilePath(), 0, 1, 1));
		}

		[Test]
		public void throws_argument_out_of_range_exception_when_size_less_than_min_size() {
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new MemoryMappedFileStringBloomFilter(GetTempFilePath(), MemoryMappedFileBloomFilter.MinSizeKB * 1000 - 1, 1, 1));
		}

		[Test]
		public void throws_argument_out_of_range_exception_when_size_greater_than_max_size() {
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new MemoryMappedFileStringBloomFilter(GetTempFilePath(), MemoryMappedFileBloomFilter.MaxSizeKB * 1000 + 1, 1, 1));
		}
	}
}
