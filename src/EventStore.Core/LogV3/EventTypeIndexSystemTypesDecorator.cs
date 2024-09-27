// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common.Utils;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV3 {
	public class EventTypeIndexSystemTypesDecorator : INameIndex<uint> {
		private readonly INameIndex<uint> _wrapped;

		public EventTypeIndexSystemTypesDecorator(INameIndex<uint> wrapped) {
			_wrapped = wrapped;
		}

		public void CancelReservations() {
			_wrapped.CancelReservations();
		}

		public bool GetOrReserve(string eventType, out uint eventTypeId, out uint createdId, out string createdName) {
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
