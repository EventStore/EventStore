#!/usr/bin/env bash
test_result=$(grep failures=\"0\" TestResult.xml)
if [[ "$test_result" == "" ]] ; then
    echo "There are test failures"
    exit 1
else
    echo "All tests passed"
    exit 0
fi
