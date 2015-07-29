#!/usr/bin/env bash
pathPrefix=$1
while getopts "m:x:p" option
do
    case $option in
        m)
            MONOPATH=$OPTARG 
            ;;   
        x)
            EXCLUDE="-exclude $OPTARG"
            ;;
        p)
            RUNPROJECTIONS="TRUE"
            ;;
        ?)
            echo "Usage: run_tests.sh [-x ExcludeCategories] [-m /path/to/mono] [-p]"
            echo "Defaults:"
            echo "   Mono Path: /opt/mono"
            echo "   Exclude: None"
            echo "   projections: false"
            exit
            ;;
    esac
done

if [[ $MONOPATH == "" ]]; then
    MONOPATH="/opt/mono"
fi

LD_LIBRARY_PATH=$pathPrefix/bin/tests:$MONOPATH/lib/:$LD_LIBRARY_PATH mono $pathPrefix/tools/nunit-2.6.3/bin/nunit-console.exe $pathPrefix/bin/tests/EventStore.Core.Tests.dll $EXCLUDE

if [[ $RUNPROJECTIONS == "TRUE" ]]; then
    LD_LIBRARY_PATH=$pathPrefix/bin/tests/:$MONOPATH/lib/:$LD_LIBRARY_PATH mono $pathPrefix/tools/nunit-2.6.3/bin/nunit-console.exe $pathPrefix/bin/tests/EventStore.Projections.Core.Tests.dll $EXCLUDE
fi

echo "##teamcity[importData type='nunit' path='$pathPrefix/TestResult.xml']"
