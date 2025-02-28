for /r "..\..\src" %%i in (*.csproj) do (  
    dotnet build "%%i" -f net8.0 --configuration Release --no-incremental /t:"Build;GenAPIGenerateReferenceAssemblySource"  
)  