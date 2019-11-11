using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Grpc.Projections {
	internal static class StandardProjections {
		public static IEnumerable<string> Names = typeof(ProjectionNamesBuilder.StandardProjections).GetFields(
				System.Reflection.BindingFlags.Public |
				System.Reflection.BindingFlags.Static |
				System.Reflection.BindingFlags.FlattenHierarchy)
			.Where(x => x.IsLiteral && !x.IsInitOnly)
			.Select(x => x.GetRawConstantValue().ToString());

		public static Task Created(ISubscriber bus) {
			var systemProjectionsReady = Names.ToDictionary(x => x, _ => new TaskCompletionSource<bool>());

			bus.Subscribe(new AdHocHandler<CoreProjectionStatusMessage.Stopped>(m => {
				if (!systemProjectionsReady.TryGetValue(m.Name, out var ready)) return;
				ready.TrySetResult(true);
			}));

			return Task.WhenAll(systemProjectionsReady.Values.Select(x => x.Task));
		}
	}
}
