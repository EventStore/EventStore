# Deploy to AKS

This guide is to show how to use [the official EventStoreDB Helm Chart](https://github.com/EventStore/EventStore.Charts) to interactively deploy an EventStoreDB Cluster in Kubernetes Azure Cloud AKS service.

::: warning
After reviewing our strategy in regards to deployment of EventStoreDB on Kubernetes, we have decided to deprecate the Helm chart. While we believe that Helm charts are a great solution for deploying simple applications, we do not believe that they provide the comprehensive life-cycle management features that a distributed database like EventStoreDB requires for real world operational use. As such we are devoting resources to develop a Kubernetes operator that satisfies these requirements, for release at a future date.

For more information [read this blog post](https://eventstore.com/blog/event-store-on-kubernetes/).
:::

## Prerequisites

Install the following utilities in your development machine.

- [kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl)
- [Helm](https://github.com/helm/helm/releases)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)

## Configuration steps

Login in your Azure Cloud account using the az cli, this triggers 2 factor authentication that launches your default browser to select account credentials.

```shell
az login
```

Create a new resource group:

```shell
az group create -n {resourcegroupname} -l {location-compatible-with-aks}
```

Create a Kubernetes cluster with 3 nodes. This command accept parameters such as the version of Kubernetes you want installed. For this tutorial we use the default options.

```shell
az aks create -n {clustername} -g {resourcegroupname} -c 3
```

The command (after some delay) returns a JSON object with details of the new Kubernetes cluster. Use the command below to return a list of all Kubernetes clusters in your Azure account:

```shell
az aks list -o table
```

We recommend `kubectl` for managing resources in the Kubernetes cluster. Set the current context for `kubectl` and merge it with any existing configuration in your existing config file:

```shell
az aks get-credentials -n {clustername} -g {groupname}
```

Get the list of nodes using `kubectl`:

```shell
kubectl get nodes
```

To use the Kubernetes dashboard you need to change the Role Base Access Control enabled by default on Azure AKS.

Create a _rbac-config.yaml_ file containing the following yaml:

```yaml
apiVersion: rbac.authorization.k8s.io/v1beta1
kind: ClusterRoleBinding
metadata:
  name: kubernetes-dashboard
  labels:
    k8s-app: kubernetes-dashboard
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: cluster-admin
subjects:
- kind: ServiceAccount
  name: kubernetes-dashboard
  namespace: kube-system
```

Create a deployment for this `ClusterRoleBinding` object

```shell
kubectl create -f ./rbac-config.yaml
```

To access the dashboard you can now use the `browse` command. This command is a wrapper around the `proxy` command of kubectl. It creates a local web server with a tunnel to the cluster hosted in Azure AKS web server

```shell
az aks browse -n {clustername} -g {groupname}
```

## Deploy cluster with Helm

Helm is the package manager for Kubernetes. After you've created a new Kubernetes cluster you need to configure Helm for your local Helm CLI to connect to a configured service account on the server side. The service account used by Helm is called Tiller. Give Tiller access to the cluster and initialise it with the following commands:

```shell
kubectl -n kube-system create serviceaccount tiller
kubectl create clusterrolebinding tiller --clusterrole cluster-admin --serviceaccount=kube-system:tiller
helm init --service-account tiller
```

You can then check if the `tiller-deploy-xxxx` pod is running

```shell
kubectl -n kube-system get pod
```

Now deploy the EventStoreDB cluster using the official Helm Chart with the following commands:

```shell
helm repo add eventstore https://eventstore.github.io/EventStore.Charts
helm repo update
helm install -n eventstore eventstore/eventstore --set persistence.enabled=true
```

The EventStoreDB cluster is now deployed and available in a couple of minutes. The default cluster size in the Helm Chart is set to 3 so this results in a 3 node EventStoreDB cluster over the 3 nodes Kubernetes cluster. The `persistence.enable=true` setting uses the `PersistentVolumeClaim` on your Kubernetes cluster to dynamically claim persistent storage volumes. You can configure this to use statically defined volumes if required.

## Upgrade the cluster

Verify your current EventStoreDB cluster:

```shell
helm status eventstore
```

Fork the official EventStoreDB Helm Chart repository and change the version of the image in the chart _values.yaml_.

Then run the command in the same directory as the chart:

```shell
helm upgrade eventstore . --set persistence.enabled=true
```

The upgrade command silently upgrades all the pods one by one
without downtime. Helm takes care of attaching the existing volumes to the new pods during the upgrade.

## Rollback to a previous version

To rollback to a previous version, first use the following command to display the history:

```shell
helm history eventstore
```

And the following command to rollback to a specific revision:

```shell
helm rollback eventstore 1
```

## Delete resources

To delete all resources associated with the EventStoreDB installation use the following command:

```shell
az aks delete -n {clustername} -g {groupname}
az group delete -n {groupname}
```
