#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore
dotnet test --no-restore --configuration Release
dotnet publish src/FirmaElectronica.Web --no-restore --configuration Release --output artifacts/publish
if [[ -e artifacts/publish/appsettings.Local.json || -d artifacts/publish/App_Data ]]; then
  echo 'ERROR: el paquete contiene configuración o datos locales.' >&2
  exit 1
fi
