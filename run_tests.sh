#!/usr/bin/env bash
while getopts "c:m:x:p" option
do
    case $option in
        c)
            CONFIGURATION=$OPTARG
            ;;
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
            echo "Usage: run_tests.sh [-c Configuration] [-x ExcludeCategories] [-m /path/to/mono] [-p]"
            echo "Defaults:"
            echo "   Configuration: release"
            echo "   Mono Path: /opt/mono"
            echo "   Exclude: None"
            echo "   projections: false"
            exit
            ;;
    esac
done

if [[ $CONFIGURATION == "" ]]; then
    CONFIGURATION="debug"
fi

if [[ $MONOPATH == "" ]]; then
    MONOPATH="/opt/mono"
fi

LD_LIBRARY_PATH=bin/eventstore/$CONFIGURATION/anycpu/:$MONOPATH/lib/:$LD_LIBRARY_PATH mono tools/nunit-2.6.3/bin/nunit-console.exe bin/eventstore.tests/$CONFIGURATION/anycpu/EventStore.Core.Tests.dll $EXCLUDE -xml=inter 
rc=$?
xsltproc tools/nunit-2.6.3/results.xslt inter
rm inter
if [[ $rc != 0 ]] ; then
    exit $rc
fi

if [[ $RUNPROJECTIONS == "TRUE" ]]; then
    LD_LIBRARY_PATH=bin/eventstore/$CONFIGURATION/anycpu/:$MONOPATH/lib/:$LD_LIBRARY_PATH mono tools/nunit-2.6.3/bin/nunit-console.exe bin/eventstore.tests/$CONFIGURATION/anycpu/EventStore.Projections.Core.Tests.dll $EXCLUDE
fi
