#!/bin/sh

set -e
dotnet --info
dotnet --list-sdks
dotnet restore

echo "🤖 Attempting to build..."
for path in src/**/*.csproj; do
    dotnet build -f net8 -c Release ${path}8
done

echo "🤖 Running tests..."
for path in test/*.Tests/*.csproj; do
    dotnet test -f net8  -c Release ${path}
done
