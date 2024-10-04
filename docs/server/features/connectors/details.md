---
order: 4
---

# Technical details

## Delivery guarantees

Events are delivered *at least once* to the sink. They are delivered *in order* meaning that event `x` is not delivered until all the events before `x` have been delivered.

Often, the system that is receiving the events will want to keep track of the address of the last event that it has received so that it can easily spot and discard duplicate deliveries. 

## Checkpointing

Connectors periodically store the position of the last event that they have successfully processed. Then, if the connector host is restarted, the connectors can continue from close to where they got up to. The checkpoints are stored in EventStoreDB.

The checkpoints are stored in a stream per connector.

![Connector Checkpoint](./images/connector-checkpoint-stream.png)

## High availability

Connectors are hosted in EventStoreDB nodes. If a node goes down, the connectors will be restarted on another node based on the configured affinity.

Currently, when a connector instance is configured to run on a follower node, it might end up receiving events with a delay that matches the replication lag. This is because the connector will only process events that have been replicated to the follower node.

## Performance

The preview version of connectors is not optimized for high throughput. Each configured connector runs as one instance in the whole cluster, and it doesn't support concurrent event processing. It is a limitation that we plan to address in future versions.