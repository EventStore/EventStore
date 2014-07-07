using EventStore.Common.Yaml.Core.Events;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Common.Yaml.Core
{
	/// <summary>
	/// Simple implementation of <see cref="IParser"/> that implements merging: http://yaml.org/type/merge.html
	/// </summary>
	public sealed class MergingParser : IParser
	{
		private readonly List<ParsingEvent> _allEvents = new List<ParsingEvent>();
		private readonly IParser _innerParser;
		private int _currentIndex = -1;

		public MergingParser(IParser innerParser)
		{
			_innerParser = innerParser;
		}

		public ParsingEvent Current { get; private set; }

		public bool MoveNext()
		{
			if (_currentIndex < 0)
			{
				while (_innerParser.MoveNext())
				{
					_allEvents.Add(_innerParser.Current);
				}

				for (int i = _allEvents.Count - 2; i >= 0; --i)
				{
					var merge = _allEvents[i] as Scalar;
					if (merge != null && merge.Value == "<<")
					{
						var anchorAlias = _allEvents[i + 1] as AnchorAlias;
						if (anchorAlias != null)
						{
							var mergedEvents = GetMappingEvents(anchorAlias.Value);
							_allEvents.RemoveRange(i, 2);
							_allEvents.InsertRange(i, mergedEvents);
						}

					}
				}
			}

			var nextIndex = _currentIndex + 1;
			if (nextIndex < _allEvents.Count)
			{
				Current = _allEvents[nextIndex];
				_currentIndex = nextIndex;
				return true;
			}
			return false;
		}

		private IEnumerable<ParsingEvent> GetMappingEvents(string mappingAlias)
		{
			var nesting = 0;
			return _allEvents
				.SkipWhile(e =>
				{
					var mappingStart = e as MappingStart;
					return mappingStart == null || mappingStart.Anchor != mappingAlias;
				})
				.Skip(1)
				.TakeWhile(e => (nesting += e.NestingIncrease) >= 0)
				.ToList();
		}
	}
}
