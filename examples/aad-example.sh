#!/bin/bash
# Example: Running tests with Azure AD / Microsoft Entra ID authentication

# Configuration - update these values for your environment
LAUNCH_JSON_PATH="/path/to/your/project/.vscode/launch.json"
CONFIG_NAME="Your BC Cloud Config"

# Interactive AAD auth (browser flow)
# When no username/password is provided, an interactive browser window will open
echo "Running tests with interactive AAD auth..."
bcdev test \
  -launchJsonPath "$LAUNCH_JSON_PATH" \
  -launchJsonName "$CONFIG_NAME" \
  -authType AAD

# AAD with username/password (for CI/CD)
# Note: This requires the user account to have "Resource Owner Password Credentials" enabled
AAD_USERNAME="user@yourtenant.onmicrosoft.com"
AAD_PASSWORD="your_password"

echo "Running tests with AAD username/password..."
bcdev test \
  -launchJsonPath "$LAUNCH_JSON_PATH" \
  -launchJsonName "$CONFIG_NAME" \
  -authType AAD \
  -Username "$AAD_USERNAME" \
  -Password "$AAD_PASSWORD"

# Publishing with AAD auth
echo "Publishing app with AAD auth..."
bcdev publish \
  -appPath "/path/to/your/app.app" \
  -launchJsonPath "$LAUNCH_JSON_PATH" \
  -launchJsonName "$CONFIG_NAME" \
  -authType AAD
