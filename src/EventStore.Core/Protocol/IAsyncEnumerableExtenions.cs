using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Protocol.Infrastructure.Serialization;

namespace EventStore.Core.Protocol;

public static partial class IAsyncEnumerableExtenions {
	public static async Task ToChannelAsync<T>(
		this IAsyncEnumerable<T> self,
		ChannelWriter<T> writer,
		CancellationToken ct) {

		await foreach (var x in self.WithCancellation(ct)) {
			await writer.WriteAsync(x, ct);
		}
	}

	public static IAsyncEnumerable<Message> DeserializeWithDefault(
		this IAsyncEnumerable<Event> self,
		ISerializer serializer,
		Message unknown) =>

		self.Select(x => {
			if (!serializer.TryDeserialize(x, out var message, out _)) {
				message = unknown;
			}
			return message!;
		});

	public static IAsyncEnumerable<Message> DeserializeSkippingUnknown(
		this IAsyncEnumerable<Event> self,
		ISerializer serializer) =>

		self
			.Select(x => serializer.TryDeserialize(x, out var message, out _)
				? message
				: null)
			.Where(x => x is not null)
			.Select(x => x!);

	public static async IAsyncEnumerable<T> HandleStreamNotFound<T>(
		this IAsyncEnumerable<T> self) {

		await Task.Yield();

		await using var enumerator = self.GetAsyncEnumerator();
		while (true) {
			try {
				if (!await enumerator.MoveNextAsync())
					break;
			} catch (ResponseException.StreamNotFound) {
				break;
			}

			yield return enumerator.Current;
		}
	}
}
