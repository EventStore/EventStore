const aws = require("@pulumi/aws");
const pulumi = require("@pulumi/pulumi");

var config = new pulumi.Config();

//const keyName = config.require("keyName");  //#AWS key pair name
const pullNo = config.require("pullNo");  //#AWS key pair name

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
`   #!/bin/bash
git clone https://github.com/EventStore/EventStore.git
git fetch origin pull/`+pullNo+`2068/head:pull-ci
git checkout pull-ci`;

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
