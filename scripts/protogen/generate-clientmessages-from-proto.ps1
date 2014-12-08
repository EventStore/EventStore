# Setup and Configuration
$baseDirectory = Resolve-Path (Join-Path $PSScriptRoot "..\..\")
$srcDirectory = Join-Path $baseDirectory "src"
$toolsDirectory = Join-Path $baseDirectory "tools"
$protocPath = Join-Path $toolsDirectory (Join-Path "protoc" "protoc.exe")
$protogenPath = Join-Path $toolsDirectory (Join-Path "ProtoGen" "protogen.exe")

try {
    Push-Location $baseDirectory

    $protoPath = Join-Path $srcDirectory (Join-Path "Protos" "ClientAPI")
    $clientProtoFile = Join-Path $protoPath "ClientMessageDtos.proto"
    $tempDescriptorsFile = Join-Path $protoPath "ClientMessageDtos.TempDescriptors"

    Start-Process -NoNewWindow -Wait -FilePath $protocPath -ArgumentList @("--descriptor_set_out=$tempDescriptorsFile", "--proto_path=$protoPath", "$clientProtoFile")
    
    $clientApiClassPath = Join-Path $srcDirectory (Join-Path "EventStore.ClientAPI" (Join-Path "Messages" "ClientMessage.cs"))
    Start-Process -NoNewWindow -Wait -FilePath $protogenPath -ArgumentList @("-p:fixCase", "-i:$tempDescriptorsFile", "-ns:EventStore.ClientAPI.Messages", "-o:$clientApiClassPath", '-p:umbrella="ClientMessage"', '-p:publicClasses="false"')

    $serverClassPath = Join-Path $srcDirectory (Join-Path "EventStore.Core" (Join-Path "Messages" "TcpClientMessageDto.cs"))
    Start-Process -NoNewWindow -Wait -FilePath $protogenPath -ArgumentList @("-p:fixCase", "-i:$tempDescriptorsFile", "-ns:EventStore.Core.Messages", "-o:$serverClassPath", '-p:umbrella="TcpClientMessageDto"', '-p:publicClasses="false"')
} finally {
    Remove-Item -Force $tempDescriptorsFile
    Pop-Location
}