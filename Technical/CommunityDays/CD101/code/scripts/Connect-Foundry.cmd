@echo off
rem Edit these two values before running this script from Command Prompt.
set "Foundry__TenantId=1ea1357c-191e-4475-b015-c34ed0745b5f"
set "FOUNDRY_SUBSCRIPTION_ID=b4a47141-6cca-4c11-ac61-ff493d02f20c"

if "%Foundry__TenantId%"=="" goto missing_values
if "%FOUNDRY_SUBSCRIPTION_ID%"=="" goto missing_values

where az >nul 2>&1
if errorlevel 1 (
    echo ERROR: Azure CLI was not found. Install it and reopen Command Prompt.
    exit /b 1
)

rem No setlocal: the app must inherit these settings from this terminal.
set "AZURE_EXTENSION_DIR=%TEMP%\sourcing-azure-cli-extensions"

echo Sign in with the account that has access to your Foundry project.
call az login --tenant "%Foundry__TenantId%"
if errorlevel 1 (
    echo ERROR: Azure login failed. Authentication setup did not complete.
    exit /b 1
)

call az account set --subscription "%FOUNDRY_SUBSCRIPTION_ID%"
if errorlevel 1 (
    echo ERROR: Could not select the subscription. Check its ID and your account access.
    exit /b 1
)

call az account show --query "{account:user.name,tenant:tenantId,subscription:id}" --output json
if errorlevel 1 (
    echo ERROR: Could not verify the selected Azure account.
    exit /b 1
)

set "AZURE_TOKEN_CREDENTIALS=AzureCliCredential"
echo.
echo Authentication setup complete. Restart the app from this same Command Prompt.
echo Foundry project permissions are still required; login alone does not verify agent access.
exit /b 0

:missing_values
echo ERROR: Edit the tenant and subscription IDs at the top of scripts\Connect-Foundry.cmd.
exit /b 1
