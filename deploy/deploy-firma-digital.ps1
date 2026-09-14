#requires -RunAsAdministrator
[CmdletBinding()]
param(
  [string]$RepoPath = 'C:\Plataformas\Firma-Electronica-V2',
  [string]$BasePath = 'C:\Plataformas\FirmaDigital',
  [string]$SecretsPath = 'C:\Plataformas\secrets\FirmaDigital\appsettings.Production.json',
  [string]$SiteName = 'Firma Digital V2',
  [int]$Port = 3366
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module WebAdministration
function Invoke-Checked([string]$File, [string[]]$Arguments) {
  & $File @Arguments
  if ($LASTEXITCODE -ne 0) { throw "$File termino con codigo $LASTEXITCODE" }
}
if (!(Test-Path $SecretsPath)) { throw "Falta completar la configuracion privada: $SecretsPath" }
if (!(Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")) {
  throw 'Instala el Hosting Bundle de ASP.NET Core 10 para IIS antes de continuar.'
}
if (!(Test-Path $RepoPath)) {
  Invoke-Checked git @('clone', 'https://github.com/xavs-ban/Firma-Electronica-V2.git', $RepoPath)
}
Push-Location $RepoPath
try {
  if ((git remote get-url origin) -ne 'https://github.com/xavs-ban/Firma-Electronica-V2.git') { throw 'El remoto no corresponde a Firma V2.' }
  if (git status --porcelain) { throw 'Hay cambios locales en el repositorio. Revisalos antes de desplegar.' }
  if ((git branch --show-current) -ne 'main') { throw 'El repositorio debe estar en main.' }
  Invoke-Checked git @('pull', '--ff-only', 'origin', 'main')
  $commit = (git rev-parse --short HEAD).Trim()
  $release = Join-Path $BasePath ('releases\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $commit)
  Invoke-Checked dotnet @('test', '-c', 'Release')
  Invoke-Checked dotnet @('publish', 'src\FirmaElectronica.Web', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-o', $release)
} finally { Pop-Location }
if (Test-Path "$release\appsettings.Local.json") { throw 'El paquete contiene configuracion local inesperada.' }
$config = Get-Content $SecretsPath -Raw | ConvertFrom-Json
if (!$config.ConnectionStrings.Firma -or !$config.Quiter.ClientId -or !$config.Quiter.ClientSecret -or !$config.Quiter.Code) { throw 'Completa SQL y Quiter en la configuracion privada.' }
if (($config | ConvertTo-Json -Depth 20) -match 'REEMPLAZAR') { throw 'La configuracion contiene valores REEMPLAZAR.' }
$data = Join-Path $BasePath 'data'
$root = Join-Path $BasePath 'site-root'
New-Item -ItemType Directory -Force -Path $data,$root | Out-Null
$config | Add-Member -Force NoteProperty Hosting (@{ HttpInterno = $true })
$config | Add-Member -Force NoteProperty Intentos (@{ Carpeta = "$data\intentos" })
$config | Add-Member -Force NoteProperty DataProtection (@{ Carpeta = "$data\keys" })
$config | ConvertTo-Json -Depth 20 | Set-Content "$release\appsettings.Production.json" -Encoding UTF8
$pool = 'FirmaDigitalV2'
if (!(Test-Path "IIS:\AppPools\$pool")) { New-WebAppPool $pool | Out-Null }
Set-ItemProperty "IIS:\AppPools\$pool" managedRuntimeVersion ''
Set-ItemProperty "IIS:\AppPools\$pool" enable32BitAppOnWin64 $false
Invoke-Checked icacls @($data, '/inheritance:r', '/grant:r', '*S-1-5-18:(OI)(CI)F', '*S-1-5-32-544:(OI)(CI)F', "IIS AppPool\${pool}:(OI)(CI)M")
Invoke-Checked icacls @($release, '/inheritance:r', '/grant:r', '*S-1-5-18:(OI)(CI)F', '*S-1-5-32-544:(OI)(CI)F', "IIS AppPool\${pool}:(OI)(CI)RX")
$site = Get-Website | Where-Object Name -eq $SiteName
if (!$site) {
  $ocupado = Get-WebBinding | Where-Object { $_.bindingInformation -match ":${Port}:" }
  if ($ocupado) { throw "El puerto $Port pertenece a otro sitio. No se modifico ese sitio." }
  New-Website -Name $SiteName -Port $Port -PhysicalPath $root | Out-Null
} elseif (!($site.bindings.Collection | Where-Object { $_.protocol -eq 'http' -and $_.bindingInformation -eq "*:${Port}:" })) {
  throw 'El sitio existente tiene bindings diferentes. Revisar antes de cambiarlo.'
}
$app = Get-WebApplication -Site $SiteName -Name 'firma-digital'
$anterior = if ($app) { $app.physicalPath } else { $null }
if ($app -and $app.applicationPool -ne $pool) { throw 'La aplicacion existente usa otro pool. Revisar antes de continuar.' }
try {
  if ($app) {
    Stop-WebAppPool $pool
    Set-ItemProperty "IIS:\Sites\$SiteName\firma-digital" physicalPath $release
  } else {
    New-WebApplication -Site $SiteName -Name 'firma-digital' -PhysicalPath $release -ApplicationPool $pool | Out-Null
  }
  if ((Get-WebAppPoolState $pool).Value -ne 'Started') { Start-WebAppPool $pool }
  Start-Website $SiteName
  $ok = $false
  for ($i=0; $i -lt 12; $i++) {
    try {
      $r = Invoke-WebRequest "http://localhost:$Port/firma-digital/" -UseBasicParsing -TimeoutSec 10
      if ($r.StatusCode -eq 200 -and $r.Content -match 'firma-base') { $ok = $true; break }
    } catch { Start-Sleep -Seconds 2 }
  }
  if (!$ok) { throw 'El login no respondio correctamente despues del despliegue.' }
  Write-Host "Publicado $commit en http://10.0.128.73:$Port/firma-digital" -ForegroundColor Green
} catch {
  if ($anterior) {
    Stop-WebAppPool $pool -ErrorAction SilentlyContinue
    Set-ItemProperty "IIS:\Sites\$SiteName\firma-digital" physicalPath $anterior
    Start-WebAppPool $pool
    Write-Warning "Restaurada version anterior: $anterior"
  }
  throw
}
