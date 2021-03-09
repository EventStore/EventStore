# Deploy to GKE

This guide is to show how to use [the official EventStoreDB Helm Chart](https://github.com/EventStore/EventStore.Charts) to
interactively deploy an EventStoreDB Cluster in Kubernetes Google Cloud GKE service.

::: warning
After reviewing our strategy in regards to deployment of EventStoreDB on Kubernetes, we have decided to deprecate the Helm chart. While we believe that Helm charts are a great solution for deploying simple applications, we do not believe that they provide the comprehensive life-cycle management features that a distributed database like EventStoreDB requires for real world operational use. As such we are devoting resources to develop a Kubernetes operator that satisfies these requirements, for release at a future date.

For more information [read this blog post](https://eventstore.com/blog/event-store-on-kubernetes/).
:::

## Prerequisites

Install the following utilities in your development machine.

- [Kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl)
- [Helm](https://github.com/helm/helm/releases)
- [Google Cloud SDK](https://cloud.google.com/sdk/install)

## Configuration steps

Login in your Google Cloud account using the gcloud CLI:

```shell
gcloud auth login --no-launch-browser
```

Set the [region](https://cloud.google.com/compute/docs/regions-zones/), and the project id from above:

```shell
gcloud config set compute/region <regionname>
gcloud config set project <projectid>
```

Enable the Kubernetes Engine API for your project, by visiting the _<https://console.cloud.google.com/apis/library/container.googleapis.com?project={project-id}>_ page.

Create a Kubernetes cluster in your account, the following command does not specify the number of nodes and uses the default of 3:

```shell
gcloud container clusters create <clustername> --zone <zonename>
```

We recommend `kubectl` for managing resources in the Kubernetes cluster. Set the current context for `kubectl` and merge it with any existing configuration in your existing config file:

```shell
gcloud beta container clusters get-credentials <clustername> --zone <zonename> --project <projectid>
```

On the server side Helm relies on a service account called Tiller, and you need to configure this account for [Role Base Access](https://kubernetes.io/docs/reference/access-authn-authz/rbac/) as Google Cloud GKE enables it by default. To configure RBAC follow [these instructions](https://helm.sh/docs/using_helm/#tiller-and-role-based-access-control). In Summary you need to create a special deployment with the Tiller user settings before running the `helm init` command.

You can then check if the 'tiller-deploy-xxxx' pod is running

```shell
kubectl -n kube-system get pod
```

### Deploy cluster with Helm

It is possible to specify a lot of options to customise your EventStoreDB deployment. The setting used in this guide is "Persistent Volume", that allows you to deploy a [Persistent Volume Claim](https://kubernetes.io/docs/concepts/storage/persistent-volumes/). This Claim is an abstraction that requires Kubernetes to set up one persistent volume per each EventStoreDB node and assign an id to it. These volumes are then reused by the cluster, for example, we want to upgrade the version of the Cluster and retain the data. If we don’t specify an existing volume then the volumes are dynamically created.

```shell
helm repo add eventstore https://eventstore.github.io/EventStore.Charts
helm repo update
helm install -n eventstore eventstore/eventstore --set persistence.enabled=true
```

Google Cloud GKE sets the authentication to use RBAC by default. Because of this, to reach your EventStoreDB cluster you have to set up access for  anonymous users. This is something you would only do for a test environment using the following command:

```shell
kubectl create clusterrolebinding cluster-system-anonymous --clusterrole=cluster-admin --user=system:anonymous
```

## Upgrade the cluster

Verify your current EventStoreDB cluster:

```shell
helm status eventstore
```

Fork the official EventStoreDB Helm Chart repository and change the version of the image in the chart _values.yaml_.

And run the command in the same directory as the chart:

```shell
helm upgrade eventstore . --set persistence.enabled=true
```

The upgrade command upgrades all the pods one by one without downtime and attaches the existing volumes to the new pods during the upgrade.

## Rollback to a previous version

To rollback the upgrade, first use the following command to display the history:

```shell
helm history eventstore
```

And then the following command to rollback to a specific revision:

```shell
helm rollback eventstore 1
```

## Delete resources

```shell
gcloud container clusters delete <clustername> --zone <zonename>
```

Then login in to the Google Cloud Web UI, and in the Kubernetes Engine view delete the Kubernetes cluster using the bin icon.
