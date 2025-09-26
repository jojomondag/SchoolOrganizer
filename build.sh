#!/bin/bash

# Build script for SchoolOrganizer
# Cleans, builds, and runs the Avalonia application

echo "🧹 Cleaning project..."
dotnet clean

echo ""
echo "🔨 Building project..."
dotnet build

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Build successful! Starting application..."
    echo ""
    dotnet run
else
    echo ""
    echo "❌ Build failed! Please check the errors above."
    exit 1
fi
