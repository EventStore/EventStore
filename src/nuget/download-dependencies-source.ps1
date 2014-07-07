svn checkout -r 594 http://protobuf-net.googlecode.com/svn/trunk/ ..\..\..\protobuf-net-read-only
git clone https://github.com/JamesNK/Newtonsoft.Json ..\..\..\Newtonsoft.Json
push-location ..\..\..\Newtonsoft.Json
git checkout 4.5.7
pop-location
