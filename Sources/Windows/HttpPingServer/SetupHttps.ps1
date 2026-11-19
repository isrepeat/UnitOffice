#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [System.Net.IPAddress]$Address = '192.168.202.46',
    [string]$ServerUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name,
    [string]$RemoteSubnet = '192.168.202.0/24',
    [string]$CertificateDirectory = "$env:USERPROFILE\Downloads\UnitOffice.HttpPingServer"
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
if ($Address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
    throw 'An IPv4 address is required.'
}
$endpoint = "${Address}:8443"
$url = "https://${endpoint}/"
$existingBinding = & netsh http show sslcert "ipport=$endpoint" 2>&1
$hasBinding = ($existingBinding -join "`n") -match [regex]::Escape($endpoint)
$friendlyName = "UnitOffice.HttpPingServer HTTPS $Address"
$certificate = Get-ChildItem Cert:\LocalMachine\My | Where-Object {
    $_.FriendlyName -eq $friendlyName -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(7)
} | Sort-Object NotAfter -Descending | Select-Object -First 1
if (!$certificate) {
    $certificate = New-SelfSignedCertificate -Type Custom -Subject "CN=$Address" `
        -FriendlyName $friendlyName -CertStoreLocation Cert:\LocalMachine\My `
        -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable `
        -KeyUsage DigitalSignature, KeyEncipherment -NotAfter (Get-Date).AddMonths(12) `
        -TextExtension @("2.5.29.17={text}IPAddress=$Address", '2.5.29.37={text}1.3.6.1.5.5.7.3.1', '2.5.29.19={text}ca=0')
}
New-Item -ItemType Directory -Path $CertificateDirectory -Force | Out-Null
if ($hasBinding -and (($existingBinding -join "`n") -notmatch $certificate.Thumbprint -or
    ($existingBinding -join "`n") -notmatch 'b582509a-99a0-484d-82ed-d9dc1a4342bf')) {
    throw "An unrelated SSL binding exists for $endpoint. No existing binding was changed."
}
$certificateFile = Join-Path $CertificateDirectory 'UnitOffice.HttpPingServer.cer'
Export-Certificate -Cert $certificate -FilePath $certificateFile -Force | Out-Null
Import-Certificate -FilePath $certificateFile -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
if (!$hasBinding) {
    & netsh http add sslcert "ipport=$endpoint" "certhash=$($certificate.Thumbprint)" 'appid={b582509a-99a0-484d-82ed-d9dc1a4342bf}' 'certstorename=MY'
    if ($LASTEXITCODE -ne 0) { throw 'Could not register the HTTPS certificate binding.' }
}
$existingReservation = & netsh http show urlacl "url=$url" 2>&1
if (($existingReservation -join "`n") -notmatch [regex]::Escape($url)) {
    & netsh http add urlacl "url=$url" "user=$ServerUser" listen=yes
    if ($LASTEXITCODE -ne 0) { throw 'Could not reserve the HTTPS URL.' }
}
$ruleName = 'UnitOffice.HttpPingServer-HTTPS-8443'
if (!(Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name $ruleName -DisplayName 'UnitOffice HttpPingServer HTTPS' `
        -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8443 `
        -LocalAddress $Address.ToString() -RemoteAddress $RemoteSubnet -Profile Any | Out-Null
}
Write-Output "HTTPS configured: $url"
Write-Output "Public certificate for the second computer: $certificateFile"
Write-Output "SHA256: $((Get-FileHash -LiteralPath $certificateFile -Algorithm SHA256).Hash)"
Write-Output 'Start the server with: UnitOffice.HttpPingServer.exe'