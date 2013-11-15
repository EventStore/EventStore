#!/bin/bash

MONO_PATH=/opt/mono/bin/mono
PROTOC_PATH=../../../../tools/protoc/protoc
PROTOGEN_PATH=../../../../tools/ProtoGen/protogen.exe

$PROTOC_PATH --descriptor_set_out=ClientMessageDtos.TempDescriptors --proto_path=../../Protos/ClientAPI ../../Protos/ClientAPI/ClientMessageDtos.proto
$MONO_PATH $PROTOGEN_PATH -p:fixCase -i:ClientMessageDtos.TempDescriptors -ns:EventStore.ClientAPI.Messages -o:../../EventStore.ClientAPI/Messages/ClientMessage.cs -p:umbrella="ClientMessage" -p:umbrellaVisibility="internal"
$MONO_PATH $PROTOGEN_PATH -p:fixCase -i:ClientMessageDtos.TempDescriptors -ns:EventStore.Core.Messages -o:../../EventStore.Core/Messages/TcpClientMessageDto.cs -p:umbrella="TcpClientMessageDto" -p:umbrellaVisibility="public"
rm -f ClientMessageDtos.TempDescriptors
