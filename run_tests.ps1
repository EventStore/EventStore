.\tools\nunit-3.4.1\bin\nunit3-console.exe bin\tests\EventStore.BufferManagement.Tests.dll bin\tests\EventStore.Core.Tests.dll bin\tests\EventStore.Projections.Core.Tests.dll /framework:net-4.5 /timeout=60000

$result = Select-String -Path TestResult.xml -Pattern "result=`"Failed`""

if ([string]::IsNullOrEmpty($result) -eq "True"){
   $host.SetShouldExit(0)
}else{
   $host.SetShouldExit(1)
}
