@echo off
set PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319;%PATH%
set /p HB_HOME="enter Honorbuddy installation path(where honorbuddy.exe is located): "
msbuild HBRelog.csproj /t:NugetRestore;Rebuild /p:Configuration=Release /p:AllowUnsafeBlocks=true /p:ReferencePath=%HB_HOME%
pause