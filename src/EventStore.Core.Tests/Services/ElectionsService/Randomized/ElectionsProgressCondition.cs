using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Infrastructure;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized {
	internal class ElectionsProgressCondition : IRandTestFinishCondition {
		public bool Done { get; protected set; }

		public bool Success {
			get {
				var byView = ElectionsResults.Values.GroupBy(x => x.Item1)
					.Select(x => Tuple.Create(x.Count(), x.Select(y => y.Item2).Distinct().Count()))
					.ToArray();
				return byView.Count(x => x.Item1 >= _majorityCount && x.Item2 == 1) == 1;
			}
		}

		private readonly int _majorityCount;

		protected readonly Dictionary<IPEndPoint, Tuple<int, IPEndPoint>> ElectionsResults =
			new Dictionary<IPEndPoint, Tuple<int, IPEndPoint>>();

		public ElectionsProgressCondition(int instancesCount) {
			_majorityCount = instancesCount / 2 + 1;
		}

		public virtual void Process(int iteration, RandTestQueueItem item) {
			var msg = item.Message as ElectionMessage.ElectionsDone;
			if (msg != null) {
				var leader = msg.Leader.ExternalHttpEndPoint;
				ElectionsResults[item.EndPoint] = Tuple.Create(msg.InstalledView, leader);
				Done = ElectionsResults.Values.Count(x => x.Item1 == msg.InstalledView) >= _majorityCount;
			}
		}

		public void Log() {
			Console.WriteLine("Success Condition data:");
			Console.WriteLine("node - (installed view, leader)");
			foreach (var electionsResult in ElectionsResults) {
				Console.WriteLine("{0} - ({1}, {2})",
					electionsResult.Key.Port,
					electionsResult.Value.Item1,
					electionsResult.Value.Item2.Port);
			}

			Console.WriteLine("end.");
		}
	}
}
