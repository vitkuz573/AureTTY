#!/usr/bin/env bash
set -euo pipefail

if ! command -v dotnet >/dev/null 2>&1; then
  echo "[linux-smoke] dotnet SDK is required." >&2
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "[linux-smoke] curl is required." >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "[linux-smoke] jq is required." >&2
  exit 1
fi

base_url="${AURETTY_BASE_URL:-http://127.0.0.1:17850}"
api_key="${AURETTY_API_KEY:-demo-linux-api-key}"
pipe_name="${AURETTY_PIPE_NAME:-demo-linux-pipe-$(date +%s)-$RANDOM}"
pipe_token="${AURETTY_PIPE_TOKEN:-demo-linux-pipe-token-$(date +%s)-$RANDOM}"
http_viewer_id="${AURETTY_HTTP_VIEWER_ID:-demo-http-viewer-$(date +%s)-$RANDOM}"
pipe_viewer_id="${AURETTY_PIPE_VIEWER_ID:-demo-pipe-viewer-$(date +%s)-$RANDOM}"
pipe_session_id="${AURETTY_PIPE_SESSION_ID:-demo-pipe-session-$(date +%s)-$RANDOM}"

log_dir="${AURETTY_SMOKE_LOG_DIR:-/tmp/auretty-demos}"
mkdir -p "$log_dir"
server_log_path="${log_dir}/auretty-linux-smoke-server.log"

cleanup() {
  if [[ -n "${server_pid:-}" ]] && kill -0 "$server_pid" >/dev/null 2>&1; then
    kill "$server_pid" >/dev/null 2>&1 || true
    wait "$server_pid" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "[linux-smoke] Starting AureTTY (http + pipe)..."
dotnet run --project src/AureTTY/AureTTY.csproj -c Debug -- \
  --transport pipe --transport http \
  --http-listen-url "$base_url" \
  --api-key "$api_key" \
  --pipe-name "$pipe_name" \
  --pipe-token "$pipe_token" \
  >"$server_log_path" 2>&1 &
server_pid=$!

echo "[linux-smoke] Waiting for health endpoint..."
for _ in {1..120}; do
  if curl -sS --fail-with-body -H "X-AureTTY-Key: ${api_key}" "${base_url}/api/v1/health" >/dev/null 2>&1; then
    break
  fi

  if ! kill -0 "$server_pid" >/dev/null 2>&1; then
    echo "[linux-smoke] AureTTY exited during startup. Log: ${server_log_path}" >&2
    tail -n 200 "$server_log_path" >&2 || true
    exit 1
  fi

  sleep 0.25
done

if ! curl -sS --fail-with-body -H "X-AureTTY-Key: ${api_key}" "${base_url}/api/v1/health" >/dev/null 2>&1; then
  echo "[linux-smoke] Health endpoint did not become ready. Log: ${server_log_path}" >&2
  tail -n 200 "$server_log_path" >&2 || true
  exit 1
fi

echo "[linux-smoke] Running HTTP demo..."
AURETTY_BASE_URL="$base_url" \
AURETTY_API_KEY="$api_key" \
AURETTY_VIEWER_ID="$http_viewer_id" \
bash demos/http/http-demo.sh

echo "[linux-smoke] Running pipe demo..."
dotnet run --project demos/pipe/AureTTY.Demo.PipeClient/AureTTY.Demo.PipeClient.csproj -- \
  --pipe-name "$pipe_name" \
  --pipe-token "$pipe_token" \
  --viewer-id "$pipe_viewer_id" \
  --session-id "$pipe_session_id" \
  --shell bash

echo "[linux-smoke] Verifying no leaked sessions..."
remaining_sessions="$(curl -sS --fail-with-body -H "X-AureTTY-Key: ${api_key}" "${base_url}/api/v1/sessions" | jq 'length')"
if [[ "$remaining_sessions" != "0" ]]; then
  echo "[linux-smoke] Expected zero active sessions, got ${remaining_sessions}." >&2
  exit 1
fi

echo "[linux-smoke] Completed successfully. Server log: ${server_log_path}"
