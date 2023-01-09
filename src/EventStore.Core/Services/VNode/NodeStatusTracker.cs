using EventStore.Core.Data;
using EventStore.Core.Telemetry;

namespace EventStore.Core.Services.VNode {
	public interface INodeStatusTracker {
		void OnStateChange(VNodeState newState);
	}

	public class NodeStatusTracker : INodeStatusTracker {
		private readonly StatusSubMetric _subMetric;

		public NodeStatusTracker(StatusMetric metric) {
			_subMetric = new StatusSubMetric("Node", VNodeState.Initializing, metric);
		}

		public void OnStateChange(VNodeState newState) =>
			_subMetric.SetStatus(newState.ToString());

		public class NoOp : INodeStatusTracker {
			public void OnStateChange(VNodeState newState) {
			}
		}
	}
}
