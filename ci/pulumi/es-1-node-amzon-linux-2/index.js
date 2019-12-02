const aws = require("@pulumi/aws");
const pulumi = require("@pulumi/pulumi");

var config = new pulumi.Config();

// const keyName = config.require("keyName");  //#AWS key pair name
const pullNo = config.require("pullNo");

let size = "t2.small";
let ami = aws.getAmi({
    filters: [{
        name: "name",
        values: ["amzn2-ami-hvm-*"],
    }],
    owners: ["137112412989"],
    mostRecent: true,
});

let group = new aws.ec2.SecurityGroup("webserver-secgrp", {
    ingress: [
        { protocol: "tcp", fromPort: 22, toPort: 22, cidrBlocks: ["0.0.0.0/0"] },
        { protocol: "tcp", fromPort: 80, toPort: 80, cidrBlocks: ["0.0.0.0/0"] },
        { protocol: "tcp", fromPort: 2113, toPort: 2113, cidrBlocks: ["0.0.0.0/0"] },
        { protocol: "tcp", fromPort: 1113, toPort: 1113, cidrBlocks: ["0.0.0.0/0"] }
    ],
    egress: [
        { protocol: "tcp", fromPort: 0, toPort: 65535, cidrBlocks: ["0.0.0.0/0"] }
    ],
});

let userData =
`#!/bin/bash
wget https://download.visualstudio.microsoft.com/download/pr/941853c3-98c6-44ff-b11f-3892e4f91814/14e8f22c7a1d95dd6fe9a53296d19073/dotnet-sdk-3.1.100-preview3-014645-linux-x64.tar.gz
sudo mkdir -p $HOME/dotnet && sudo tar zxf dotnet-sdk-3.1.100-preview3-014645-linux-x64.tar.gz -C $HOME/dotnet
export DOTNET_ROOT=$HOME/dotnet
export PATH=$PATH:$HOME/dotnet
export DOTNET_CLI_HOME=/

sudo yum install git -y
git clone https://github.com/EventStore/EventStore.git
cd EventStore/
git fetch origin pull/`+pullNo+`/head:pull-ci
git checkout pull-ci

cd src/EventStore.ClusterNode
export EVENTSTORE_INT_IP=0.0.0.0
export EVENTSTORE_EXT_IP=0.0.0.0
dotnet run`;

var instance = new aws.ec2.Instance("EventStoreNode", {
    instanceType: size,
    securityGroups: [group.name],
    ami: ami.id,
    // keyName: keyName,
    tags: {
        Name: "EventStoreNode"
    },
    userData: userData
    ,
});

exports.publicIp = instance.publicIp;
exports.publicHostName = instance.publicDns;
