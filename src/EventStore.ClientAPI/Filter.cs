using System.Text.RegularExpressions;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI {
	/// <summary>
	/// A Filter, used to filter events when reading from the $all stream.
	/// </summary>
	public class Filter {
		internal Filter(ClientMessage.Filter.FilterContext context, ClientMessage.Filter.FilterType type,
			string[] data) =>
			Value = new ClientMessage.Filter(context, type, data);

		internal ClientMessage.Filter Value { get; }

		/// <summary>
		/// Filters by EventType.
		/// </summary>
		public static FilterContext EventType => new FilterContext(ClientMessage.Filter.FilterContext.EventType);
		/// <summary>
		/// Filters by StreamId.
		/// </summary>
		public static FilterContext StreamId => new FilterContext(ClientMessage.Filter.FilterContext.StreamId);

		/// <summary>
		/// A <see cref="Filter"/> that excludes all system events.
		/// </summary>
		public static Filter ExcludeSystemEvents => new Filter(ClientMessage.Filter.FilterContext.EventType,
			ClientMessage.Filter.FilterType.Regex, new[] {@"^[^\$].*"});

		/// <inheritdoc />
		public override string ToString() {
			return $"{Value.Context}/{Value.Type}/{string.Join(",", Value.Data)}";
		}                                                                                                                                                                                                                                                                                                                                                                                                       
	}

	/// <summary>
	/// A filter context.
	/// </summary>
	public class FilterContext {
		private readonly ClientMessage.Filter.FilterContext _context;

		internal FilterContext(ClientMessage.Filter.FilterContext context) =>
			_context = context;

		/// <summary>
		/// Filters by <see cref="Regex"/>.
		/// </summary>
		/// <param name="regex"></param>
		/// <returns></returns>
		public Filter Regex(Regex regex) =>
			new Filter(_context, ClientMessage.Filter.FilterType.Regex, new[] {regex.ToString()});


		/// <summary>
		/// Filters by starting-with.
		/// </summary>
		/// <param name="prefixes"></param>
		/// <returns></returns>
		public Filter Prefix(params string[] prefixes) =>
			new Filter(_context, ClientMessage.Filter.FilterType.Prefix, prefixes);
	}
}
