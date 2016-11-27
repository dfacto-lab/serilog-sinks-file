#!/bin/bash
dotnet restore --no-cache
for path in src/*/project.json; do
    dirname="$(dirname "${path}")"
    dotnet build ${dirname} -f netstandard1.3 -c Release
done

for path in test/*.Tests/project.json; do
    dirname="$(dirname "${path}")"
    dotnet build ${dirname} -f netcoreapp1.0 -c Release
    dotnet test ${dirname} -f netcoreapp1.0  -c Release
done
