# Guide to setting up a 3-node cluster

This guide provides a step-by-step process for setting up a 3-node EventStoreDB cluster, including environment preparation, certificate generation, and cluster configuration. For additional help, refer to our tutorials on how to get started with EventStoreDB on [Windows](https://www.eventstore.com/blog/getting-started-with-eventstoredb-our-how-to-guide) or [Linux](https://developers.eventstore.com/server/v23.10/installation.html#linux). 



## Preparing the environment

1. Create a 'Cluster' folder. Inside, add three subfolders: 'Node1', 'Node2', and 'Node3'.
2. Add an empty configuration file (.yml or .txt) to each node folder.
3. In each node folder, create subfolders:
    - 'certificates' for the certificate and private key. 
    - 'data' for data files.
    - 'index' for node indexes.
    - 'logs' for logging.



## Generating certificates

1. Create a 'Generate_certificate' folder on your desktop.
2. Download the latest certificate generator from the [EventStore Certificate Generation CLI repository](https://github.com/EventStore/es-gencert-cli/releases) and extract it into the 'Generate_certificate' folder.
3. Open a terminal and navigate to the 'es-gencert-cli' directory: 

For instructions on how to do this 

Run this command to generate the root certificate and root private key on Linux: 
`./es-gencert-cli create-ca -out [Generate_certificate Path]/ca`

On Windows:

`.\es-gencert-cli.exe create-ca -out [Generate_certificate Path]\ca`



4. In the 'Generate_certificate' directory, run the following commands one at a time for each node (change the path and node number as needed) to generate the certificate and private keys for each node. Use your actual DNS names and recommended IP address configurations to align with production practices. For further details, check out our [cluster with DNS guide](https://developers.eventstore.com/server/v23.10/cluster.html#cluster-with-dns).

For example, if the certificate generator version is 1.2.1 and we’re generating the certificate and private keys for 'Node1' on Linux:

`./es-gencert-cli create-node -ca-certificate /path/to/folder/Generate_certificate/es-gencert-cli_1.2.1_Linux-x86_64/ca/ca.crt -ca-key` 
`/path/to/folder/Generate_certificate/es-gencert-cli_1.2.1_Linux-x86_64/ca/ca.key -out`
`/path/to/folder/Cluster/Node1/certificates -dns-names your.node1.dns.com`

Each command will automatically generate the security certificate and the private key for each node in their respective certificates file:

Linux: `/path/to/folder/Cluster/Node1/certificates` 
Windows: `C:\Path\To\Folder\Cluster\Node1\certificates`

5.  Include CA certificate and key paths in each node's configuration file. An example of a complete configuration file on Linux:

``` 
# Paths
Db: /path/to/folder/Cluster/Node1/Data
Index: /path/to/folder/Cluster/Node1/Index
Log: /path/to/folder/Cluster/Node1/Log

# Network configuration
IntIp: your.node1.internal.dns.com
ExtIp: your.node1.external.dns.com
HttpPort: 21131
IntTcpPort: 11121
EnableAtomPubOverHTTP: true

# Cluster gossip
ClusterSize: 3
DiscoverViaDns: true
ClusterDns: your.cluster.dns.com

# Projections configuration
RunProjections: All

# Certificates configuration
CertificateFile: /path/to/folder/Cluster/Node1/certificates/node.crt
CertificatePrivateKeyFile: /path/to/folder/Cluster/Node1/certificates/node.key
TrustedRootCertificatesPath: /path/to/folder/Generate_certificate/ca
```

Adjust the paths based on the operating system.

## Running nodes

1. Download and unzip the latest version of ESDB.
2. Navigate to the ESDB directory and start each node with its configuration file:

Linux: `./EventStore.ClusterNode --config /path/to/folder/Cluster/Node1/node1.yml`
Windows: `.\EventStore.ClusterNode --config C:\Path\To\Folder\Cluster\Node1\node1.yml` 


## Verifying Cluster Status

EventStoreDB comes with an Admin UI that can be accessed via a web browser. Navigate to:
`http://your.node1.dns.com:2113/web/index.html`

Replace `your.node1.dns.com` with the DNS name or IP address of any node in your cluster. The Admin UI provides an overview of the cluster's health, including the status of each node. You can verify if all nodes are connected and functioning as expected.









