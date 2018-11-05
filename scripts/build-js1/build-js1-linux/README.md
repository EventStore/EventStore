# EventStore Projections Library
The purpose of this Docker image is to build Google's `v8` and EventStore's native `Projections` library for linux distributions.

The build image is based on CentOS 7 which uses `glibc` version 2.17. Other flavors of linux should also be compatible as long as they use a version of `glibc` >= 2.17. This can be verified by running `ldd --version`.

Currently pinned `v8` version: `branch-heads/7.0`  
EventStore branch to build from: `master`

## Instructions
1. Build the Docker image  
`sudo docker build . -t build-js1-linux`

2. Run the image. This should launch a script to do all the work.  
`sudo docker run --name build-js1-linux -it build-js1-linux`

3. Wait for the compilation to complete - this will take a while. If there are no errors, a file named `libjs1.so` should be present in `/build-js1-linux`. Copy it to your system as follows:  
`sudo docker cp build-js1-linux:/build-js1-linux/libjs1.so ./`

4. Commit `libjs1.so` in the EventStore repository under `src/libs/x64/linux`


## Useful links
- Official v8 repository - https://chromium.googlesource.com/v8/v8.git  
- v8 Embedding Guide - https://github.com/v8/v8/wiki/Getting-Started-with-Embedding  
- v8 API changes - http://bit.ly/v8-api-changes  
- v8 Blog - https://v8project.blogspot.com/