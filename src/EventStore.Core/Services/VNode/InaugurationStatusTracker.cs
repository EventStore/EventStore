using EventStore.Core.Telemetry;

namespace EventStore.Core.Services.VNode {
	public interface IInaugurationStatusTracker {
		void OnStateChange(InaugurationManager.ManagerState newState);
	}

	public class InaugurationStatusTracker : IInaugurationStatusTracker {
		private readonly StatusSubMetric _subMetric;

		public InaugurationStatusTracker(StatusMetric metric) {
			_subMetric = new StatusSubMetric("Inauguration", InaugurationManager.ManagerState.Idle, metric);
		}

		public void OnStateChange(InaugurationManager.ManagerState newState) =>
			_subMetric.SetStatus(newState.ToString());

		public class NoOp : IInaugurationStatusTracker {
			public void OnStateChange(InaugurationManager.ManagerState newState) {
			}
		}
	}
}
