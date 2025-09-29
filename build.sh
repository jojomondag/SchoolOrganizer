#!/bin/bash

# SchoolOrganizer Build Script for macOS/Linux
# This script builds the project and handles any necessary cleanup

echo "Building SchoolOrganizer..."

# Clean previous build
echo "Cleaning previous build..."
dotnet clean

# Restore packages
echo "Restoring packages..."
dotnet restore

# Build the project in Debug mode for better debugging experience
echo "Building project in Debug mode..."
dotnet build --configuration Debug

# Check if build was successful
if [ $? -eq 0 ]; then
    echo "Build completed successfully!"
    echo "You can now run the application with: dotnet run"
else
    echo "Build failed. Please check the errors above."
    exit 1
fi
