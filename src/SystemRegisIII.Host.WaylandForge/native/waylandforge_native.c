#define _GNU_SOURCE

#include <errno.h>
#include <fcntl.h>
#include <linux/input-event-codes.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <unistd.h>
#include <wayland-client.h>

#include "xdg-shell-client-protocol.h"

typedef void (*waylandforge_render_callback)(uint32_t *pixels, int width, int height, int stride_pixels, uint64_t frame_index);

struct waylandforge_app {
    struct wl_display *display;
    struct wl_registry *registry;
    struct wl_compositor *compositor;
    struct wl_shm *shm;
    struct wl_seat *seat;
    struct wl_keyboard *keyboard;
    struct xdg_wm_base *wm_base;
    struct wl_surface *surface;
    struct xdg_surface *xdg_surface;
    struct xdg_toplevel *toplevel;
    struct wl_buffer *buffer;
    struct wl_callback *frame_callback;
    uint32_t *pixels;
    int width;
    int height;
    int stride_pixels;
    int buffer_bytes;
    int configured;
    int running;
    uint64_t frame_index;
    waylandforge_render_callback render;
};

static int make_shm_file(size_t size)
{
    int fd = memfd_create("waylandforge-shm", MFD_CLOEXEC | MFD_ALLOW_SEALING);
    if (fd < 0) {
        perror("memfd_create");
        return -1;
    }

    if (ftruncate(fd, (off_t)size) < 0) {
        perror("ftruncate");
        close(fd);
        return -1;
    }

    return fd;
}

static void draw_and_commit(struct waylandforge_app *app);

static void frame_done(void *data, struct wl_callback *callback, uint32_t time_ms)
{
    (void)time_ms;
    struct waylandforge_app *app = data;
    wl_callback_destroy(callback);
    app->frame_callback = NULL;
    draw_and_commit(app);
}

static const struct wl_callback_listener frame_listener = {
    .done = frame_done,
};

static void draw_and_commit(struct waylandforge_app *app)
{
    if (!app->configured || !app->running) {
        return;
    }

    app->render(app->pixels, app->width, app->height, app->stride_pixels, app->frame_index++);

    wl_surface_attach(app->surface, app->buffer, 0, 0);
    wl_surface_damage_buffer(app->surface, 0, 0, app->width, app->height);
    app->frame_callback = wl_surface_frame(app->surface);
    wl_callback_add_listener(app->frame_callback, &frame_listener, app);
    wl_surface_commit(app->surface);
}

static void wm_base_ping(void *data, struct xdg_wm_base *wm_base, uint32_t serial)
{
    (void)data;
    xdg_wm_base_pong(wm_base, serial);
}

static const struct xdg_wm_base_listener wm_base_listener = {
    .ping = wm_base_ping,
};

static void xdg_surface_configure(void *data, struct xdg_surface *surface, uint32_t serial)
{
    struct waylandforge_app *app = data;
    xdg_surface_ack_configure(surface, serial);

    if (!app->configured) {
        app->configured = 1;
        draw_and_commit(app);
    }
}

static const struct xdg_surface_listener xdg_surface_listener = {
    .configure = xdg_surface_configure,
};

static void toplevel_configure(void *data, struct xdg_toplevel *toplevel, int32_t width, int32_t height, struct wl_array *states)
{
    (void)data;
    (void)toplevel;
    (void)width;
    (void)height;
    (void)states;
}

static void toplevel_close(void *data, struct xdg_toplevel *toplevel)
{
    (void)toplevel;
    struct waylandforge_app *app = data;
    app->running = 0;
}

static const struct xdg_toplevel_listener toplevel_listener = {
    .configure = toplevel_configure,
    .close = toplevel_close,
};

static void keyboard_keymap(void *data, struct wl_keyboard *keyboard, uint32_t format, int32_t fd, uint32_t size)
{
    (void)data;
    (void)keyboard;
    (void)format;
    (void)size;
    if (fd >= 0) {
        close(fd);
    }
}

