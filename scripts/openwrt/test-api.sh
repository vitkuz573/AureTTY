#!/bin/bash
set -euo pipefail

# AureTTY OpenWRT Automated Test Suite
# Tests AureTTY functionality via HTTP API

API_KEY="${API_KEY:-test-key}"
BASE_URL="${BASE_URL:-http://localhost:17850/api/v1}"
VIEWER_ID="test-viewer"
TEST_SUITE_NAME="${TEST_SUITE_NAME:-AureTTY API Test Suite}"
CURL_CONNECT_TIMEOUT_SECONDS="${CURL_CONNECT_TIMEOUT_SECONDS:-3}"
CURL_MAX_TIME_SECONDS="${CURL_MAX_TIME_SECONDS:-15}"

CURL_COMMON_ARGS=(
    --silent
    --show-error
    --connect-timeout "$CURL_CONNECT_TIMEOUT_SECONDS"
    --max-time "$CURL_MAX_TIME_SECONDS"
)

echo "=========================================="
echo "$TEST_SUITE_NAME"
echo "=========================================="
echo "Base URL: $BASE_URL"
echo "API Key: $API_KEY"
echo "Curl timeouts: connect=${CURL_CONNECT_TIMEOUT_SECONDS}s, max=${CURL_MAX_TIME_SECONDS}s"
echo "=========================================="

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

PASSED=0
FAILED=0

test_pass() {
    echo -e "${GREEN}✓ PASS${NC}: $1"
    PASSED=$((PASSED + 1))
}

test_fail() {
    echo -e "${RED}✗ FAIL${NC}: $1"
    FAILED=$((FAILED + 1))
}

# Test 1: Health check
echo ""
echo "Test 1: Health check"
if curl "${CURL_COMMON_ARGS[@]}" --fail -H "X-AureTTY-Key: $API_KEY" "$BASE_URL/health" | grep -q '"status":"ok"'; then
    test_pass "Health endpoint returned OK"
else
    test_fail "Health endpoint failed"
fi

# Test 2: Create session
echo ""
echo "Test 2: Create session"
if ! RESPONSE=$(curl "${CURL_COMMON_ARGS[@]}" --fail -X POST \
  -H "X-AureTTY-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"shell":"sh"}' \
  "$BASE_URL/viewers/$VIEWER_ID/sessions"); then
    test_fail "Session creation request failed"
    exit 1
fi

SESSION_ID="$(echo "$RESPONSE" | sed -n 's/.*"sessionId":"\([^"]*\)".*/\1/p' | head -n 1)"

if [ -n "$SESSION_ID" ]; then
    test_pass "Session created (ID: $SESSION_ID)"
else
    test_fail "Session creation failed"
    echo "Response: $RESPONSE"
    exit 1
fi

# Test 3: Send input
echo ""
echo "Test 3: Send input"
if curl "${CURL_COMMON_ARGS[@]}" --fail -X POST \
  -H "X-AureTTY-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"text":"echo test\n","sequence":1}' \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID/inputs" > /dev/null; then
    test_pass "Input sent successfully"
else
    test_fail "Input send failed"
fi

# Test 4: Get session info
echo ""
echo "Test 4: Get session info"
if curl "${CURL_COMMON_ARGS[@]}" --fail -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID" | grep -q "$SESSION_ID"; then
    test_pass "Session info retrieved"
else
    test_fail "Session info retrieval failed"
fi

# Test 5: List sessions
echo ""
echo "Test 5: List sessions"
if curl "${CURL_COMMON_ARGS[@]}" --fail -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions" | grep -q "$SESSION_ID"; then
    test_pass "Session listed"
else
    test_fail "Session listing failed"
fi

# Test 6: Resize terminal
echo ""
echo "Test 6: Resize terminal"
if curl "${CURL_COMMON_ARGS[@]}" --fail -X PUT \
  -H "X-AureTTY-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"rows":40,"columns":120}' \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID/terminal-size" > /dev/null; then
    test_pass "Terminal resized"
else
    test_fail "Terminal resize failed"
fi

# Test 7: Send more input
echo ""
echo "Test 7: Send multiple inputs"
SUCCESS=true
for i in {2..5}; do
    if ! curl "${CURL_COMMON_ARGS[@]}" --fail -X POST \
      -H "X-AureTTY-Key: $API_KEY" \
      -H "Content-Type: application/json" \
      -d "{\"text\":\"echo line $i\n\",\"sequence\":$i}" \
      "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID/inputs" > /dev/null; then
        SUCCESS=false
        break
    fi
done

if $SUCCESS; then
    test_pass "Multiple inputs sent"
else
    test_fail "Multiple inputs failed"
fi

# Test 8: Close session
echo ""
echo "Test 8: Close session"
if curl "${CURL_COMMON_ARGS[@]}" --fail -X DELETE \
  -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID" > /dev/null; then
    test_pass "Session closed"
else
    test_fail "Session close failed"
fi

# Test 9: Verify session is closed
echo ""
echo "Test 9: Verify session closed"
if ! curl "${CURL_COMMON_ARGS[@]}" --fail -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID" > /dev/null 2>&1; then
    test_pass "Session no longer exists"
else
    test_fail "Session still exists after close"
fi

# Summary
echo ""
echo "=========================================="
echo "Test Summary"
echo "=========================================="
echo -e "${GREEN}Passed: $PASSED${NC}"
echo -e "${RED}Failed: $FAILED${NC}"
echo "=========================================="

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}Some tests failed!${NC}"
    exit 1
fi
