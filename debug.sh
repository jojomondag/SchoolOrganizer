#!/bin/bash

# Debug script for SchoolOrganizer
# This script runs the application in debug mode with optimized settings

echo "🔧 Starting SchoolOrganizer in Debug mode..."
echo "This should eliminate the SVG debugging warnings."
echo ""

# Set environment variables to suppress debugging warnings
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export DOTNET_EnableDiagnostics=0

# Run the application
dotnet run --configuration Debug
