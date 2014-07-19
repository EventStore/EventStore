##!/usr/bin/env bash

LD_LIBRARY_PATH=.:$LD_LIBRARY_PATH MONO_GC_DEBUG=clear-at-gc ./clusternode $@
