##!/usr/bin/env bash

export LD_LIBRARY_PATH=.:$LD_LIBRARY_PATH
./clusternode $@
