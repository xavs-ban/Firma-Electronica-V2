[CmdletBinding()]
param(
  [string]$SecretsPath = 'C:\Plataformas\secrets\FirmaDigital\appsettings.Production.json',
  [string]$ArchivoCuenta,
  [switch]$Desactivar
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!(Test-Path -LiteralPath $SecretsPath)) { throw 'No existe la configuracion privada del servidor.' }
$config = Get-Content -LiteralPath $SecretsPath -Raw | ConvertFrom-Json
if ($Desactivar) {
  $modo = @{ Activo = $false; Usuario = ''; Contrasena = '' }
} else {
  if (!$ArchivoCuenta -or !(Test-Path -LiteralPath $ArchivoCuenta)) { throw 'Indica -ArchivoCuenta con el JSON privado de la cuenta compartida.' }
  $cuenta = Get-Content -LiteralPath $ArchivoCuenta -Raw | ConvertFrom-Json
  if (!$cuenta.PSObject.Properties['AccesoTemporal']) { throw 'El archivo no contiene AccesoTemporal.' }
  $modo = $cuenta.AccesoTemporal
  foreach ($campo in @('Usuario','Contrasena')) {
    if (!$modo.PSObject.Properties[$campo] -or $modo.$campo -isnot [string] -or [string]::IsNullOrWhiteSpace($modo.$campo)) { throw "Falta $campo en el archivo privado." }
  }
  $modo | Add-Member -Force NoteProperty Activo $true
}
$config | Add-Member -Force NoteProperty AccesoTemporal $modo
$config | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $SecretsPath -Encoding UTF8
Write-Host 'Configuracion temporal guardada sin cambiar SQL ni Quiter. Ejecuta el despliegue habitual para aplicarla.'
