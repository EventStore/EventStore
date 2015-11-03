#!/usr/bin/env bash

# File License:
#     Copyright 2015, Event Store LLP
#
#     Licensed under the Apache License, Version 2.0 (the "License");
#     you may not use this file except in compliance with the License.
#     You may obtain a copy of the License at
#
#         http://www.apache.org/licenses/LICENSE-2.0
#
#     Unless required by applicable law or agreed to in writing, software
#     distributed under the License is distributed on an "AS IS" BASIS,
#     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
#     See the License for the specific language governing permissions and
#     limitations under the License.
#
# This is a modified version of the Boundary script used for meter setup, also
# released under the Apache license. The original license is as follows:
#
#     Copyright 2011-2013, Boundary
#
#     Licensed under the Apache License, Version 2.0 (the "License");
#     you may not use this file except in compliance with the License.
#     You may obtain a copy of the License at
#
#         http://www.apache.org/licenses/LICENSE-2.0
#
#     Unless required by applicable law or agreed to in writing, software
#     distributed under the License is distributed on an "AS IS" BASIS,
#     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
#     See the License for the specific language governing permissions and
#     limitations under the License.

# getSystemInformation determines the likely distribution and version of the
# distribution we are running on, and sets the variables ES_DISTRO and 
# ES_DISTRO_VERSION for further use.
function getSystemInformation() {
    local ES_TMP_PLATFORM=""
    local ES_TMP_DISTRO=""
    local ES_TMP_DISTRO_VERSION=""
    
    if [ -f /etc/redhat-release ] ; then
        ES_TMP_PLATFORM=`cat /etc/redhat-release`
        ES_TMP_DISTRO=`echo $ES_TMP_PLATFORM | awk '{print $1}'`
        if [ "$ES_TMP_DISTRO" = "Fedora" ]; then
            ES_TMP_DISTRO="RHEL"
            ES_TMP_DISTRO_VERSION="6"
        else
            if [ "$ES_TMP_DISTRO" != "CentOS" ]; then
                if [ "$ES_TMP_DISTRO" = "Enterprise" ] || [ -f /etc/oracle-release ]; then
                    # Oracle "Enterprise Linux"/"Linux"
                    ES_TMP_DISTRO="Oracle"
                    ES_TMP_DISTRO_VERSION=`echo $ES_TMP_PLATFORM | awk '{print $7}'`
                elif [ "$ES_TMP_DISTRO" = "Red" ]; then
                    ES_TMP_DISTRO="RHEL"
                    ES_TMP_DISTRO_VERSION=`echo $ES_TMP_PLATFORM | awk '{print $7}'`
                elif [ "$ES_TMP_DISTRO" = "Scientific" ]; then
                    ES_TMP_DISTRO_VERSION=`echo $ES_TMP_PLATFORM | awk '{print $4}'`
                elif [ "$ES_TMP_DISTRO" = "SHMZ" ]; then
                    ES_TMP_DISTRO_VERSION=`echo $ES_TMP_PLATFORM | awk '{print $3}'`
                else
                    ES_TMP_DISTRO="unknown"
                    ES_TMP_PLATFORM="unknown"
                    ES_TMP_DISTRO_VERSION="unknown"
                fi
            elif [ "$ES_TMP_DISTRO" = "CentOS" ]; then
                if [ "`echo $ES_TMP_PLATFORM | awk '{print $2}'`" = "Linux" ]; then
                    # CentOS 7 now includes "Linux" in the release string...
                    ES_TMP_DISTRO_VERSION=`echo $ES_TMP_PLATFORM | awk '{print $4}'`
                else
                    ES_TMP_DISTRO_VERSION=`echo $ES_TMP_PLATFORM | awk '{print $3}'`
                fi
            fi
        fi
    elif [ -f /etc/SuSE-brand ] ; then
        ES_TMP_DISTRO=`cat /etc/SuSE-brand | head -n1`
        ES_TMP_DISTRO_VERSION=`cat /etc/SuSE-brand | sed -n '2p' | awk '{print $3}'`
    elif [ -f /etc/system-release ]; then
        ES_TMP_PLATFORM=`cat /etc/system-release | head -n 1`
        ES_TMP_DISTRO=`echo $ES_TMP_PLATFORM | awk '{print $1}'`
        ES_TMP_DISTRO_VERSION=`echo $ES_TMP_PLATFORM | awk '{print $5}'`
    elif [ -f /etc/lsb-release ] ; then
        #Ubuntu version lsb-release - https://help.ubuntu.com/community/CheckingYourUbuntuVersion
        . /etc/lsb-release
        ES_TMP_PLATFORM=$DISTRIB_DESCRIPTION
        ES_TMP_DISTRO=$DISTRIB_ID
        ES_TMP_DISTRO_VERSION=$DISTRIB_RELEASE
        if [ "$ES_TMP_DISTRO" = "LinuxMint" ]; then
            ES_TMP_DISTRO="Ubuntu"
            ES_TMP_DISTRO_VERSION="12.04"
        fi
    elif [ -f /etc/debian_version ] ; then
        #Debian Version /etc/debian_version - Source: http://www.debian.org/doc/manuals/debian-faq/ch-software.en.html#s-isitdebian
        ES_TMP_DISTRO="Debian"
        ES_TMP_DISTRO_VERSION=`cat /etc/debian_version`
        INFO="$ES_TMP_DISTRO $ES_TMP_DISTRO_VERSION"
        ES_TMP_PLATFORM=$INFO
    elif [ -f /etc/arch-release ] ; then
        ES_TMP_PLATFORM="Arch"
        ES_TMP_DISTRO="Arch"
        ES_TMP_DISTRO_VERSION=`uname -r`
    elif [ -f /etc/os-release ] ; then
        . /etc/os-release
        ES_TMP_PLATFORM=$PRETTY_NAME
        ES_TMP_DISTRO=$NAME
        ES_TMP_DISTRO_VERSION=$ES_TMP_DISTRO_VERSION_ID
    elif [ -f /etc/gentoo-release ] ; then
        ES_TMP_PLATFORM="Gentoo"
        ES_TMP_DISTRO="Gentoo"
        ES_TMP_DISTRO_VERSION=`cat /etc/gentoo-release | cut -d ' ' -f 5`
    elif [ -f /etc/alpine-release ] ; then
        ES_TMP_PLATFORM="Alpine"
        ES_TMP_DISTRO="Alpine"
        ES_TMP_DISTRO_VERSION=`cat /etc/alpine-release | cut -d '.' -f 1-2`
    else
        uname -sv | grep 'FreeBSD' > /dev/null
        if [ "$?" = "0" ]; then
            ES_TMP_PLATFORM="FreeBSD"
            ES_TMP_DISTRO="FreeBSD"
            ES_TMP_DISTRO_VERSION=`uname -r`
        else
            uname -sv | grep 'Darwin' > /dev/null
            if [ "$?" = "0" ]; then
                ES_TMP_PLATFORM="Darwin"
                ES_TMP_DISTRO="OSX"
                ES_TMP_DISTRO_VERSION=`sw_vers -productVersion | cut -f 1,2 -d . -`
            fi
        fi
    fi
    ES_DISTRO=`echo "$ES_TMP_DISTRO" | awk '{print tolower($0)}'`
    ES_DISTRO_VERSION=`echo "$ES_TMP_DISTRO_VERSION" | awk '{print tolower($0)}'`
    if [[ $ES_DISTRO == osx* ]] ; then
        ES_FRIENDLY_DISTRO=`echo "MacOSX$(echo $ES_DISTRO | cut -c4-)"`
    else
        ES_FRIENDLY_DISTRO=`echo "$(echo $ES_DISTRO | cut -c1 | tr '[:lower:]' '[:upper:]')$(echo $ES_DISTRO | cut -c2-)"`
    fi

}
