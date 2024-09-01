fromStreams(["website-enquiries", "email-enquiries"]).when({
	$any: function (state, event) {
		linkTo(`customer-${event.data.customerId}`, event, {
			linkOrigin: "enquiry-link-projection",
		});
	},
});
