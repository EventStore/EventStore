#!/usr/bin/env bash

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

METADATA_BASE_URL="http://169.254.169.254/latest/meta-data"
EVENTSTORE_SERVER_COUNT_MIN=${EVENTSTORE_SERVER_COUNT_MIN:-0}
THIS_PRIVATE_IP=`curl --silent --location ${METADATA_BASE_URL}/local-ipv4`

port=2112
eventstore_config_dir='/etc/eventstore'

addresses=`${BASE_DIR}/getASGInstanceAddresses.sh`

while [ "$(echo "$addresses" | sed 's/\s/\n/g' | wc -l)" -lt "${EVENTSTORE_SERVER_COUNT_MIN}" ]; do
    sleep 5
    addresses=`${BASE_DIR}/getASGInstanceAddresses.sh`
done

if [ "$EVENTSTORE_SERVER_COUNT_MIN" -eq "0" ]; then
    address_count=$(echo "$addresses" | sed 's/\s/\n/g' | wc -l)
    EVENTSTORE_SERVER_COUNT_MIN=$address_count
fi

#TODO(jen20) Improve this contatenation
cat <<EOF > "${eventstore_config_dir}/eventstore.conf"
---
RunProjections: System
Db: /var/lib/eventstore
Log: /var/log/eventstore
StatsPeriodSec: 60
ClusterSize: ${EVENTSTORE_SERVER_COUNT_MIN}
GossipIntervalMs: 1000
GossipTimeoutMs: 2000
MaxMemTableSize: 500000
IntIp: ${THIS_PRIVATE_IP}
ExtIp: ${THIS_PRIVATE_IP}
IntHttpPort: 2112
ExtHttpPort: 2113
IntTcpPort: 1112
ExtTcpPort: 1113
IntHttpPrefixes: http://*:2112/
ExtHttpPrefixes: http://*:2113/
AddInterfacePrefixes: false
DiscoverViaDns: false
GossipSeed:
EOF

for ip in $addresses; do
    if [ "$ip" != "$THIS_PRIVATE_IP" ]; then
        if [ -n "$joined" ]; then
		continue
        fi
        echo "    - ${ip}:${port}" >> "${eventstore_config_dir}/eventstore.conf"
    fi
done
