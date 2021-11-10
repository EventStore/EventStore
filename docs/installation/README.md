# Installation

EventStoreDB is available on multiple platforms. Below you can find instructions for installing an EventStoreDB instance. 

Refer to the [clustering documentation](../clustering/) to upgrade your deployment to a highly available cluster. Clusters consists of several EventStoreDB nodes, follow the guidelines from this section to install each node of the cluster.

After installing an EventStoreDB instance you'd need to set up its [networking](../networking/README.md) so you can connect to it from other machines.

Follow the [security guidelines](../security/) to prepare your instance of cluster for production use.

Check the [configuration](configuration.md) page to find out how to configure your deployment.

## Bare metal and VMs

- [Linux](./linux.md)
- [Windows](./windows.md)
- macOS bare metal is not supported. We recommend using Docker instead.

## Docker

- [Docker and Docker Compose](./docker.md)

## Kubernetes

- [Azure Kubernetes Services](./kubernetes-aks.md)
- [Google Kubernetes Engine](./kubernetes-gke.md)
- [Generic Kubernetes](./kubernetes.md)


