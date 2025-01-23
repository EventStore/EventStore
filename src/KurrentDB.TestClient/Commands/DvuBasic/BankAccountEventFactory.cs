// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace KurrentDB.TestClient.Commands.DvuBasic;

internal static class BankAccountEventFactory {
	public static object CreateAccountObject(int version) {
		object accountObject = null;

		var internalCounter = version + 1;

		{
			const int checkpointVersion = 10;

			var checkPointModVersion = internalCounter % checkpointVersion;
			if (checkPointModVersion == 0) {
				int otherCheckPointsCount = internalCounter / checkpointVersion;

				var elementsCount = internalCounter / 2;

				var creditedSum = ComputeSum(20, elementsCount, 20) - ComputeSum(100, otherCheckPointsCount, 100);
				var debitedSum = ComputeSum(10, elementsCount, 20);

				var checkpoint = new AccountCheckPoint(creditedSum, debitedSum);

				accountObject = checkpoint;
			} else {
				var modVersion = internalCounter % 2;
				if (modVersion == 0) {
					var credited = new AccountCredited(internalCounter * 10, internalCounter % 17);
					accountObject = credited;
				} else {
					var debited = new AccountDebited(internalCounter * 10, internalCounter % 17);
					accountObject = debited;
				}
			}
		}
		return accountObject;
	}

	private static int ComputeSum(int first, int count, int step) {
		var sum = count * (2 * first + step * (count - 1)) / 2;
		return sum;
	}
}
