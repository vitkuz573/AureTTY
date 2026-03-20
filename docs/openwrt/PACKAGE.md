# OpenWRT Package for AureTTY

This guide explains how to create an OpenWRT package for AureTTY.

## Package Structure

```
package/auretty/
├── Makefile              # OpenWRT package build instructions
├── files/
│   ├── auretty.init      # Init script (/etc/init.d/auretty)
│   └── auretty.config    # UCI configuration (/etc/config/auretty)
└── patches/              # Optional patches
```

## Creating the Package

### 1. Makefile

Create `package/auretty/Makefile`:

```makefile
include $(TOPDIR)/rules.mk

PKG_NAME:=auretty
PKG_VERSION:=0.0.0
PKG_RELEASE:=1

PKG_SOURCE_PROTO:=git
PKG_SOURCE_URL:=https://github.com/vitkuz573/AureTTY.git
PKG_SOURCE_VERSION:=main
PKG_MIRROR_HASH:=skip

PKG_LICENSE:=MIT Apache-2.0
PKG_LICENSE_FILES:=LICENSE-MIT LICENSE-APACHE
PKG_MAINTAINER:=Vitaly Kuzyaev <vitkuz573@gmail.com>

include $(INCLUDE_DIR)/package.mk

define Package/auretty
  SECTION:=net
  CATEGORY:=Network
  TITLE:=Terminal runtime with HTTP/WebSocket API
  URL:=https://github.com/vitkuz573/AureTTY
  DEPENDS:=+util-linux-script
endef

define Package/auretty/description
  AureTTY is a standalone terminal runtime that provides HTTP REST API,
  Server-Sent Events (SSE), and WebSocket transport for terminal sessions.
  Supports session multiplexing, reconnection with state recovery, and
  MessagePack binary protocol.
endef

define Package/auretty/conffiles
/etc/config/auretty
endef

define Build/Compile
	# Binary is pre-built using build-openwrt.sh
	# Copy from artifacts/openwrt/$(ARCH)/auretty
endef

define Package/auretty/install
	$(INSTALL_DIR) $(1)/usr/bin
	$(INSTALL_BIN) $(PKG_BUILD_DIR)/artifacts/openwrt/$(ARCH)/auretty $(1)/usr/bin/

	$(INSTALL_DIR) $(1)/etc/init.d
	$(INSTALL_BIN) ./files/auretty.init $(1)/etc/init.d/auretty

	$(INSTALL_DIR) $(1)/etc/config
	$(INSTALL_CONF) ./files/auretty.config $(1)/etc/config/auretty
endef

$(eval $(call BuildPackage,auretty))
```

### 2. Init Script

Create `package/auretty/files/auretty.init`:

```bash
#!/bin/sh /etc/rc.common

START=99
STOP=10

USE_PROCD=1

PROG=/usr/bin/auretty
CONF=/etc/config/auretty

start_service() {
    local enabled transport http_url api_key
    local max_sessions max_per_viewer
    local replay_buffer sse_buffer
    local allow_query_key

    config_load auretty
    config_get_bool enabled config enabled 0
    [ "$enabled" -eq 0 ] && return 0

    config_get transport config transport "http"
    config_get http_url config http_url "http://0.0.0.0:17850"
    config_get api_key config api_key ""
    config_get max_sessions config max_sessions "16"
    config_get max_per_viewer config max_per_viewer "4"
    config_get replay_buffer config replay_buffer "2048"
    config_get sse_buffer config sse_buffer "1024"
    config_get_bool allow_query_key config allow_query_key 0

    procd_open_instance
    procd_set_param command "$PROG"
    procd_append_param command --transport "$transport"

    [ -n "$http_url" ] && procd_append_param command --http-listen-url "$http_url"
    [ -n "$api_key" ] && procd_append_param command --api-key "$api_key"

    procd_append_param command --max-concurrent-sessions "$max_sessions"
    procd_append_param command --max-sessions-per-viewer "$max_per_viewer"
    procd_append_param command --replay-buffer-capacity "$replay_buffer"
    procd_append_param command --sse-subscription-buffer-capacity "$sse_buffer"

    [ "$allow_query_key" -eq 1 ] && procd_append_param command --allow-api-key-query

    procd_set_param respawn
    procd_set_param stdout 1
    procd_set_param stderr 1
    procd_close_instance
}

service_triggers() {
    procd_add_reload_trigger "auretty"
}
```

