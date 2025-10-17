#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

BUILD_INFO_FILE="${REPO_DIR}/src/MyShowtime.Client/BuildInfo.cs"

if [[ ! -f "${BUILD_INFO_FILE}" ]]; then
  echo "Build info file not found at ${BUILD_INFO_FILE}" >&2
  exit 1
fi

current_number="$(grep -oE 'BuildNumber\s*=\s*[0-9]+' "${BUILD_INFO_FILE}" | head -n1 | awk -F '=' '{print $2}' | tr -d '[:space:]')"

if [[ -z "${current_number}" ]]; then
  echo "Unable to read current build number from ${BUILD_INFO_FILE}" >&2
  exit 1
fi

next_number=$((current_number + 1))

tmp_file="$(mktemp)"
sed -E "s/(BuildNumber\s*=\s*)${current_number};/\1${next_number};/" "${BUILD_INFO_FILE}" > "${tmp_file}"
mv "${tmp_file}" "${BUILD_INFO_FILE}"

echo "Incremented build number: ${current_number} -> ${next_number}"

STAGING_DIR="$(mktemp -d)"
trap 'rm -rf "${STAGING_DIR}"' EXIT
API_OUTPUT="${STAGING_DIR}/api"
CLIENT_OUTPUT="${STAGING_DIR}/client"

echo "Publishing API to ${API_OUTPUT}"
dotnet publish "${REPO_DIR}/src/MyShowtime.Api/MyShowtime.Api.csproj" -c Release -o "${API_OUTPUT}"

echo "Publishing Client to ${CLIENT_OUTPUT}"
dotnet publish "${REPO_DIR}/src/MyShowtime.Client/MyShowtime.Client.csproj" -c Release -o "${CLIENT_OUTPUT}"

CLIENT_WWWROOT="${CLIENT_OUTPUT}/wwwroot"
if [[ ! -d "${CLIENT_WWWROOT}" ]]; then
  echo "Expected client publish directory ${CLIENT_WWWROOT} not found." >&2
  exit 1
fi

echo "Deploying API to /var/www/projects/MyShowtime/api/"
mkdir -p /var/www/projects/MyShowtime/api/
rsync -a --delete "${API_OUTPUT}/" /var/www/projects/MyShowtime/api/

echo "Deploying Client assets to /var/www/projects/MyShowtime/wwwroot/"
mkdir -p /var/www/projects/MyShowtime/wwwroot/
rsync -a --delete "${CLIENT_WWWROOT}/" /var/www/projects/MyShowtime/wwwroot/

echo "Build ${next_number} deployed successfully."
