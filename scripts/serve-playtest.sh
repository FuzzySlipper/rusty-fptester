#!/usr/bin/env bash
set -euo pipefail
FPTESTER_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$FPTESTER_ROOT"
exec rusty dev --project src/FpTester.Game/FpTester.Game.csproj --live-debug "$@"
