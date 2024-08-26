fromStream("account-d7fa4899-1e7e-41ae-9be3-075f52bbcc3c").when({
	$init: function () {
		return { accountBalance: 0 };
	},
	// When an event of type 'FundsDeposited' is processed, add to the accountBalance
	FundsDeposited: function (state, event) {
		return { accountBalance: state.accountBalance + event.data.amount };
	},
	// When an event of type 'FundsWithdrawn' is processed, subtract from the accountBalance
	FundsWithdrawn: function (state, event) {
		return { accountBalance: state.accountBalance - event.data.amount };
	},
});
