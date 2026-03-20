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
