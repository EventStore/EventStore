#!/usr/bin/env bash
test_result=$(grep result=\"Failed\" TestResult.xml)
if [[ "$test_result" == "" ]] ; then
    echo "All tests passed"
    exit 0
else
    echo "There are test failures"
    exit 1
fi
