using System;
using EventStore.Common.Utils;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV3
{
	public class EventTypeIndexSystemTypesDecorator : INameIndex<uint> {
		private readonly INameIndex<uint> _wrapped;

		public EventTypeIndexSystemTypesDecorator(INameIndex<UInt32> wrapped) {
			_wrapped = wrapped;
		}
		
		public void CancelReservations() {
			_wrapped.CancelReservations();
		}

		public bool GetOrReserve(string eventType, out UInt32 eventTypeId, out UInt32 createdId, out string createdName) {
			Ensure.NotNull(eventType, "eventType");

			if (LogV3SystemEventTypes.TryGetSystemEventTypeId(eventType, out eventTypeId)) {
				createdId = default;
				createdName = default;
				return true;
			}

			return _wrapped.GetOrReserve(eventType, out eventTypeId, out createdId, out createdName);
		}
	}
}