Make it executable:
```bash
chmod +x package/auretty/files/auretty.init
```

### 3. UCI Configuration

Create `package/auretty/files/auretty.config`:

```
config auretty 'config'
    option enabled '0'
    option transport 'http'
    option http_url 'http://0.0.0.0:17850'
    option api_key ''
    option max_sessions '16'
    option max_per_viewer '4'
    option replay_buffer '2048'
    option sse_buffer '1024'
    option allow_query_key '0'
```

## Building the Package

### Using OpenWRT SDK

```bash
# Download OpenWRT SDK
wget https://downloads.openwrt.org/releases/23.05.0/targets/x86/64/openwrt-sdk-23.05.0-x86-64_gcc-12.3.0_musl.Linux-x86_64.tar.xz
tar xf openwrt-sdk-23.05.0-x86-64_gcc-12.3.0_musl.Linux-x86_64.tar.xz
cd openwrt-sdk-23.05.0-x86-64_gcc-12.3.0_musl.Linux-x86_64

# Copy package
cp -r /path/to/auretty_repo/package/auretty package/

# Pre-build binary
cd /path/to/auretty_repo
ARCH=x86_64 ./build-openwrt.sh

# Copy binary to SDK
mkdir -p openwrt-sdk-*/package/auretty/artifacts/openwrt/x86_64
cp artifacts/openwrt/x86_64/auretty openwrt-sdk-*/package/auretty/artifacts/openwrt/x86_64/

# Build package
cd openwrt-sdk-*
make package/auretty/compile V=s

# Output: bin/packages/x86_64/base/auretty_*.ipk
```

### Manual Package Creation

```bash
# Create package directory structure
mkdir -p auretty_ipk/usr/bin
mkdir -p auretty_ipk/etc/init.d
mkdir -p auretty_ipk/etc/config
mkdir -p auretty_ipk/CONTROL

# Copy files
cp artifacts/openwrt/x86_64/auretty auretty_ipk/usr/bin/
cp package/auretty/files/auretty.init auretty_ipk/etc/init.d/auretty
cp package/auretty/files/auretty.config auretty_ipk/etc/config/auretty

# Set permissions
chmod +x auretty_ipk/usr/bin/auretty
chmod +x auretty_ipk/etc/init.d/auretty

# Create control file
cat > auretty_ipk/CONTROL/control <<EOF
Package: auretty
Version: 0.0.0-1
Architecture: x86_64
Maintainer: Vitaly Kuzyaev <vitkuz573@gmail.com>
Section: net
Priority: optional
Depends: util-linux-script
Description: Terminal runtime with HTTP/WebSocket API
 AureTTY provides HTTP REST API, SSE, and WebSocket transport
 for terminal sessions with multiplexing and reconnection support.
EOF

# Create conffiles
cat > auretty_ipk/CONTROL/conffiles <<EOF
/etc/config/auretty
EOF

# Build IPK
opkg-build auretty_ipk

# Output: auretty_0.0.0-1_x86_64.ipk
```

## Installing the Package

### On OpenWRT Device

```bash
# Copy package to device
scp auretty_0.0.0-1_x86_64.ipk root@192.168.1.1:/tmp/

# SSH to device
ssh root@192.168.1.1

# Install
opkg install /tmp/auretty_0.0.0-1_x86_64.ipk

# Configure
uci set auretty.config.enabled='1'
uci set auretty.config.api_key='your-secret-key'
uci commit auretty

# Start service
/etc/init.d/auretty start

# Enable on boot
/etc/init.d/auretty enable

# Check status
/etc/init.d/auretty status
```

## Configuration via UCI

### View Configuration

```bash
uci show auretty
```

### Modify Configuration

