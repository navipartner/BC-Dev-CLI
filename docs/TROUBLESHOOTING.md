# Troubleshooting Guide

## Common Issues

### Connection Refused / Cannot Connect to Server

**Symptoms:**
```json
{
  "success": false,
  "error": "Connection refused"
}
```

**Solutions:**
1. Verify the BC server is running and accessible
2. Check the `server` and `port` in your launch.json
3. Ensure firewall allows connections to the BC port (typically 7049)
4. For Docker containers, verify the container is running: `docker ps`

### Invalid Credentials

**Symptoms:**
```json
{
  "success": false,
  "error": "Invalid credentials"
}
```

**Solutions:**
1. Verify username and password are correct
2. For UserPassword auth, ensure the user exists in BC
3. For AAD auth, verify the user has appropriate permissions
4. Check for special characters in password - may need escaping in shell

### Test Page Not Found (Page 130455)

**Symptoms:**
```json
{
  "success": false,
  "error": "Cannot open page 130455"
}
```

**Solutions:**
1. Install the Microsoft Test Framework app in BC
2. Verify the Test Tool extension is published and installed
3. Check that the user has permissions to access the test page

### SSL Certificate Errors

**Symptoms:**
```
SSL certificate verification failed
```

**Solutions:**
The CLI automatically disables SSL verification for development environments. If you still encounter issues:
1. Ensure the BC server's SSL certificate is valid
2. For production, use a valid SSL certificate
3. The CLI uses `ServerCertificateCustomValidationCallback` to bypass cert errors

### Compiler Not Found

**Symptoms:**
```json
{
  "success": false,
  "message": "Compiler not found"
}
```

**Solutions:**
1. Verify the path to alc.exe is correct
2. Common locations:
   - VS Code extension: `~/.vscode/extensions/ms-dynamics-smb.al-*/bin/alc.exe`
   - BC installation: `C:\Program Files (x86)\Microsoft Dynamics 365 Business Central\*\AL Development Environment\alc.exe`
3. Ensure the file exists and is executable

### App.json Parsing Failed

**Symptoms:**
```json
{
  "success": false,
  "message": "Failed to parse app.json"
}
```

**Solutions:**
1. Verify the app.json file exists at the specified path
2. Check the JSON syntax is valid
3. Ensure required fields are present: `id`, `name`, `publisher`, `version`

### Launch.json Configuration Not Found

**Symptoms:**
```json
{
  "success": false,
  "error": "Configuration 'MyConfig' not found in launch.json"
}
```

**Solutions:**
1. Verify the configuration name matches exactly (case-sensitive)
2. Check the launch.json syntax is valid
3. List available configurations by examining the launch.json file

### Timeout During Test Execution

**Symptoms:**
```json
{
  "success": false,
  "error": "ClientSession timed out"
}
```

**Solutions:**
1. Increase the timeout: `-timeout 60` (minutes)
2. Check if BC server is responding slowly
3. Consider running a smaller subset of tests: `-CodeunitId 12345`
4. Check BC server performance and resources

### BC Client DLL Not Found

**Symptoms:**
```
Could not load file or assembly 'Microsoft.Dynamics.Framework.UI.Client'
```

**Solutions:**
1. Verify the libs folder exists next to the bcdev executable
2. Check that `Microsoft.Dynamics.Framework.UI.Client.dll` is in the libs folder
3. Use `-bcClientDllPath` to specify a custom path
4. Ensure the DLL version is compatible with your BC version

### AAD Authentication Errors

**Symptoms:**
```json
{
  "success": false,
  "error": "AAD authentication failed"
}
```

**Solutions:**
1. For interactive flow, ensure a browser is available
2. For username/password flow, verify the user has ROPC enabled
3. Check the AAD tenant configuration
4. Verify the user has permissions in the BC environment

## Debugging

### Enable Verbose Output

The CLI outputs debug information to stderr. Redirect stderr to see details:

```bash
bcdev test -launchJsonPath ".vscode/launch.json" -launchJsonName "MyConfig" -Username "admin" -Password "secret" 2>&1
```

### Check JSON Output

All commands output structured JSON. Pipe to `jq` for formatting:

```bash
bcdev test ... | jq .
```

### Test Network Connectivity

```bash
# Check if BC server is reachable
curl -v http://bcserver:7049/BC/

# For HTTPS
curl -vk https://bcserver:7049/BC/
```

## Getting Help

1. Run `bcdev --help` for general help
2. Run `bcdev <command> --help` for command-specific help
3. Check the [GitHub Issues](https://github.com/your-repo/issues) for known issues
4. Review the examples in the `examples/` folder
