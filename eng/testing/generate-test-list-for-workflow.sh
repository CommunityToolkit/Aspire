#! /bin/bash

INDENT="            "

echo "${INDENT}# Hosting integration tests"
find tests -type f -name '*Hosting.*.Tests.csproj' | sort | while read -r file; do
    base=$(basename "$file" .csproj)
    echo "${INDENT}${base#CommunityToolkit.Aspire.},"
done

echo

echo "${INDENT}# Client integration tests"
find tests -type f -name '*.Tests.csproj' | grep -v Hosting | sort | while read -r file; do
    base=$(basename "$file" .csproj)
    echo "${INDENT}${base#CommunityToolkit.Aspire.},"
done