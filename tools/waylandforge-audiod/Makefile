CC ?= cc
PKG_CONFIG ?= pkg-config

TARGET := waylandforge-audiod
CFLAGS ?= -O2 -g -std=gnu11 -Wall -Wextra
CPPFLAGS += $(shell $(PKG_CONFIG) --cflags libpipewire-0.3)
LDLIBS += $(shell $(PKG_CONFIG) --libs libpipewire-0.3) -lm -pthread

.PHONY: all clean

all: $(TARGET)

$(TARGET): main.c
	$(CC) $(CPPFLAGS) $(CFLAGS) -o $@ $< $(LDLIBS)

clean:
	rm -f $(TARGET)
