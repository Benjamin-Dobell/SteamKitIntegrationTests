#!/usr/bin/env bash

set -e

if [ -z "$STEAMKIT_REPO" ]; then
    STEAMKIT_REPO=https://github.com/SteamRE/SteamKit.git
fi

if [ -z "$STEAMKIT_REF" ]; then
    STEAMKIT_REF=master
fi

echo "Cloning $STEAMKIT_REPO:$STEAMKIT_REF..."
git clone --recursive -b "$STEAMKIT_REF" "$STEAMKIT_REPO" SteamKit

echo "Building integration tests..."
cd SteamKitIntegrationTests
dotnet restore
dotnet build /p:Configuration=Debug /p:EnableSourceLink=false

echo "Running integration tests..."
dotnet test --no-build
