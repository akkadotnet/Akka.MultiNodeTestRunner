git commit -a -m "Updated package version"

cd C:\Projects\Upwork\Aaron\Akka.MultiNodeTestRunner\src\Akka.MultiNodeTestRunner.VisualStudio\bin\Debug
del /s *.nupkg
del /s *.snupkg

cd C:\Projects\Upwork\Aaron\Akka.MultiNodeTestRunner
dotnet pack src\Akka.MultiNodeTestRunner.VisualStudio\Akka.MultiNodeTestRunner.VisualStudio.csproj

cd C:\LocalNuget
del /s *.nupkg

cd C:\Projects\Upwork\Aaron\Akka.MultiNodeTestRunner\src\Akka.MultiNodeTestRunner.VisualStudio\bin\Debug
dotnet nuget push *.nupkg --source c:\LocalNuget

cd ..\..\..\Akka.MultiNodeTestRunner.SampleTests
dotnet remove package Akka.MultiNodeTestRunner.VisualStudio
dotnet add package Akka.MultiNodeTestRunner.VisualStudio -v 2.4.2-*

cd C:\Projects\Upwork\Aaron\Akka.MultiNodeTestRunner

pause