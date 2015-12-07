#!/usr/bin/env bash
pathPrefix=$1
LD_LIBRARY_PATH=$pathPrefix/bin/tests:$MONOPATH/lib/:$LD_LIBRARY_PATH mono $pathPrefix/tools/nunit-2.6.3/bin/nunit-console.exe $pathPrefix/bin/tests/EventStore.BufferManagement.Tests.dll $pathPrefix/bin/tests/EventStore.Core.Tests.dll $pathPrefix/bin/tests/EventStore.Projections.Core.Tests.dll
