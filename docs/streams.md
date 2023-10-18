# Event Streams

EventStoreDB is purpose-built for event storage. Unlike traditional state-based databases, which retain only the most recent entity state, EventStoreDB allows you to store each state alteration as an independent event.

These **events** are logically organized into **streams**, typically only one stream per entity.

## Metadata and reserved names

### Stream metadata ##

In EventStoreDB, every stream and event is accompanied by metadata, distinguished by the **`$$`** prefix. For example, the stream metadata for a stream named **`foo`** is **`$$foo`**. You can modify specific metadata values and write your data to the metadata, which can be referenced in your code.

### Reserved names

EventStoreDB uses a **`$`** prefix for all internal data, such as **`$maxCount`** in a stream's metadata. Do not use a **`$`** prefix for event names, metadata keys, or stream names, except as detailed below.

The supported internal settings are:

| Property name   | Description                                                                                                                                                                                                                                                                                                                                                                   |
| :-------------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`$maxAge`**       |  Sets a sliding window based on dates. When data reaches a certain age, it automatically disappears from the stream and becomes eligible for scavenging. This value is set as an integer representing the number of seconds (must be >= 1).                                                                                                                  |
| **`$maxCount`**     |   Sets a sliding window based on the number of items in the stream. When data reaches a certain length, it automatically disappears from the stream and becomes eligible for scavenging. This value is set as an integer representing the count of items (must be >= 1).                                                                                     |
| **`$cacheControl`** | Controls the cache of the head of a stream. While most URIs in a stream are infinitely cacheable, the head, by default, will not cache. In some situations, it may be preferable to set a small amount of caching on the head to allow intermediaries to handle polls (e.g., 10 seconds). The argument is an integer representing the seconds to cache (must be >= 1). |
&nbsp;

::: tip
If you configure both **`$maxAge`** and **`$maxCount`**, events then become eligible for scavenging when either
condition is met. For example, if you set **`$maxAge`** to 10 seconds and **`$maxCount`** to 50,000, events 
qualify for scavenging after either 10 seconds or the accumulation of 50,000 events. Affected items are removed once the scavenge process runs.
:::


**Security access control lists (ACLs)** are part of the **`$acl`** section in the stream metadata. 

| Property name | Description                                                 |
| :------------ | :---------------------------------------------------------- |
| **`$r`**          | Users with read permissions                             |
| **`$w`**          | Users with write permissions                            |
| **`$d`**          | Users with delete permissions                           |
| **`$mw`**         | Users with write permissions to stream metadata         |
| **`$mr`**         | Users with read permissions to stream metadata          |




