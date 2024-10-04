---
title: "Introduction"
---

# Connectors Preview Introduction <Badge text="Commercial" type="warning" vertical="middle"/>

Welcome to the Connectors Early Preview program!

::: note
The Connector plugin is pre-installed in commercial versions of EventStoreDB but not enabled by default. 
:::

Connectors simplify the integration of EventStoreDB data into other systems by reducing the work required to process data from EventStoreDB instances.  Connectors also reduce the work required to create data pipelines when implementing complex use cases.

Each connector runs on the server-side and uses a catch-up subscription to:
1. Receive events
2. Filter (or transform) events
3. Push events to an external system via a [sink](https://en.wikipedia.org/wiki/Sink_(computing))

![Connectors Anatomy](./images/connector-anatomy.png)

In this preview, there are two sinks:

- [Console Sink](./sinks.md#console-sink) for experimentation
- [HTTP Sink](./sinks.md#http-sink) for sending events to an HTTP endpoint

## Motivation

Users can experience a pain point when subscribing to EventStoreDB and
sending events to a downstream service requires the implementation, hosting, and maintainance of a highly avaiable solution that has to manage its own checkpoints. The management of a such a solution within the current infrastcuture can rapidly become cumbersome.

Event Store Connectors remove the need for users to develop, host, and maintain such a solution.

For example, assume a developer wants to send events to a lambda function, which projects them to an RDS instance.  In this scenario, the subscription service using a catch-up subscription needs to run as a single instance to ensure ordered event processing. The consequence is introducing a single point of failure.

![Example with EKS and Lambda](./images/example-lambda-eks.png)

With Event Store Connectors, the subscription service is provided natively by EventStoreDB and the lambda function acts the sink. For this particular example, the lambda function requires accessible HTTP endpoint, or be exposed via the AWS API Gateway.

![Example with Connector and Lambda](./images/example-lambda-connector.png)

## Preview goals

We will collect your input and feedback during the connectors preview program to:

* Validate the approach
* Identify important areas for improvement
* Allow EventStoreDB users to collaborate and contribute to the product
* Iteratively improve and gather more feedback

## Further plans

Although the Connectors plugin is only available in commercial packages, we will open up the preview program to a wider audience in the coming months.

Further, we plan to:
* Add more sinks
* Allow concurrent processing of events
