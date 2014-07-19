#!/usr/bin/env bash

SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
BASEDIR=$SCRIPTDIR/../..

MONO_PATH=/opt/mono/bin/mono
PROTOC_PATH=$BASEDIR/tools/protoc/protoc
PROTOGEN_PATH=$BASEDIR/tools/ProtoGen/protogen.exe

$PROTOC_PATH --descriptor_set_out=ClientMessageDtos.TempDescriptors --proto_path=$BASEDIR/src/Protos/ClientAPI $BASEDIR/src/Protos/ClientAPI/ClientMessageDtos.proto
$MONO_PATH $PROTOGEN_PATH -p:fixCase -i:ClientMessageDtos.TempDescriptors -ns:EventStore.ClientAPI.Messages -o:$BASEDIR/src/EventStore.ClientAPI/Messages/ClientMessage.cs -p:umbrella="ClientMessage" -p:umbrellaVisibility="internal"
$MONO_PATH $PROTOGEN_PATH -p:fixCase -i:ClientMessageDtos.TempDescriptors -ns:EventStore.Core.Messages -o:$BASEDIR/src/EventStore.Core/Messages/TcpClientMessageDto.cs -p:umbrella="TcpClientMessageDto" -p:umbrellaVisibility="public"
rm -f ClientMessageDtos.TempDescriptors