You can find more information on ACLs in the [access control lists documentation](./security.md#access-control-lists).

### Event metadata ##

All names starting with **`$`** are reserved for internal use. The currently supported reserved internal names include:

| Property name    | Description                                                        |
| :--------------- | :----------------------------------------------------------------- |
| **`$correlationId`** | The application-level correlation ID for the message.           |
| **`$causationId`**   | The application-level causation ID for the message message.    |


Projections within EventStoreDB adhere to both the **`correlationId`** and **`causationId`** patterns for any events they internally produce (e.g., through functions like `linkTo` and `emit`).

## Deleting streams and events

In EventStoreDB, metadata can determine whether an event is deleted or not. You can use [stream metadata](#metadata-and-reserved-names) elements such as **`TruncateBefore`**, **`MaxAge`**, and **`MaxCount`** to
filter events marked as deleted. During a stream read, the index references the stream's metadata to determine if any events have been deleted.

::: note
In EventStoreDB, you cannot selectively delete events from the middle of a stream. It only allows truncating the stream.
:::

When you delete a stream, EventStoreDB offers two options: **soft delete** or **hard delete**.

**Soft delete** triggers scavenging, removing all events from the stream during the subsequent scavenging process. This allows for the reopening of the stream by appending new events. 

It's worth noting that the **`$all`** stream circumvents index checking. Deleted events within this stream remain readable until a scavenging process removes them. To understand the prerequisites for successful event removal through scavenging, refer to the [scavenging guide](operations.md#scavenging-events).

Even if a stream has been deleted, EventStoreDB retains one event within the stream to indicate the stream's existence and provide information about the last event version. As a best practice, consider appending a specific event like **`StreamDeleted`** and then setting the **`MaxCount`** to 1 to delete the stream. Keep this in mind, especially when dealing with streams containing sensitive data that you want to erase thoroughly and without a trace.

### Soft delete and TruncateBefore

In event sourcing, **`TruncateBefore`** (or **`$tb`**) designates any event with an event number lower than its value as deleted. For instance, consider a stream with the following events: 

```
0@test-stream
1@test-stream
2@test-stream
3@test-stream
```

Setting the **`TruncateBefore`** (or **`$tb`**) value to 3, results in reading only the last event: 

```
3@test-stream
```

A **soft delete** leverages **`TruncateBefore`** (or **`$tb`**). When you delete a stream, **`TruncateBefore`**
(or **`$tb`**) is set to the [max long/Int64 value](https://docs.microsoft.com/en-us/dotnet/api/system.int64.maxvalue?view=net-5.0): 9223372036854775807. 

If you attempt to read this soft-deleted stream the read returns a **`StreamNotFound`** or **`404`** result. 

Appending to the stream later adds events starting from the last stream revision before deletion, incremented by one. Continuing with our previous example, a new event would be appended with event number 4, and only events from event number 4 onwards are visible when you read this stream.

### Max count and Max age

**Max count** (**`$maxCount`** and **`MaxCount`**) restricts the number of events you can read from a stream. Reading a stream with a max count of 5 allows you to retrieve only the last 5 events, regardless of the total events in the stream. 

**Max age** (**`$maxAge`** and **`MaxAge`**) specifies the time an event can live in seconds. The age is
calculated at the time of the read. For example, if you read a stream with a **`MaxAge`** of 3 minutes, an event that has existed for 4 minutes won't be returned.

### Hard delete

A **hard delete** involves appending a **`tombstone`** event to the stream and permanently deleting it. This action prevents stream recreation or further appends. Tombstone events are appended with the event type **`$streamDeleted`**. 
Any attempt to append or read a hard-deleted stream, the response is a **`StreamDeleted`** or **`410`** result. 

While events in the deleted stream are subject to removal during a scavenge, the tombstone event remains.

::: note
Hard deletion of a stream is permanent; you cannot append or recreate it. 
It is recommended to opt for soft deletion for streams unless you have a specific requirement for permanent removal.
:::

### Deleted events and projections

If you plan to use projections and delete streams, there are some considerations:

- In the context of **`$all`**, projections using **`fromAll`** read any deleted events that have not been scavenged. They also receive tombstone events from any hard deletions.
- Projections that read from a specific stream receive that stream's metadata events. You can filter these out by ignoring events with an event type `$metadata`.
- System projections like [by category](projections.md#by-category) or [by event type](projections.md#by-event-type) produce new link events stored alongside
 the original event in the database. When original events are deleted, link events remain in the projected streams, but their links remain unresolved (with undefined values). You can ignore those events in the code logic.

## System events and streams

### **`$persistentSubscriptionConfig`**

**`$persistentSubscriptionConfig`** is a specialized paged stream that stores all configuration events for all
persistent subscriptions. It uses the **`PersistentConfig`** system event type, capturing configuration changes. 
The event data includes:

| Property name    | Description                                                        |
|:-----------------|:-------------------------------------------------------------------|
|**`version`**| Event data version                               |          
|**`updated`**| Date of the update                               |
| **`updatedBy`**| User responsible for the configuration update |
| **`maxCount`**| The number of configuration events to retain   |
| **`entries`**| Configuration items set by the event            |

### **`$all`**

**`$all`** is a dedicated paged stream containing all events. You can employ the same paged reading method described earlier to
read all events for a node by pointing the stream at _/streams/\$all_. Like any other stream, you can perform all operations, except posting to it.

### **`$settings`**

The **`$settings`** stream hosts a special ACL serving as the default ACL. It governs the default ACL for streams lacking ACLs and controls who can create streams within the system.

Learn more about the default ACL in the [access control lists](security.md#default-acl) documentation.

### **`$stats`**

EventStoreDB offers debugging and statistical information about a cluster within the **`$stats`** stream. Find out more in [the stats guide](diagnostics.md#statistics).

### **`$scavenges`**

**`$scavenges`** represents a specialized paged stream for all scavenge-related events. It uses the following system event
types:

- **`$scavengeIndexInitialized`**: An event that records the initialization of the scavenge index.
- **`$scavengeStarted`**: An event that records the beginning of a scavenge process. The event data contains:

  - **`scavengeId`**: Scavenge event ID
  - **`nodeEndpoint`**: Node address

    - **`$scavengeCompleted`**: An event that records the completion of a scavenge process. The event data contains:
    - **`scavengeId`**: Scavenge event ID
    - **`nodeEndpoint`**: Node address
    - **`result`**: **`Success`**, **`Failed`**, **`Stopped`**
    - **`error`**: Error details
    - **`timeTaken`**: Duration of the scavenging process in milliseconds
    - **`spaceSaved`**: Space saved by scavenge process in bytes.