﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Index;
using EventStore.Core.Tests.Services.Storage.Transactions;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.AutoMergeLevelTests
{
	[TestFixture]
	public class when_max_auto_merge_level_is_set : SpecificationWithDirectoryPerTestFixture
	{
		private readonly int _maxAutoMergeLevel;
		protected  string _filename;
		protected  IndexMap _map;
		protected  byte _ptableVersion = 4;
		protected MergeResult _result;
		protected  bool _skipIndexVerify = true;
		protected  GuidFilenameProvider _fileNameProvider;

		public when_max_auto_merge_level_is_set(int maxAutoMergeLevel = 2)
		{
			_maxAutoMergeLevel = maxAutoMergeLevel;
		}
		[OneTimeSetUp]
		public void Setup()
		{
			_filename = GetTempFilePath();
			_fileNameProvider = new GuidFilenameProvider(PathName);
			_map = IndexMap.FromFile(_filename, maxTablesPerLevel: 2, maxAutoMergeLevel:_maxAutoMergeLevel);
			
		}

		protected void AddTables(int count)
		{
			var memtable = new HashListMemTable(_ptableVersion, maxSize: 10);
			memtable.Add(0, 1, 0);
			var first = _map;
			if (_result != null)
				first = _result.MergedMap;
			_result = first.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath(),skipIndexVerify:_skipIndexVerify),
				10, 20, UpgradeHash, ExistsAt, RecordExistsAt, _fileNameProvider, _ptableVersion,
				0, 0,skipIndexVerify:_skipIndexVerify);
			for (int i = 3; i <= count * 2; i+=2)
			{
				_result = _result.MergedMap.AddPTable(
					PTable.FromMemtable(memtable, GetTempFilePath(), skipIndexVerify: _skipIndexVerify),
					i*10, (i+1)*10, (streamId, hash) => hash, _ => true, _ => new Tuple<string, bool>("", true),
					_fileNameProvider, _ptableVersion, 0,0, skipIndexVerify: _skipIndexVerify);
				_result.ToDelete.ForEach(x => x.MarkForDestruction());
				
			}
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown()
		{
			_result.ToDelete.ForEach(x=>x.MarkForDestruction());
			_result.MergedMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
			_result.MergedMap.Dispose(TimeSpan.FromMilliseconds(100));
			_map.Dispose(TimeSpan.FromMilliseconds(100));
			File.Delete(_filename);

			base.TestFixtureTearDown();
		}

		protected Tuple<string, bool> RecordExistsAt(IndexEntry arg)
		{
			return Tuple.Create("", true);
		}

		protected bool ExistsAt(IndexEntry arg)
		{
			return true;
		}

		protected static ulong UpgradeHash(string stream, ulong hash)
		{
			return hash;
		}
	}
}
