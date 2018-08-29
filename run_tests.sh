#!/usr/bin/env bash
while getopts "m:x:f:i:p" option
do
    case $option in
        m)
            MONOPATH=$OPTARG 
            ;;   
        x)
            EXCLUDE="--where:cat!=$OPTARG"
            ;;
        i)
            INCLUDE="--where:cat==$OPTARG"
            ;;
        f)
            FILTER="--where:$OPTARG"
            ;;
        p)
            RUNPROJECTIONS="TRUE"
            ;;
        ?)
            echo "Usage: run_tests.sh [-i IncludeCategory] [-x ExcludeCategory] [-f nUnitFilter] [-m /path/to/mono] [-p]"
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

LD_LIBRARY_PATH=bin/tests:$MONOPATH/lib/:$LD_LIBRARY_PATH mono tools/nunit-3.8.0/nunit3-console.exe bin/tests/EventStore.Core.Tests.dll $EXCLUDE $INCLUDE $FILTER
rc=$?

if [[ $rc != 0 ]] ; then
    exit $rc
fi

if [[ $RUNPROJECTIONS == "TRUE" ]]; then
    LD_LIBRARY_PATH=bin/tests/:$MONOPATH/lib/:$LD_LIBRARY_PATH mono tools/nunit-3.8.0/nunit3-console.exe bin/tests/EventStore.Projections.Core.Tests.dll $EXCLUDE
fi
