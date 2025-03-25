// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using Confluent.Kafka;

namespace Kurrent.Connectors.Kafka;

static class KafkaExceptionExtensions {
	public static bool IsTransient(this KafkaException exception) =>
		exception.Error.IsTransient() || exception.IsRetryable();

	static bool IsRetryable(this KafkaException exception) => exception is KafkaRetriableException;

	static bool IsTransient(this Error error) =>
		!error.IsUseless() && !error.IsTerminal() && error.Code
			is ErrorCode.Local_NoOffset
			or ErrorCode.Local_QueueFull
			or ErrorCode.Local_AllBrokersDown
			or ErrorCode.OutOfOrderSequenceNumber
			or ErrorCode.TransactionCoordinatorFenced
			or ErrorCode.UnknownProducerId;

	static bool IsUseless(this Error error) => error.Code is ErrorCode.NoError;

	static bool IsTerminal(this Error error) =>
		error.IsFatal || error.Code
			is ErrorCode.TopicException
			or ErrorCode.Local_KeySerialization
			or ErrorCode.Local_ValueSerialization
			or ErrorCode.Local_KeyDeserialization
			or ErrorCode.Local_ValueDeserialization
			or ErrorCode.OffsetOutOfRange
			or ErrorCode.OffsetMetadataTooLarge
			or ErrorCode.ClusterAuthorizationFailed
			or ErrorCode.TopicAuthorizationFailed
			or ErrorCode.GroupAuthorizationFailed
			or ErrorCode.UnsupportedSaslMechanism
			or ErrorCode.SecurityDisabled
			or ErrorCode.SaslAuthenticationFailed;
}
