# Testing AureTTY in QEMU OpenWRT VM

This guide explains how to test AureTTY in a QEMU virtual machine running OpenWRT.

## Prerequisites

- QEMU installed: `sudo apt-get install qemu-system-x86`
- Built AureTTY binary for x86_64: `ARCH=x86_64 ./build-openwrt.sh`

## Quick Start

```bash
# Download OpenWRT x86_64 image
wget https://downloads.openwrt.org/releases/23.05.0/targets/x86/64/openwrt-23.05.0-x86-64-generic-ext4-combined.img.gz
gunzip openwrt-23.05.0-x86-64-generic-ext4-combined.img.gz

# Start QEMU VM
qemu-system-x86_64 \
  -enable-kvm \
  -m 256M \
  -nographic \
  -device e1000,netdev=net0 \
  -netdev user,id=net0,hostfwd=tcp::2222-:22,hostfwd=tcp::17850-:17850 \
  -drive file=openwrt-23.05.0-x86-64-generic-ext4-combined.img,format=raw

# Login (no password by default)
# Username: root
# Password: (press Enter)

# Set root password
passwd

# Configure network (if needed)
uci set network.lan.ipaddr='192.168.1.1'
uci commit network
/etc/init.d/network restart
```

## Transferring Binary to VM

### Method 1: SCP (Recommended)

```bash
# From host machine
scp -P 2222 artifacts/openwrt/x86_64/auretty root@localhost:/tmp/

# In VM
mv /tmp/auretty /usr/bin/
chmod +x /usr/bin/auretty
```

### Method 2: HTTP Server

```bash
# On host machine
cd artifacts/openwrt/x86_64
python3 -m http.server 8000

# In VM
wget http://10.0.2.2:8000/auretty -O /usr/bin/auretty
chmod +x /usr/bin/auretty
```

### Method 3: Shared Folder (9p)

```bash
# Start QEMU with shared folder
qemu-system-x86_64 \
  -enable-kvm \
  -m 256M \
  -nographic \
  -device e1000,netdev=net0 \
  -netdev user,id=net0,hostfwd=tcp::2222-:22,hostfwd=tcp::17850-:17850 \
  -drive file=openwrt-23.05.0-x86-64-generic-ext4-combined.img,format=raw \
  -virtfs local,path=/path/to/auretty_repo/artifacts/openwrt/x86_64,mount_tag=host0,security_model=passthrough,id=host0

# In VM
mkdir -p /mnt/host
mount -t 9p -o trans=virtio,version=9p2000.L host0 /mnt/host
cp /mnt/host/auretty /usr/bin/
chmod +x /usr/bin/auretty
```

## Testing AureTTY

### Basic Test

```bash
# Check version
auretty --version

# Check help
auretty --help

# Run service
auretty --transport http --http-listen-url http://0.0.0.0:17850 --api-key test-key
```

### Test from Host Machine

```bash
# Health check
curl http://localhost:17850/api/v1/health

# Create session
curl -X POST \
  -H "X-AureTTY-Key: test-key" \
  -H "Content-Type: application/json" \
  -d '{"shell":"sh"}' \
  http://localhost:17850/api/v1/viewers/test-viewer/sessions

# Send input
curl -X POST \
  -H "X-AureTTY-Key: test-key" \
  -H "Content-Type: application/json" \
  -d '{"text":"echo hello\n","sequence":1}' \
  http://localhost:17850/api/v1/viewers/test-viewer/sessions/SESSION_ID/inputs

# Stream events (SSE)
curl -N -H "X-AureTTY-Key: test-key" \
  http://localhost:17850/api/v1/viewers/test-viewer/events
```

### WebSocket Test

```bash
# Install websocat in VM
opkg update
opkg install websocat

# Connect to WebSocket
websocat ws://localhost:17850/api/v1/viewers/test-viewer/ws?api_key=test-key

# Send terminal.start
{"type":"request","id":"1","method":"terminal.start","payload":{"viewerId":"test-viewer","request":{"sessionId":"s1","shell":"sh"}}}

# Send terminal.input
{"type":"request","id":"2","method":"terminal.input","payload":{"viewerId":"test-viewer","request":{"sessionId":"s1","text":"ls\n","sequence":1}}}
```

## Performance Testing

### Memory Usage

```bash
# Before starting AureTTY
free -m

# Start AureTTY
auretty --transport http --http-listen-url http://0.0.0.0:17850 --api-key test &

# Check memory usage
ps aux | grep auretty
top -b -n 1 | grep auretty

# With multiple sessions
for i in {1..10}; do
  curl -X POST -H "X-AureTTY-Key: test" -d "{\"shell\":\"sh\"}" \
    http://localhost:17850/api/v1/viewers/v1/sessions &
done

# Check memory again
free -m
```

### Load Testing

```bash
# Install apache2-utils (ab) on host
sudo apt-get install apache2-utils

# Benchmark health endpoint
ab -n 1000 -c 10 http://localhost:17850/api/v1/health

# Benchmark session creation
ab -n 100 -c 5 -p session.json -T application/json \
  -H "X-AureTTY-Key: test-key" \
  http://localhost:17850/api/v1/viewers/test/sessions

# session.json content:
# {"shell":"sh"}
```

## Resource Limits Testing

### Low Memory Configuration

```bash
# Start with minimal limits
auretty \
  --transport http \
  --http-listen-url http://0.0.0.0:17850 \
  --api-key test \
  --max-concurrent-sessions 4 \
  --max-sessions-per-viewer 2 \
  --replay-buffer-capacity 512 \
  --sse-subscription-buffer-capacity 256

# Test session limits
for i in {1..10}; do
  curl -X POST -H "X-AureTTY-Key: test" -d "{\"shell\":\"sh\"}" \
    http://localhost:17850/api/v1/viewers/v1/sessions
done

# Should fail after 4 sessions
```

