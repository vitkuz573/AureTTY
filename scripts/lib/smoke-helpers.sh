#!/usr/bin/env bash

# Shared helpers for AureTTY smoke/CI shell scripts.

run_with_retry() {
    local attempts="$1"
    shift
    local attempt=1

    while true; do
        if "$@"; then
            return 0
        fi
        if (( attempt >= attempts )); then
            return 1
        fi
        echo "Retrying ($((attempt + 1))/$attempts): $*"
        attempt=$((attempt + 1))
        sleep 2
    done
}

wait_for_http_health() {
    local url="$1"
    local api_key="$2"
    local timeout_seconds="$3"
    local process_pid="${4:-}"
    local _i

    for _i in $(seq 1 "$timeout_seconds"); do
        if curl -s -f -H "X-AureTTY-Key: $api_key" "$url" >/dev/null 2>&1; then
            return 0
        fi

        if [[ -n "$process_pid" ]] && ! kill -0 "$process_pid" >/dev/null 2>&1; then
            break
        fi

        sleep 1
    done

    return 1
}