static void keyboard_enter(void *data, struct wl_keyboard *keyboard, uint32_t serial, struct wl_surface *surface, struct wl_array *keys)
{
    (void)data;
    (void)keyboard;
    (void)serial;
    (void)surface;
    (void)keys;
}

static void keyboard_leave(void *data, struct wl_keyboard *keyboard, uint32_t serial, struct wl_surface *surface)
{
    (void)data;
    (void)keyboard;
    (void)serial;
    (void)surface;
}

static void keyboard_key(void *data, struct wl_keyboard *keyboard, uint32_t serial, uint32_t time_ms, uint32_t key, uint32_t state)
{
    (void)keyboard;
    (void)serial;
    (void)time_ms;

    struct waylandforge_app *app = data;
    if (key == KEY_ESC && state == WL_KEYBOARD_KEY_STATE_PRESSED) {
        app->running = 0;
    }
}

static void keyboard_modifiers(void *data, struct wl_keyboard *keyboard, uint32_t serial, uint32_t depressed, uint32_t latched, uint32_t locked, uint32_t group)
{
    (void)data;
    (void)keyboard;
    (void)serial;
    (void)depressed;
    (void)latched;
    (void)locked;
    (void)group;
}

static void keyboard_repeat_info(void *data, struct wl_keyboard *keyboard, int32_t rate, int32_t delay)
{
    (void)data;
    (void)keyboard;
    (void)rate;
    (void)delay;
}

static const struct wl_keyboard_listener keyboard_listener = {
    .keymap = keyboard_keymap,
    .enter = keyboard_enter,
    .leave = keyboard_leave,
    .key = keyboard_key,
    .modifiers = keyboard_modifiers,
    .repeat_info = keyboard_repeat_info,
};

static void seat_capabilities(void *data, struct wl_seat *seat, uint32_t capabilities)
{
    struct waylandforge_app *app = data;

    if ((capabilities & WL_SEAT_CAPABILITY_KEYBOARD) && app->keyboard == NULL) {
        app->keyboard = wl_seat_get_keyboard(seat);
        wl_keyboard_add_listener(app->keyboard, &keyboard_listener, app);
    } else if (!(capabilities & WL_SEAT_CAPABILITY_KEYBOARD) && app->keyboard != NULL) {
        wl_keyboard_destroy(app->keyboard);
        app->keyboard = NULL;
    }
}

static void seat_name(void *data, struct wl_seat *seat, const char *name)
{
    (void)data;
    (void)seat;
    (void)name;
}

static const struct wl_seat_listener seat_listener = {
    .capabilities = seat_capabilities,
    .name = seat_name,
};

static void registry_global(void *data, struct wl_registry *registry, uint32_t name, const char *interface, uint32_t version)
{
    struct waylandforge_app *app = data;

    if (strcmp(interface, wl_compositor_interface.name) == 0) {
        uint32_t bind_version = version < 4 ? version : 4;
        app->compositor = wl_registry_bind(registry, name, &wl_compositor_interface, bind_version);
    } else if (strcmp(interface, wl_shm_interface.name) == 0) {
        app->shm = wl_registry_bind(registry, name, &wl_shm_interface, 1);
    } else if (strcmp(interface, xdg_wm_base_interface.name) == 0) {
        app->wm_base = wl_registry_bind(registry, name, &xdg_wm_base_interface, 1);
        xdg_wm_base_add_listener(app->wm_base, &wm_base_listener, app);
    } else if (strcmp(interface, wl_seat_interface.name) == 0) {
        uint32_t bind_version = version < 7 ? version : 7;
        app->seat = wl_registry_bind(registry, name, &wl_seat_interface, bind_version);
        wl_seat_add_listener(app->seat, &seat_listener, app);
    }
}

static void registry_global_remove(void *data, struct wl_registry *registry, uint32_t name)
{
    (void)data;
    (void)registry;
    (void)name;
}

static const struct wl_registry_listener registry_listener = {
    .global = registry_global,
    .global_remove = registry_global_remove,
};

