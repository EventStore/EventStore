..\..\..\..\tools\protoc\protoc.exe --descriptor_set_out=ClientMessageDtos.TempDescriptors --proto_path=..\..\Protos\ClientAPI ..\..\Protos\ClientAPI\ClientMessageDtos.proto
..\..\..\..\tools\ProtoGen\protogen.exe -p:fixCase -i:ClientMessageDtos.TempDescriptors -ns:EventStore.ClientAPI.Messages -o:..\..\EventStore.ClientAPI\Messages\ClientMessage.cs -p:umbrella="ClientMessage" -p:umbrellaVisibility="internal"
..\..\..\..\tools\ProtoGen\protogen.exe -p:fixCase -i:ClientMessageDtos.TempDescriptors -ns:EventStore.Core.Messages -o:..\..\EventStore.Core\Messages\TcpClientMessageDto.cs -p:umbrella="TcpClientMessageDto" -p:umbrellaVisibility="public"
del -f ClientMessageDtos.TempDescriptors
