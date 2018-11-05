FROM centos:centos7

RUN yum install -y git bzip2 make glib2-devel-2.54.2 at-spi2-core-devel-2.22.0 glibc-devel-2.17 gcc-4.8.5

ADD build-js1-linux /build-js1-linux
ENTRYPOINT ["/build-js1-linux/build-js1-linux.sh"]