static int create_shm_buffer(struct waylandforge_app *app)
{
    app->stride_pixels = app->width;
    int stride_bytes = app->stride_pixels * 4;
    app->buffer_bytes = stride_bytes * app->height;

    int fd = make_shm_file((size_t)app->buffer_bytes);
    if (fd < 0) {
        return -1;
    }

    app->pixels = mmap(NULL, (size_t)app->buffer_bytes, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
    if (app->pixels == MAP_FAILED) {
        perror("mmap");
        app->pixels = NULL;
        close(fd);
        return -1;
    }

    struct wl_shm_pool *pool = wl_shm_create_pool(app->shm, fd, app->buffer_bytes);
    app->buffer = wl_shm_pool_create_buffer(pool, 0, app->width, app->height, stride_bytes, WL_SHM_FORMAT_ARGB8888);
    wl_shm_pool_destroy(pool);
    close(fd);

    return app->buffer == NULL ? -1 : 0;
}

static void cleanup(struct waylandforge_app *app)
{
    if (app->frame_callback) {
        wl_callback_destroy(app->frame_callback);
    }
    if (app->buffer) {
        wl_buffer_destroy(app->buffer);
    }
    if (app->pixels) {
        munmap(app->pixels, (size_t)app->buffer_bytes);
    }
    if (app->keyboard) {
        wl_keyboard_destroy(app->keyboard);
    }
    if (app->seat) {
        wl_seat_destroy(app->seat);
    }
    if (app->toplevel) {
        xdg_toplevel_destroy(app->toplevel);
    }
    if (app->xdg_surface) {
        xdg_surface_destroy(app->xdg_surface);
    }
    if (app->surface) {
        wl_surface_destroy(app->surface);
    }
    if (app->wm_base) {
        xdg_wm_base_destroy(app->wm_base);
    }
    if (app->shm) {
        wl_shm_destroy(app->shm);
    }
    if (app->compositor) {
        wl_compositor_destroy(app->compositor);
    }
    if (app->registry) {
        wl_registry_destroy(app->registry);
    }
    if (app->display) {
        wl_display_disconnect(app->display);
    }
}

int waylandforge_run(int width, int height, const char *title, waylandforge_render_callback render)
{
    if (width <= 0 || height <= 0 || render == NULL) {
        return 2;
    }

    struct waylandforge_app app = {
        .width = width,
        .height = height,
        .running = 1,
        .render = render,
    };

    app.display = wl_display_connect(NULL);
    if (app.display == NULL) {
        fprintf(stderr, "wl_display_connect failed. Is WAYLAND_DISPLAY set?\n");
        return 10;
    }

    app.registry = wl_display_get_registry(app.display);
    wl_registry_add_listener(app.registry, &registry_listener, &app);
    wl_display_roundtrip(app.display);
    wl_display_roundtrip(app.display);

    if (app.compositor == NULL || app.shm == NULL || app.wm_base == NULL) {
        fprintf(stderr, "Missing required Wayland globals: compositor=%p shm=%p xdg_wm_base=%p\n",
                (void *)app.compositor, (void *)app.shm, (void *)app.wm_base);
        cleanup(&app);
        return 11;
    }

    if (create_shm_buffer(&app) != 0) {
        cleanup(&app);
        return 12;
    }

    app.surface = wl_compositor_create_surface(app.compositor);
    app.xdg_surface = xdg_wm_base_get_xdg_surface(app.wm_base, app.surface);
    xdg_surface_add_listener(app.xdg_surface, &xdg_surface_listener, &app);

    app.toplevel = xdg_surface_get_toplevel(app.xdg_surface);
    xdg_toplevel_add_listener(app.toplevel, &toplevel_listener, &app);
    xdg_toplevel_set_title(app.toplevel, title == NULL ? "WaylandForge" : title);

    wl_surface_commit(app.surface);

    while (app.running && wl_display_dispatch(app.display) != -1) {
    }

    cleanup(&app);
    return 0;
}
