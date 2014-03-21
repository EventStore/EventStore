#!/usr/bin/env bash

while getopts "c:m:x:" option
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
        ?)
            echo "Usage: run_tests.sh [-c Configuration] [-x ExcludeCategories] [-m /path/to/mono]"
            echo "Defaults:"
            echo "   Configuration: release"
            echo "   Mono Path: /opt/mono"
            echo "   Exclude: None"
            exit
            ;;
    esac
done

if [[ $CONFIGURATION == "" ]]; then
    CONFIGURATION="release"
fi

if [[ $MONOPATH == "" ]]; then
    MONOPATH="/opt/mono"
fi

LD_LIBRARY_PATH=$MONOPATH/lib:$LD_LIBRARY_PATH mono tools/nunit-2.6.3/bin/nunit-console.exe bin/eventstore.tests/$CONFIGURATION/anycpu/*.Tests.dll $EXCLUDE
