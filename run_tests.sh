#!/usr/bin/env bash
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

LD_LIBRARY_PATH=bin/tests:$MONOPATH/lib/:$LD_LIBRARY_PATH mono tools/nunit-3.4.1/bin/nunit3-console.exe bin/tests/EventStore.Core.Tests.dll $EXCLUDE
rc=$?
# xsltproc tools/nunit-3.4.1/results.xslt TestResult.xml
# rm inter
if [[ $rc != 0 ]] ; then
    exit $rc
fi

if [[ $RUNPROJECTIONS == "TRUE" ]]; then
    LD_LIBRARY_PATH=bin/tests/:$MONOPATH/lib/:$LD_LIBRARY_PATH mono tools/nunit-3.4.1/bin/nunit3-console.exe bin/tests/EventStore.Projections.Core.Tests.dll $EXCLUDE
fi
