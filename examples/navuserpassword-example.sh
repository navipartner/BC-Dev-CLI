#!/bin/bash
# Example: Running tests with NavUserPassword authentication

# Configuration - update these values for your environment
LAUNCH_JSON_PATH="/path/to/your/project/.vscode/launch.json"
CONFIG_NAME="Your BC Config Name"
BC_USERNAME="your_username"
BC_PASSWORD="your_password"

# Run all tests
echo "Running all tests..."
bcdev test \
  -launchJsonPath "$LAUNCH_JSON_PATH" \
  -launchJsonName "$CONFIG_NAME" \
  -Username "$BC_USERNAME" \
  -Password "$BC_PASSWORD"

# Run tests for a specific codeunit
echo "Running tests for codeunit 84003..."
bcdev test \
  -launchJsonPath "$LAUNCH_JSON_PATH" \
  -launchJsonName "$CONFIG_NAME" \
  -Username "$BC_USERNAME" \
  -Password "$BC_PASSWORD" \
  -CodeunitId 84003

# Run a specific test method
echo "Running specific test method..."
bcdev test \
  -launchJsonPath "$LAUNCH_JSON_PATH" \
  -launchJsonName "$CONFIG_NAME" \
  -Username "$BC_USERNAME" \
  -Password "$BC_PASSWORD" \
  -CodeunitId 84003 \
  -MethodName "TestPostDocument"

# Compile and publish an app
echo "Compiling and publishing app..."
bcdev publish \
  -recompile \
  -appJsonPath "/path/to/your/project/app.json" \
  -compilerPath "/path/to/alc.exe" \
  -launchJsonPath "$LAUNCH_JSON_PATH" \
  -launchJsonName "$CONFIG_NAME" \
  -Username "$BC_USERNAME" \
  -Password "$BC_PASSWORD"
