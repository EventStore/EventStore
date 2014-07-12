#!/bin/sh
notify-send "Auto Started" "" -i $PWD/tools/autotest-images/circleWIN.png -t 1000
while true
do
      if [ ! -z $(inotifywait --recursive -qre modify --format "%w%f" ./ | grep -v -f includes) ]
          then
	  sleep .25
          /opt/mono/bin/xbuild src/EventStore.sln /p:Configuration=Debug /verbosity:0 /nologo
          if [ $? -eq 0 ]
            then
               ./run_tests.sh -m /opt/mono -x LongRunning 
                if [ $? -eq 0 ]
                then
                      notify-send "Passed" "Tests Passed" -i $PWD/tools/autotest-images/circleWIN.png -t 1000
                else
                      notify-send "Failed" "Tests Failed" -i $PWD/tools/autotest-images/circleFAIL.png -t 1000
                fi
            else
                notify-send "Failed" "Build Failed" -i $PWD/tools/autotest-images/circleFAIL.png -t 1000
          fi 
      fi
done
