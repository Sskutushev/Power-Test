#!/usr/bin/env bash
# Runs every gate CI runs, locally, in one command.
#
# The point is that a reviewer can prove the repository's claims without reading the workflow file.
# Pass --skip-docker when Docker is unavailable.

set -euo pipefail

cd "$(dirname "$0")/.."

SKIP_DOCKER=0
[[ "${1:-}" == "--skip-docker" ]] && SKIP_DOCKER=1

step() {
    printf '\n\033[36m→ %s\033[0m\n' "$1"
}

step 'Restore'
dotnet restore WeatherApp.slnx

step 'Format'
dotnet format WeatherApp.slnx --verify-no-changes --no-restore

step 'Build (warnings are errors)'
dotnet build WeatherApp.slnx --no-restore --configuration Release

step 'Tests'
dotnet test WeatherApp.slnx --no-build --configuration Release

step 'Vulnerable packages'
if dotnet list WeatherApp.slnx package --vulnerable --include-transitive | grep -q 'has the following vulnerable packages'; then
    echo 'vulnerable packages found' >&2
    exit 1
fi

step 'No credential in the working tree'
# The provider credential is a 31-character hex string; documentation is excluded because it discusses
# the credential without containing one.
if git grep -nIE '[0-9a-f]{31}' -- ':!*.md' ':!docs/**'; then
    echo 'possible credential found' >&2
    exit 1
fi

if [[ "$SKIP_DOCKER" -eq 0 ]]; then
    step 'Container image'
    docker build -t weather-app:verify .

    step 'Image runs as a non-root user'
    user="$(docker image inspect weather-app:verify --format '{{.Config.User}}')"
    if [[ -z "$user" || "$user" == "root" || "$user" == "0" ]]; then
        echo "image user is '$user'" >&2
        exit 1
    fi

    step 'No credential baked into the image'
    if docker image inspect weather-app:verify --format '{{json .Config.Env}}' | grep -qi 'credential=.'; then
        echo 'credential present in image environment' >&2
        exit 1
    fi
fi

printf '\n\033[32mAll gates passed.\033[0m\n'
