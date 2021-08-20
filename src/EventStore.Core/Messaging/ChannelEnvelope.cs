using System.Threading;
using System.Threading.Channels;

namespace EventStore.Core.Messaging {
	public class ChannelEnvelope : IEnvelope {
		private readonly ChannelWriter<Message> _channelWriter;

		public ChannelEnvelope(ChannelWriter<Message> channelWriter) => _channelWriter = channelWriter;

		public void ReplyWith<T>(T message) where T : Message =>
			SpinWait.SpinUntil(() => _channelWriter.TryWrite(message));
	}
}
