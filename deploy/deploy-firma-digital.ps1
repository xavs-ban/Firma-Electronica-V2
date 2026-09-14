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
function Set-PoolState([string]$Name, [string]$Desired) {
  $limite = [DateTime]::UtcNow.AddSeconds(90)
  $enviado = $false
  while ([DateTime]::UtcNow -lt $limite) {
    $estado = (Get-WebAppPoolState $Name).Value
    if ($estado -eq $Desired) { return }
    if (!$enviado -and $estado -in @('Started', 'Stopped')) {
      $enviado = $true
      if ($Desired -eq 'Stopped') { Stop-WebAppPool $Name }
      else { Start-WebAppPool $Name }
    }
    Start-Sleep -Milliseconds 500
  }
  throw "El pool $Name no alcanzo $Desired en 90 segundos. Estado: $estado"
}
if (!(Test-Path $SecretsPath)) { throw "Falta completar la configuracion privada: $SecretsPath" }
# Validar antes de compilar; StrictMode no permite acceder a propiedades ausentes.
try { $config = Get-Content $SecretsPath -Raw | ConvertFrom-Json -ErrorAction Stop }
catch { throw "No se pudo leer un JSON valido en $SecretsPath. Revisa el archivo sin compartir sus credenciales." }
if ($null -eq $config -or $config -isnot [System.Management.Automation.PSCustomObject]) {
  throw "La configuracion debe ser un objeto JSON en $SecretsPath."
}
$faltantes = @()
foreach ($ruta in @('ConnectionStrings.Firma', 'Quiter.ClientId', 'Quiter.ClientSecret', 'Quiter.Code')) {
  $valor = $config
  foreach ($parte in $ruta.Split('.')) {
    if ($null -eq $valor) { break }
    $propiedad = $valor.PSObject.Properties[$parte]
    if ($null -eq $propiedad) { $valor = $null; break }
    $valor = $propiedad.Value
  }
  if ($valor -isnot [string] -or [string]::IsNullOrWhiteSpace($valor) -or $valor -match 'REEMPLAZAR') {
    $faltantes += $ruta
  }
}
if ($faltantes.Count -gt 0) {
  throw ("Completa estos campos en {0}: {1}. Usa deploy\iis-settings.example.json como estructura; no sobrescribas tus valores existentes." -f $SecretsPath, ($faltantes -join ', '))
}
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
    Set-PoolState $pool Stopped
    Set-ItemProperty "IIS:\Sites\$SiteName\firma-digital" physicalPath $release
  } else {
    New-WebApplication -Site $SiteName -Name 'firma-digital' -PhysicalPath $release -ApplicationPool $pool | Out-Null
  }
  Set-PoolState $pool Started
  if ((Get-Website -Name $SiteName).State -ne 'Started') { Start-Website $SiteName }
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
  $errorOriginal = $_
  if ($anterior) {
    try {
      Set-PoolState $pool Stopped
      Set-ItemProperty "IIS:\Sites\$SiteName\firma-digital" physicalPath $anterior
      Set-PoolState $pool Started
      Write-Warning "Restaurada version anterior: $anterior"
    } catch {
      Write-Warning ("No se pudo restaurar la version anterior: " + $_.Exception.Message)
    }
  }
  throw $errorOriginal
}
