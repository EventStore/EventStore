using BenchmarkDotNet.Running;

namespace EventStore.Benchmarks {
	internal class Program {
		private static void Main(string[] args) {
			BenchmarkRunner.Run(typeof(Program).Assembly);
		}
	}
}
