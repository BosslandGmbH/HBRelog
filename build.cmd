set PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319;%PATH%
msbuild HBRelog.csproj /t:NugetRestore;Build /p:Configuration=Release /p:AllowUnsafeItems=true /p:ReferencePath=%HB_HOME%
pause