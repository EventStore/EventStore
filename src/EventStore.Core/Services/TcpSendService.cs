using System.Diagnostics;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Histograms;

namespace EventStore.Core.Services {
	public class TcpSendService : IHandle<TcpMessage.TcpSend> {
		private readonly Stopwatch _watch = Stopwatch.StartNew();
		private const string _tcpSendHistogram = "tcp-send";

		public void Handle(TcpMessage.TcpSend message) {
			var start = _watch.ElapsedTicks;
			message.ConnectionManager.SendMessage(message.Message);
			HistogramService.SetValue(_tcpSendHistogram,
				(long)((((double)_watch.ElapsedTicks - start) / Stopwatch.Frequency) * 1000000000));
		}
	}
}
