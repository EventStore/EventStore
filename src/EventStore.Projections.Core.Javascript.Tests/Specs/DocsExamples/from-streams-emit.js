fromStreams(["website-enquiries", "email-enquiries"]).when({
	$any: function (state, event) {
		// Track the origin of the enquiry on the emitted event
		const enquiryOrigin =
			event.streamId === "website-enquiries" ? "Website" : "Email";

		const emittedEventData = {
			...event.data,
			enquiryOrigin,
		};

		emit(
			`customer-${event.data.customerId}`,
			event.eventType,
			emittedEventData,
			event.metadata
		);
	},
});
