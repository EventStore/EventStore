using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core;

namespace EventStore.TestClient {
	public class TestClientHostedService : EventStoreHostedService<ClientOptions> {
		private Client _client;

		private readonly CancellationTokenSource _stopped;

		private readonly TaskCompletionSource<int> _exitCode;
		public Task<int> Exited => _exitCode.Task;

		public TestClientHostedService(string[] args) : base(args) {
			_exitCode = new TaskCompletionSource<int>();
			_stopped = new CancellationTokenSource();
			_stopped.Token.Register(() => _exitCode.TrySetResult(0));
		}

		protected override string GetLogsDirectory(ClientOptions options) =>
			options.Log.IsNotEmptyString() ? options.Log : Helper.GetDefaultLogsDir();

		protected override string GetComponentName(ClientOptions options) => "client";

		protected override void Create(ClientOptions options) => _client = new Client(options);

		protected override Task StartInternalAsync(CancellationToken cancellationToken) {
			cancellationToken.Register(_stopped.Cancel);
			Task.Run(() => _exitCode.SetResult(_client.Run()), _stopped.Token);
			return Task.CompletedTask;
		}

		protected override Task StopInternalAsync(CancellationToken cancellationToken) {
			_stopped.Cancel();
			return Task.CompletedTask;
		}
	}
}
