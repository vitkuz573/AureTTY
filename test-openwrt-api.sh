#!/bin/bash
set -e

# AureTTY OpenWRT Automated Test Suite
# Tests AureTTY functionality via HTTP API

API_KEY="${API_KEY:-test-key}"
BASE_URL="${BASE_URL:-http://localhost:17850/api/v1}"
VIEWER_ID="test-viewer"

echo "=========================================="
echo "AureTTY OpenWRT Test Suite"
echo "=========================================="
echo "Base URL: $BASE_URL"
echo "API Key: $API_KEY"
echo "=========================================="

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

PASSED=0
FAILED=0

test_pass() {
    echo -e "${GREEN}✓ PASS${NC}: $1"
    ((PASSED++))
}

test_fail() {
    echo -e "${RED}✗ FAIL${NC}: $1"
    ((FAILED++))
}

# Test 1: Health check
echo ""
echo "Test 1: Health check"
if curl -s -f -H "X-AureTTY-Key: $API_KEY" "$BASE_URL/health" | grep -q '"status":"ok"'; then
    test_pass "Health endpoint returned OK"
else
    test_fail "Health endpoint failed"
fi

# Test 2: Create session
echo ""
echo "Test 2: Create session"
RESPONSE=$(curl -s -X POST \
  -H "X-AureTTY-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"shell":3}' \
  "$BASE_URL/viewers/$VIEWER_ID/sessions")

SESSION_ID=$(echo "$RESPONSE" | grep -o '"sessionId":"[^"]*"' | cut -d'"' -f4)

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
if curl -s -f -X POST \
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
if curl -s -f -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID" | grep -q "$SESSION_ID"; then
    test_pass "Session info retrieved"
else
    test_fail "Session info retrieval failed"
fi

# Test 5: List sessions
echo ""
echo "Test 5: List sessions"
if curl -s -f -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions" | grep -q "$SESSION_ID"; then
    test_pass "Session listed"
else
    test_fail "Session listing failed"
fi

# Test 6: Resize terminal
echo ""
echo "Test 6: Resize terminal"
if curl -s -f -X PUT \
  -H "X-AureTTY-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"rows":40,"cols":120}' \
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
    if ! curl -s -f -X POST \
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
if curl -s -f -X DELETE \
  -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID" > /dev/null; then
    test_pass "Session closed"
else
    test_fail "Session close failed"
fi

# Test 9: Verify session is closed
echo ""
echo "Test 9: Verify session closed"
if ! curl -s -f -H "X-AureTTY-Key: $API_KEY" \
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
