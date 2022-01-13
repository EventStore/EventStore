# Configuring your EventStoreDB installation

<!-- I envisage this file growing. Possibly some material from other server docs could be moved here 
(there's an issue out there about tidying up this section)
and I imagine other config questions may come up in future -->

## Linux

### Start EventStoreDB as a service on a custom port

Follow the instructions in the [installation](installation.md#linux) section to install an EventStoreDB package. Do not start EventStoreDB.

Find your `inet` address. For example:

```shell
#Retrieve your IP address
ifconfig

#Output
eth0: flags=4163<UP,BROADCAST,RUNNING,MULTICAST>  mtu 1500
        inet 192.168.1.68  netmask 255.255.255.0  broadcast 192.168.1.255
        inet6 fe80::d12:27b9:96c2:c00e  prefixlen 64  scopeid 0xfd<compat,link,site,host>

```

Edit your EventStoreDB config:

```shell
#Open the file
sudo nano /etc/eventstore/eventstore.conf

#Add the following
extIp: <your inet address>
ExtHttpPort: 80
ExtHttpPrefixes: http://*:80/
AddInterfacePrefixes: false
```

Allow the EventStoreDB executable to bind to a port lower than 1024:

```shell
sudo setcap CAP_NET_BIND_SERVICE=+eip /usr/bin/eventstored
```

Start the EventStoreDB service as normal:

```shell
sudo systemctl start eventstore
```
