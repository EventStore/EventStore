@echo off
pushd %~dp0 || goto :error
cd ..\.. || goto :error

call :get-v8 || goto :error
call :get-python || goto :error
call :get-gyp || goto :error
call :get-cygwin || goto :error

popd || goto :error

goto :EOF

:error
echo %ERRORLEVEL%
echo FAILED. See previous messages
exit /b 1

:a8140cb930617054ed0487e6e1287c81cd51718e

:get-v8
  call :get-from-git V8 https://github.com/v8/v8.git v8 0140fb7c08054e6ef1bdfeffebd2eff7b57749ab || goto :error
exit /b 0

:get-python
  call :get-from-svn Python http://src.chromium.org/svn/trunk/tools/third_party/python_26@89111 v8\third_party\python_26 || goto :error
exit /b 0

:get-gyp
  call :get-from-svn Gyp http://gyp.googlecode.com/svn/trunk v8\build\gyp
exit /b 0

:get-cygwin
  call :get-from-svn CygWin http://src.chromium.org/svn/trunk/deps/third_party/cygwin@66844 v8\third_party\cygwin || goto :error
exit /b 0

:get-from-svn
  set to=%3
  set from=%2
  set what=%1

  pushd . || goto :error
  if not exist %to% (
      echo Checking out %what% ...
      svn co %from% %to% || goto :error
      cd %to% || goto :error
  ) else (
      cd %to% || goto :error
      echo Updating %what% ...
      svn up > nul || goto :error
  )
  popd || goto :error

exit /b 0

:get-from-git

  set to=%3
  set from=%2
  set what=%1
  set rev=%4

  pushd . || goto :error
  if not exist %to% (
      echo Checking out %what% ...
      call git clone  %from% %to% || goto :error
      cd %to% || goto :error
  ) else (
      cd %to% || goto :error
      echo Updating %what% ...
      call git checkout master || goto :error
      call git pull || goto :error
  )
  call git checkout %rev%
  popd || goto :error

exit /b 0
