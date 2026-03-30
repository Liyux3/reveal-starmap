#!/usr/bin/env bash
set -euo pipefail

LOG_PATH="${ONI_LOG_PATH:-$HOME/Library/Logs/Klei/Oxygen Not Included/Player.log}"

mkdir -p "$(dirname "$LOG_PATH")"
touch "$LOG_PATH"

tail -n 200 -f "$LOG_PATH"
