#!/usr/bin/env bash
set -euo pipefail

if ! command -v curl >/dev/null 2>&1; then
  echo "[http-demo] curl is required." >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "[http-demo] jq is required." >&2
  exit 1
fi

base_url="${AURETTY_BASE_URL:-http://127.0.0.1:17850}"
api_key="${AURETTY_API_KEY:-auretty-terminal}"
viewer_id="${AURETTY_VIEWER_ID:-demo-http-$(date +%s)}"
session_id="${AURETTY_SESSION_ID:-demo-http-session-$(date +%s)-$RANDOM}"

request() {
  local method="$1"
  local path="$2"
  local data="${3:-}"

  if [[ -n "$data" ]]; then
    curl -sS --fail-with-body \
      -X "$method" \
      -H "${AURETTY_API_KEY_HEADER:-X-AureTTY-Key}: ${api_key}" \
      -H "Content-Type: application/json" \
      "${base_url}${path}" \
      -d "$data"
    return
  fi

  curl -sS --fail-with-body \
    -X "$method" \
    -H "${AURETTY_API_KEY_HEADER:-X-AureTTY-Key}: ${api_key}" \
    "${base_url}${path}"
}

echo "[http-demo] Health check..."
health_json="$(request GET "/api/v1/health")"
echo "[http-demo] Health: $(echo "$health_json" | jq -c '.')"

echo "[http-demo] Creating session '${session_id}' for viewer '${viewer_id}'..."
create_payload="$(jq -nc \
  --arg sessionId "$session_id" \
  '{ sessionId: $sessionId, shell: 3 }')"
create_json="$(request POST "/api/v1/viewers/${viewer_id}/sessions" "$create_payload")"
created_session_id="$(echo "$create_json" | jq -r '.sessionId')"
if [[ -z "$created_session_id" || "$created_session_id" == "null" ]]; then
  echo "[http-demo] Session creation response is invalid: $create_json" >&2
  exit 1
fi

echo "[http-demo] Sending input..."
input_payload="$(jq -nc --arg text $'echo demo-http && uname -s\n' '{ text: $text, sequence: 1 }')"
_="$(request POST "/api/v1/viewers/${viewer_id}/sessions/${created_session_id}/inputs" "$input_payload")"

echo "[http-demo] Reading diagnostics..."
diagnostics_json="$(request GET "/api/v1/viewers/${viewer_id}/sessions/${created_session_id}/input-diagnostics")"
echo "[http-demo] Diagnostics: $(echo "$diagnostics_json" | jq -c '{sessionId, viewerId, state, nextExpectedSequence, lastAcceptedSequence}')"

echo "[http-demo] Closing viewer sessions..."
request DELETE "/api/v1/viewers/${viewer_id}/sessions" >/dev/null

echo "[http-demo] Completed successfully."
