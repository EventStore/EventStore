FROM centos:7

RUN rpm -Uvh "https://packages.microsoft.com/config/rhel/7/packages-microsoft-prod.rpm"

RUN rpm --import "https://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
RUN curl -s https://download.mono-project.com/repo/centos7-stable.repo | tee /etc/yum.repos.d/mono-centos7-stable.repo

RUN yum update -y && yum install -y mono-devel-5.16.0.187-0.xamarin.3.epel7 msbuild-15.8+xamarinxplat.2018.07.31.22.43-0.xamarin.10.epel7 python pip unzip git "dotnet-sdk-2.1"