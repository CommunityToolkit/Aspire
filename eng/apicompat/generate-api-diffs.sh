find "../../src" -type f -name "*.csproj" | while read -r csproj; do
    dotnet build "$csproj" -f net8.0 --configuration Release --no-incremental -t:Build -t:GenAPIGenerateReferenceAssemblySource
done