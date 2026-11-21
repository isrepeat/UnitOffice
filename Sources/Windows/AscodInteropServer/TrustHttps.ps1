[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CertificateFile,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedSha256
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$actual = (Get-FileHash -LiteralPath $CertificateFile -Algorithm SHA256).Hash
if ($actual -ne $ExpectedSha256) { throw 'Certificate SHA256 does not match the server output.' }
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path -LiteralPath $CertificateFile).Path)
if ($certificate.HasPrivateKey) { throw 'Only the public CER certificate should be transferred.' }
if ($certificate.NotAfter -le (Get-Date)) { throw 'The certificate has expired.' }
Import-Certificate -FilePath $CertificateFile -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
Write-Output "Trusted certificate: $($certificate.Subject)"
Write-Output 'Close and reopen the chat tab using HTTPS. The page must have no certificate warning.'