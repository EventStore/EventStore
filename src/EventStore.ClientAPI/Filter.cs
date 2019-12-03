using System.Text.RegularExpressions;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI {
	public class Filter {
		internal Filter(ClientMessage.Filter.FilterContext context, ClientMessage.Filter.FilterType type,
			string[] data) =>
			Value = new ClientMessage.Filter(context, type, data);

		internal ClientMessage.Filter Value { get; }

		public static FilterContext EventType => new FilterContext(ClientMessage.Filter.FilterContext.EventType);
		public static FilterContext StreamId => new FilterContext(ClientMessage.Filter.FilterContext.StreamId);

		public static Filter ExcludeSystemEvents => new Filter(ClientMessage.Filter.FilterContext.EventType,
			ClientMessage.Filter.FilterType.Regex, new[] {@"^[^\$].*"});

		public override string ToString() {
			return $"{Value.Context}/{Value.Type}/{string.Join(",", Value.Data)}";
		}                                                                                                                                                                                                                                                                                                                                                                                                       
	}

	public class FilterContext {
		private readonly ClientMessage.Filter.FilterContext _context;

		internal FilterContext(ClientMessage.Filter.FilterContext context) =>
			_context = context;

		public Filter Regex(Regex regex) =>
			new Filter(_context, ClientMessage.Filter.FilterType.Regex, new[] {regex.ToString()});


		public Filter Prefix(params string[] prefixes) =>
			new Filter(_context, ClientMessage.Filter.FilterType.Prefix, prefixes);
	}
}