### High Load Configuration

```bash
# Start with high limits
auretty \
  --transport http \
  --http-listen-url http://0.0.0.0:17850 \
  --api-key test \
  --max-concurrent-sessions 32 \
  --max-sessions-per-viewer 8 \
  --replay-buffer-capacity 4096 \
  --sse-subscription-buffer-capacity 2048

# Create many sessions
for i in {1..32}; do
  curl -X POST -H "X-AureTTY-Key: test" -d "{\"shell\":\"sh\"}" \
    http://localhost:17850/api/v1/viewers/v$i/sessions &
done

# Monitor memory
watch -n 1 'free -m && ps aux | grep auretty'
```

## Automated Testing Script

Create `test-openwrt.sh`:

```bash
#!/bin/bash
set -e

API_KEY="test-key"
BASE_URL="http://localhost:17850/api/v1"
VIEWER_ID="test-viewer"

echo "=== AureTTY OpenWRT Test Suite ==="

# Test 1: Health check
echo "Test 1: Health check"
curl -s "$BASE_URL/health" | grep -q "Healthy" && echo "✓ PASS" || echo "✗ FAIL"

# Test 2: Create session
echo "Test 2: Create session"
SESSION_ID=$(curl -s -X POST \
  -H "X-AureTTY-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"shell":"sh"}' \
  "$BASE_URL/viewers/$VIEWER_ID/sessions" | grep -o '"sessionId":"[^"]*"' | cut -d'"' -f4)

if [ -n "$SESSION_ID" ]; then
  echo "✓ PASS (Session ID: $SESSION_ID)"
else
  echo "✗ FAIL"
  exit 1
fi

# Test 3: Send input
echo "Test 3: Send input"
curl -s -X POST \
  -H "X-AureTTY-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"text":"echo test\n","sequence":1}' \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID/inputs" | grep -q "accepted" && echo "✓ PASS" || echo "✗ FAIL"

# Test 4: Get session info
echo "Test 4: Get session info"
curl -s -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID" | grep -q "$SESSION_ID" && echo "✓ PASS" || echo "✗ FAIL"

# Test 5: List sessions
echo "Test 5: List sessions"
curl -s -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions" | grep -q "$SESSION_ID" && echo "✓ PASS" || echo "✗ FAIL"

# Test 6: Close session
echo "Test 6: Close session"
curl -s -X DELETE \
  -H "X-AureTTY-Key: $API_KEY" \
  "$BASE_URL/viewers/$VIEWER_ID/sessions/$SESSION_ID" && echo "✓ PASS" || echo "✗ FAIL"

echo "=== All tests completed ==="
```

Run tests:
```bash
chmod +x test-openwrt.sh
./test-openwrt.sh
```

## QEMU Snapshot Testing

```bash
# Create snapshot after setup
qemu-img snapshot -c clean-install openwrt-23.05.0-x86-64-generic-ext4-combined.img

# Test changes
# ... make changes ...

# Restore snapshot
qemu-img snapshot -a clean-install openwrt-23.05.0-x86-64-generic-ext4-combined.img

# List snapshots
qemu-img snapshot -l openwrt-23.05.0-x86-64-generic-ext4-combined.img
```

## Troubleshooting

### VM Won't Boot

```bash
# Check if KVM is available
kvm-ok

# If KVM not available, remove -enable-kvm flag
qemu-system-x86_64 \
  -m 256M \
  -nographic \
  -device e1000,netdev=net0 \
  -netdev user,id=net0,hostfwd=tcp::2222-:22,hostfwd=tcp::17850-:17850 \
  -drive file=openwrt-23.05.0-x86-64-generic-ext4-combined.img,format=raw
```

### Can't Connect to VM

```bash
# Check port forwarding
netstat -tuln | grep 2222
netstat -tuln | grep 17850

# Try different ports
qemu-system-x86_64 \
  -enable-kvm \
  -m 256M \
  -nographic \
  -device e1000,netdev=net0 \
  -netdev user,id=net0,hostfwd=tcp::2223-:22,hostfwd=tcp::17851-:17850 \
  -drive file=openwrt-23.05.0-x86-64-generic-ext4-combined.img,format=raw
```

### Binary Won't Run

```bash
# Check binary
file /usr/bin/auretty
# Should show: ELF 64-bit LSB pie executable, x86-64, dynamically linked, interpreter /lib/ld-musl-x86_64.so.1

# Check musl
ls -l /lib/ld-musl-x86_64.so.1

# Test execution
/usr/bin/auretty --version

# Check dependencies
ldd /usr/bin/auretty 2>&1
```

### Out of Memory

```bash
# Increase VM memory
qemu-system-x86_64 \
  -enable-kvm \
  -m 512M \
  ...

# Or reduce AureTTY limits
auretty \
  --max-concurrent-sessions 2 \
  --max-sessions-per-viewer 1 \
  --replay-buffer-capacity 256 \
  ...
```

## Exit QEMU

```bash
# From QEMU console
poweroff

# Or force quit (Ctrl+A, then X)
# Press: Ctrl+A
# Then press: X
```

## Next Steps

- [Create OpenWRT Package](PACKAGE.md)
- [Deploy to Real Device](DEPLOYMENT.md)
- [Performance Tuning](../TROUBLESHOOTING.md)

## See Also

- [QEMU Documentation](https://www.qemu.org/docs/master/)
- [OpenWRT in QEMU](https://openwrt.org/docs/guide-user/virtualization/qemu)
- [OpenWRT x86 Images](https://downloads.openwrt.org/releases/)
