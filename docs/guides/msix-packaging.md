# MSIX Packaging & Signing (Windows Desktop)

This guide covers producing MSIX packages for the Windows Desktop app and signing them
for development or release distribution.

## Build MSIX Packages

**Makefile (Windows):**

```powershell
make desktop-publish
```

**PowerShell install script:**

```powershell
.\scripts\install\install.ps1 -Mode Desktop
```

Both commands output MSIX packages under:

```
dist\win-x64\msix\    (install script)
publish\desktop\      (make target)
```

## Optional AppInstaller File

To generate an AppInstaller alongside the MSIX package, provide the AppInstaller URI:

```powershell
$env:MDC_APPINSTALLER_URI = "https://example.com/market-data-collector/MarketDataCollector.appinstaller"
```

For `make`:

```powershell
set APPINSTALLER_URI=https://example.com/market-data-collector/MarketDataCollector.appinstaller
make desktop-publish
```

## Signing for Development (Self-Signed)

MSIX packages must be signed. For local development you can use a self-signed certificate.
Create one and export a PFX:

```powershell
$cert = New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=MarketDataCollector" `
  -KeyUsage DigitalSignature `
  -FriendlyName "MarketDataCollector Dev Certificate" `
  -CertStoreLocation "Cert:\CurrentUser\My"

$password = ConvertTo-SecureString -String "dev-password" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "$env:USERPROFILE\Documents\MarketDataCollector.Dev.pfx" -Password $password
```

Trust the certificate for local installs:

```powershell
Import-Certificate -FilePath "$env:USERPROFILE\Documents\MarketDataCollector.Dev.pfx" -CertStoreLocation "Cert:\CurrentUser\TrustedPeople"
```

Then pass the certificate to the build:

```powershell
$env:MDC_SIGNING_CERT_PFX = "$env:USERPROFILE\Documents\MarketDataCollector.Dev.pfx"
$env:MDC_SIGNING_CERT_PASSWORD = "dev-password"
```

## Signing for Release (Code-Signing Certificate)

For production distribution, use a trusted code-signing certificate from a CA:

1. Purchase a code-signing certificate (standard or EV).
2. Ensure the **Publisher** in `Package.appxmanifest` and
   `MarketDataCollector.Uwp.csproj` matches the certificate subject exactly.
3. Provide the PFX path and password via environment variables:

```powershell
$env:MDC_SIGNING_CERT_PFX = "C:\secure\MarketDataCollector.Release.pfx"
$env:MDC_SIGNING_CERT_PASSWORD = "<secure-password>"
```

For `make`, pass the same values:

```powershell
set SIGNING_CERT_PFX=C:\secure\MarketDataCollector.Release.pfx
set SIGNING_CERT_PASSWORD=<secure-password>
make desktop-publish
```

## Notes

- Keep the package identity values in the project file and manifest in sync.
- AppInstaller generation is optional; omit the URI to skip it.
