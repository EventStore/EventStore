@echo off

netsh http add urlacl url=http://127.0.0.1:2111/ user=%USERDOMAIN%\%USERNAME%
netsh http add urlacl url=http://127.0.0.1:2112/ user=%USERDOMAIN%\%USERNAME%
netsh http add urlacl url=http://127.0.0.1:2113/ user=%USERDOMAIN%\%USERNAME%
netsh http add urlacl url=http://127.0.0.1:30777/ user=%USERDOMAIN%\%USERNAME%