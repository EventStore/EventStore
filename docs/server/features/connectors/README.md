---
title: "Connectors Preview"
dir:
  text: "Connectors"
  order: 10
---

# Connectors Preview introduction 

<Badge text="Commercial" type="info" vertical="middle"/>

Connector Preview is available in EventStoreDB v24.2, Commercial distribution package.

::: important
The Connector plugin is pre-installed in commercial versions of EventStoreDB but not enabled by default. 
:::

Connectors make it easy to integrate EventStoreDB data into other systems.

Each connector runs on the server-side and uses a catch-up subscription to receive events, filter or transform them, and push them to an external system via a [sink](https://en.wikipedia.org/wiki/Sink_(computing)).

![Connectors Anatomy](./images/connector-anatomy.svg#light)
![Connectors Anatomy](./images/connector-anatomy-dark.svg#dark)

This reduces the amount of work needed to process data from EventStoreDB instances and makes it easy to create data pipelines to implement complex use cases.

In this preview, there are two sinks:

- [Console Sink](./sinks.md#console-sink) for experimentation
- [HTTP Sink](./sinks.md#http-sink) for sending events to an HTTP endpoint

## Motivation

Currently, a pain point that users experience is that, on one hand, they have a convenient EventStoreDB cloud service, on the other hand, they have a convenient downstream database or processing engine, but there's nothing in between. As a result, users need to host and maintain their own
solution in their own infrastructure for subscribing to EventStoreDB and
sending the events to a downstream service. This solution in the middle often needs to be highly available and needs to manage its own checkpoints: this quickly becomes cumbersome.

EventStore Connectors remove the need for users to develop, host and maintain such a solution.

As an example, a developers want to send events to a lambda function, which projects events to an RDS instance.

Without connectors, it requires to implement a subscription service. Such service then needs to be hosted, observed, and maintained. It also needs to manage its own checkpoints.

In addition, the subscription service that uses a catch-up subscription would need to run as a single instance to ensure ordered event processing. As a consequence, it becomes a single point of failure.

![Example with EKS and Lambda](./images/example-lambda-eks.svg#light)
![Example with EKS and Lambda](./images/example-lambda-eks-dark.svg#dark)

With connectors, the subscription service is provided by EventStoreDB, and the lambda function can be the sink. For this particular example, the lambda function would need an accessible HTTP endpoint, or be exposed via the AWS API Gateway.

![Example with Connector and Lambda](./images/example-lambda-connector.svg#light)
![Example with Connector and Lambda](./images/example-lambda-connector-dark.svg#dark)

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





