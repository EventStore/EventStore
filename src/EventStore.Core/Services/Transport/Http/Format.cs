// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Transport.Http;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using EventStore.Common.Utils;
using EventStore.Core.Cluster;

namespace EventStore.Core.Services.Transport.Http;

public static class Format {
	public static string TextMessage(HttpResponseFormatterArgs entity, Message message) {
		return message is HttpMessage.TextMessage textMessage ? entity.ResponseCodec.To(textMessage) : string.Empty;
	}

	public static object EventEntry(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed) {
		if (message is not ClientMessage.ReadEventCompleted { Result: ReadEventResult.Success } msg || msg.Record.Event == null)
			return entity.ResponseCodec.To(Empty.Result);

		return entity.ResponseCodec.ContentType switch {
			ContentType.Atom or ContentType.AtomJson or ContentType.Html => entity.ResponseCodec.To(Convert.ToEntry(msg.Record, entity.ResponseUrl, embed, singleEntry: true)),
			_ => AutoEventConverter.SmartFormat(msg.Record, entity.ResponseCodec)
		};
	}

	public static string GetStreamEventsBackward(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed, bool headOfStream) {
		return message is not ClientMessage.ReadStreamEventsBackwardCompleted { Result: ReadStreamResult.Success } msg
			? string.Empty
			: entity.ResponseCodec.To(Convert.ToStreamEventBackwardFeed(msg, entity.ResponseUrl, embed, headOfStream));
	}

	public static string GetStreamEventsForward(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed) {
		return message is not ClientMessage.ReadStreamEventsForwardCompleted { Result: ReadStreamResult.Success } msg
			? string.Empty
			: entity.ResponseCodec.To(Convert.ToStreamEventForwardFeed(msg, entity.ResponseUrl, embed));
	}

	public static string ReadAllEventsBackwardCompleted(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed) {
		return message is not ClientMessage.ReadAllEventsBackwardCompleted { Result: ReadAllResult.Success } msg
			? string.Empty
			: entity.ResponseCodec.To(Convert.ToAllEventsBackwardFeed(msg, entity.ResponseUrl, embed));
	}

	public static string ReadAllEventsBackwardFilteredCompleted(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed) {
		return message is not ClientMessage.FilteredReadAllEventsBackwardCompleted { Result: FilteredReadAllResult.Success } msg
			? string.Empty
			: entity.ResponseCodec.To(Convert.ToFilteredAllEventsBackwardFeed(msg, entity.ResponseUrl, embed));
	}

	public static string ReadAllEventsForwardCompleted(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed) {
		return message is not ClientMessage.ReadAllEventsForwardCompleted { Result: ReadAllResult.Success } msg
			? string.Empty
			: entity.ResponseCodec.To(Convert.ToAllEventsForwardFeed(msg, entity.ResponseUrl, embed));
	}

	public static string ReadAllEventsForwardFilteredCompleted(HttpResponseFormatterArgs entity, Message message, EmbedLevel embed) {
		return message is not ClientMessage.FilteredReadAllEventsForwardCompleted { Result: FilteredReadAllResult.Success } msg
			? string.Empty
			: entity.ResponseCodec.To(Convert.ToAllEventsForwardFilteredFeed(msg, entity.ResponseUrl, embed));
	}

	public static string WriteEventsCompleted(HttpResponseFormatterArgs entity, Message message) => string.Empty;

	public static string DeleteStreamCompleted(HttpResponseFormatterArgs entity, Message message) => string.Empty;

	public static string GetFreshStatsCompleted(HttpResponseFormatterArgs entity, Message message) {
		return message is not MonitoringMessage.GetFreshStatsCompleted { Success: true } completed ? string.Empty : entity.ResponseCodec.To(completed.Stats);
	}

	public static string GetReplicationStatsCompleted(HttpResponseFormatterArgs entity, Message message) {
		if (message.GetType() != typeof(ReplicationMessage.GetReplicationStatsCompleted))
			throw new Exception($"Unexpected type of Response message: {message.GetType().Name}, expected: {nameof(ReplicationMessage.GetReplicationStatsCompleted)}");
		var completed = message as ReplicationMessage.GetReplicationStatsCompleted;
		return entity.ResponseCodec.To(completed.ReplicationStats);
	}

	public static string GetFreshTcpConnectionStatsCompleted(HttpResponseFormatterArgs entity, Message message) {
		if (message is not MonitoringMessage.GetFreshTcpConnectionStatsCompleted completed)
			return string.Empty;

		return entity.ResponseCodec.To(completed.ConnectionStats);
	}

	public static string SendPublicGossip(HttpResponseFormatterArgs entity, Message message) {
		if (message.GetType() != typeof(GossipMessage.SendClientGossip))
			throw new Exception($"Unexpected type of response message: {message.GetType().Name}, expected: {typeof(GossipMessage.SendClientGossip).Name}");

		return message is GossipMessage.SendClientGossip sendPublicGossip
			? entity.ResponseCodec.To(sendPublicGossip.ClusterInfo)
			: string.Empty;
	}

	public static string ReadNextNPersistentMessagesCompleted(HttpResponseFormatterArgs entity, Message message, string streamId, string groupName, int count, EmbedLevel embed) {
		var msg = message as ClientMessage.ReadNextNPersistentMessagesCompleted;
		return msg is not { Result: ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult.Success }
			? msg != null ? entity.ResponseCodec.To(msg.Reason) : string.Empty
			: entity.ResponseCodec.To(Convert.ToNextNPersistentMessagesFeed(msg, entity.ResponseUrl, streamId, groupName, count, embed));
	}

	public static string GetDescriptionDocument(HttpResponseFormatterArgs entity, string streamId, string[] persistentSubscriptionStats) {
		return entity.ResponseCodec.To(Convert.ToDescriptionDocument(entity.RequestedUrl, streamId, persistentSubscriptionStats));
	}
}