```bash
# Enable service
uci set auretty.config.enabled='1'

# Set API key
uci set auretty.config.api_key='your-secret-key'

# Change listen URL
uci set auretty.config.http_url='http://0.0.0.0:8080'

# Adjust limits for low-memory devices
uci set auretty.config.max_sessions='8'
uci set auretty.config.max_per_viewer='2'
uci set auretty.config.replay_buffer='1024'
uci set auretty.config.sse_buffer='512'

# Allow API key in query parameter (not recommended for production)
uci set auretty.config.allow_query_key='1'

# Commit changes
uci commit auretty

# Restart service
/etc/init.d/auretty restart
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| enabled | bool | 0 | Enable/disable service |
| transport | string | http | Transport type (http, pipe, or both) |
| http_url | string | http://0.0.0.0:17850 | HTTP listen URL |
| api_key | string | (empty) | API authentication key |
| max_sessions | int | 16 | Maximum concurrent sessions |
| max_per_viewer | int | 4 | Maximum sessions per viewer |
| replay_buffer | int | 2048 | Replay buffer size (events) |
| sse_buffer | int | 1024 | SSE subscription buffer size |
| allow_query_key | bool | 0 | Allow API key in query parameter |

## Service Management

```bash
# Start service
/etc/init.d/auretty start

# Stop service
/etc/init.d/auretty stop

# Restart service
/etc/init.d/auretty restart

# Reload configuration
/etc/init.d/auretty reload

# Enable on boot
/etc/init.d/auretty enable

# Disable on boot
/etc/init.d/auretty disable

# Check status
/etc/init.d/auretty status
```

## Logs

```bash
# View logs
logread | grep auretty

# Follow logs
logread -f | grep auretty

# System log
tail -f /var/log/messages | grep auretty
```

## Firewall Configuration

```bash
# Allow HTTP access from LAN
uci add firewall rule
uci set firewall.@rule[-1].name='Allow-AureTTY'
uci set firewall.@rule[-1].src='lan'
uci set firewall.@rule[-1].proto='tcp'
uci set firewall.@rule[-1].dest_port='17850'
uci set firewall.@rule[-1].target='ACCEPT'
uci commit firewall
/etc/init.d/firewall restart

# Allow from WAN (not recommended without HTTPS)
uci set firewall.@rule[-1].src='wan'
uci commit firewall
/etc/init.d/firewall restart
```

## Reverse Proxy with nginx

```bash
# Install nginx
opkg update
opkg install nginx-ssl

# Configure nginx
cat > /etc/nginx/conf.d/auretty.conf <<EOF
server {
    listen 443 ssl http2;
    server_name router.local;

    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;

    location /api/v1/ {
        proxy_pass http://127.0.0.1:17850;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
EOF

# Restart nginx
/etc/init.d/nginx restart
```

## Uninstalling

```bash
# Stop service
/etc/init.d/auretty stop
/etc/init.d/auretty disable

# Remove package
opkg remove auretty

# Remove configuration (optional)
rm /etc/config/auretty
```

## Troubleshooting

### Service Won't Start

```bash
# Check configuration
uci show auretty

# Check if binary exists
ls -l /usr/bin/auretty

# Test binary manually
/usr/bin/auretty --version

# Check logs
logread | grep auretty

# Run manually for debugging
/usr/bin/auretty --transport http --http-listen-url http://0.0.0.0:17850 --api-key test
```

### Out of Memory

```bash
# Reduce limits
uci set auretty.config.max_sessions='4'
uci set auretty.config.max_per_viewer='2'
uci set auretty.config.replay_buffer='512'
uci set auretty.config.sse_buffer='256'
uci commit auretty
/etc/init.d/auretty restart

# Check memory usage
free -m
top -b -n 1 | grep auretty
```

### Permission Denied

```bash
# Fix permissions
chmod +x /usr/bin/auretty
chmod +x /etc/init.d/auretty
```

## See Also

- [Build Guide](BUILD.md)
- [QEMU Testing](QEMU_TESTING.md)
- [OpenWRT Package Guidelines](https://openwrt.org/docs/guide-developer/packages)
