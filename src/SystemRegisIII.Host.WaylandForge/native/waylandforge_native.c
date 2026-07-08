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
#include <wayland-cursor.h>

#include "xdg-shell-client-protocol.h"

typedef uint32_t (*waylandforge_render_callback)(
    uint32_t *pixels,
    int width,
    int height,
    int stride_pixels,
    uint64_t frame_index,
    uint32_t input_mask,
    int32_t pointer_x,
    int32_t pointer_y,
    uint32_t pointer_buttons,
    uint32_t pointer_inside,
    uint32_t key_code,
    uint32_t key_serial,
    uint32_t key_state,
    int32_t scroll_delta,
    uint32_t scroll_serial);

typedef void (*waylandforge_input_callback)(
    uint32_t key_code,
    uint32_t key_serial,
    uint32_t key_state);

enum {
    WAYLANDFORGE_INPUT_ESCAPE = 1u << 0,
    WAYLANDFORGE_INPUT_UP = 1u << 1,
    WAYLANDFORGE_INPUT_DOWN = 1u << 2,
    WAYLANDFORGE_INPUT_LEFT = 1u << 3,
    WAYLANDFORGE_INPUT_RIGHT = 1u << 4,
    WAYLANDFORGE_INPUT_START = 1u << 5,
    WAYLANDFORGE_INPUT_A = 1u << 6,
    WAYLANDFORGE_INPUT_B = 1u << 7,
    WAYLANDFORGE_INPUT_C = 1u << 8,
    WAYLANDFORGE_INPUT_X = 1u << 9,
    WAYLANDFORGE_INPUT_Y = 1u << 10,
    WAYLANDFORGE_INPUT_Z = 1u << 11,
    WAYLANDFORGE_INPUT_SCALE_FIT = 1u << 12,
    WAYLANDFORGE_INPUT_SCALE_INTEGER = 1u << 13,
    WAYLANDFORGE_INPUT_SCALE_STRETCH = 1u << 14,
    WAYLANDFORGE_INPUT_THEME_NEXT = 1u << 15,
    WAYLANDFORGE_INPUT_SHIFT = 1u << 16,
    WAYLANDFORGE_INPUT_SUPER = 1u << 17,
};

struct waylandforge_app;

struct waylandforge_shm_buffer {
    struct wl_buffer *buffer;
    uint32_t *pixels;
    struct waylandforge_app *owner;
    int busy;
    int retired;
    int bytes;
};

struct waylandforge_app {
    struct wl_display *display;
    struct wl_registry *registry;
    struct wl_compositor *compositor;
    struct wl_shm *shm;
    struct wl_seat *seat;
    struct wl_keyboard *keyboard;
    struct wl_pointer *pointer;
    struct xdg_wm_base *wm_base;
    struct wl_surface *surface;
    struct xdg_surface *xdg_surface;
    struct xdg_toplevel *toplevel;
    struct wl_callback *frame_callback;
    struct waylandforge_shm_buffer *buffers[2];
    int width;
    int height;
    int pending_width;
    int pending_height;
    int stride_pixels;
    int configured;
    int running;
    uint32_t input_mask;
    int32_t pointer_x;
    int32_t pointer_y;
    uint32_t pointer_buttons;
    uint32_t pointer_inside;
    uint32_t key_code;
    uint32_t key_serial;
    uint32_t key_state;
    int32_t scroll_delta;
    uint32_t scroll_serial;
    uint64_t frame_index;
    waylandforge_render_callback render;
    waylandforge_input_callback input;
    uint32_t pointer_serial;
    int cursor_hidden;
    int cursor_hidden_applied;
    struct wl_cursor_theme *cursor_theme;
    struct wl_cursor *cursor;
    struct wl_surface *cursor_surface;
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
static void update_cursor(struct waylandforge_app *app);
static int resize_buffers(struct waylandforge_app *app, int width, int height);

static void destroy_shm_buffer(struct waylandforge_shm_buffer *shm_buffer)
{
    if (shm_buffer == NULL) {
        return;
    }

    if (shm_buffer->buffer) {
        wl_buffer_destroy(shm_buffer->buffer);
    }

    if (shm_buffer->pixels) {
        munmap(shm_buffer->pixels, (size_t)shm_buffer->bytes);
    }

    free(shm_buffer);
}

static void retire_shm_buffer(struct waylandforge_shm_buffer *shm_buffer)
{
    if (shm_buffer == NULL) {
        return;
    }

    shm_buffer->owner = NULL;
    if (shm_buffer->busy) {
        shm_buffer->retired = 1;
    } else {
        destroy_shm_buffer(shm_buffer);
    }
}

static void buffer_release(void *data, struct wl_buffer *buffer)
{
    (void)buffer;
    struct waylandforge_shm_buffer *shm_buffer = data;
    shm_buffer->busy = 0;

    if (shm_buffer->retired) {
        destroy_shm_buffer(shm_buffer);
        return;
    }

    if (shm_buffer->owner != NULL && shm_buffer->owner->running && shm_buffer->owner->configured && shm_buffer->owner->frame_callback == NULL) {
        draw_and_commit(shm_buffer->owner);
    }
}

static const struct wl_buffer_listener buffer_listener = {
    .release = buffer_release,
};

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
    if (!app->configured || !app->running || app->frame_callback != NULL) {
        return;
    }

    struct waylandforge_shm_buffer *target = NULL;
    for (size_t i = 0; i < sizeof(app->buffers) / sizeof(app->buffers[0]); i++) {
        if (app->buffers[i] != NULL && !app->buffers[i]->busy) {
            target = app->buffers[i];
            break;
        }
    }

    if (target == NULL) {
        return;
    }

    uint32_t render_flags = app->render(
        target->pixels,
        app->width,
        app->height,
        app->stride_pixels,
        app->frame_index++,
        app->input_mask,
        app->pointer_x,
        app->pointer_y,
        app->pointer_buttons,
        app->pointer_inside,
        app->key_code,
        app->key_serial,
        app->key_state,
        app->scroll_delta,
        app->scroll_serial);
    app->cursor_hidden = (render_flags & 1u) != 0;
    update_cursor(app);
    app->scroll_delta = 0;
    target->busy = 1;

    wl_surface_attach(app->surface, target->buffer, 0, 0);
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

    if (app->pending_width > 0 && app->pending_height > 0 &&
        (app->pending_width != app->width || app->pending_height != app->height)) {
        if (resize_buffers(app, app->pending_width, app->pending_height) != 0) {
            app->running = 0;
            return;
        }

        if (app->frame_callback) {
            wl_callback_destroy(app->frame_callback);
            app->frame_callback = NULL;
        }
    }

    if (!app->configured) {
        app->configured = 1;
    }

    draw_and_commit(app);
}

static const struct xdg_surface_listener xdg_surface_listener = {
    .configure = xdg_surface_configure,
};

static void toplevel_configure(void *data, struct xdg_toplevel *toplevel, int32_t width, int32_t height, struct wl_array *states)
{
    (void)toplevel;
    (void)states;

    struct waylandforge_app *app = data;
    if (width > 0 && height > 0) {
        app->pending_width = width;
        app->pending_height = height;
    }
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

static void ensure_cursor_theme(struct waylandforge_app *app)
{
    if (app->cursor_theme != NULL || app->shm == NULL) {
        return;
    }

    app->cursor_theme = wl_cursor_theme_load(NULL, 24, app->shm);
    if (app->cursor_theme == NULL) {
        return;
    }

    app->cursor = wl_cursor_theme_get_cursor(app->cursor_theme, "left_ptr");
    app->cursor_surface = wl_compositor_create_surface(app->compositor);
}

static void update_cursor(struct waylandforge_app *app)
{
    if (app->pointer == NULL || !app->pointer_inside || app->pointer_serial == 0) {
        app->cursor_hidden_applied = -1;
        return;
    }

    if (app->cursor_hidden == app->cursor_hidden_applied) {
        return;
    }

    if (app->cursor_hidden) {
        wl_pointer_set_cursor(app->pointer, app->pointer_serial, NULL, 0, 0);
        app->cursor_hidden_applied = 1;
        return;
    }

    ensure_cursor_theme(app);
    if (app->cursor == NULL || app->cursor->image_count == 0 || app->cursor_surface == NULL) {
        return;
    }

    struct wl_cursor_image *image = app->cursor->images[0];
    struct wl_buffer *buffer = wl_cursor_image_get_buffer(image);
    wl_pointer_set_cursor(app->pointer, app->pointer_serial, app->cursor_surface, (int32_t)image->hotspot_x, (int32_t)image->hotspot_y);
    wl_surface_attach(app->cursor_surface, buffer, 0, 0);
    wl_surface_damage_buffer(app->cursor_surface, 0, 0, (int32_t)image->width, (int32_t)image->height);
    wl_surface_commit(app->cursor_surface);
    app->cursor_hidden_applied = 0;
}

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
    (void)keyboard;
    (void)serial;
    (void)surface;

    struct waylandforge_app *app = data;
    app->input_mask = 0;
    app->key_code = UINT32_MAX;
    app->key_state = 0;
    app->key_serial++;
    if (app->input) {
        app->input(app->key_code, app->key_serial, app->key_state);
    }
}

static void keyboard_key(void *data, struct wl_keyboard *keyboard, uint32_t serial, uint32_t time_ms, uint32_t key, uint32_t state)
{
    (void)keyboard;
    (void)serial;
    (void)time_ms;

    struct waylandforge_app *app = data;
    uint32_t bit = 0;
    switch (key) {
    case KEY_ESC:
        bit = WAYLANDFORGE_INPUT_ESCAPE;
        break;
    case KEY_UP:
        bit = WAYLANDFORGE_INPUT_UP;
        break;
    case KEY_DOWN:
        bit = WAYLANDFORGE_INPUT_DOWN;
        break;
    case KEY_LEFT:
        bit = WAYLANDFORGE_INPUT_LEFT;
        break;
    case KEY_RIGHT:
        bit = WAYLANDFORGE_INPUT_RIGHT;
        break;
    case KEY_ENTER:
        bit = WAYLANDFORGE_INPUT_START;
        break;
    case KEY_Z:
        bit = WAYLANDFORGE_INPUT_A;
        break;
    case KEY_X:
        bit = WAYLANDFORGE_INPUT_B;
        break;
    case KEY_C:
        bit = WAYLANDFORGE_INPUT_C;
        break;
    case KEY_A:
        bit = WAYLANDFORGE_INPUT_X;
        break;
    case KEY_S:
        bit = WAYLANDFORGE_INPUT_Y;
        break;
    case KEY_D:
        bit = WAYLANDFORGE_INPUT_Z;
        break;
    case KEY_1:
        bit = WAYLANDFORGE_INPUT_SCALE_FIT;
        break;
    case KEY_2:
        bit = WAYLANDFORGE_INPUT_SCALE_INTEGER;
        break;
    case KEY_3:
        bit = WAYLANDFORGE_INPUT_SCALE_STRETCH;
        break;
    case KEY_T:
        bit = WAYLANDFORGE_INPUT_THEME_NEXT;
        break;
    case KEY_LEFTSHIFT:
    case KEY_RIGHTSHIFT:
        bit = WAYLANDFORGE_INPUT_SHIFT;
        break;
    case KEY_LEFTMETA:
    case KEY_RIGHTMETA:
        bit = WAYLANDFORGE_INPUT_SUPER;
        break;
    default:
        break;
    }

    if (bit != 0) {
        if (state == WL_KEYBOARD_KEY_STATE_PRESSED) {
            app->input_mask |= bit;
        } else {
            app->input_mask &= ~bit;
        }
    }

    app->key_code = key;
    app->key_state = state == WL_KEYBOARD_KEY_STATE_PRESSED ? 1u : 0u;
    app->key_serial++;
    if (app->input) {
        app->input(app->key_code, app->key_serial, app->key_state);
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

static void pointer_enter(void *data, struct wl_pointer *pointer, uint32_t serial, struct wl_surface *surface, wl_fixed_t surface_x, wl_fixed_t surface_y)
{
    (void)pointer;
    (void)surface;

    struct waylandforge_app *app = data;
    app->pointer_serial = serial;
    app->pointer_inside = 1;
    app->pointer_x = wl_fixed_to_int(surface_x);
    app->pointer_y = wl_fixed_to_int(surface_y);
    app->cursor_hidden_applied = -1;
    update_cursor(app);
}

static void pointer_leave(void *data, struct wl_pointer *pointer, uint32_t serial, struct wl_surface *surface)
{
    (void)pointer;
    (void)serial;
    (void)surface;

    struct waylandforge_app *app = data;
    app->pointer_inside = 0;
    app->pointer_buttons = 0;
    app->cursor_hidden_applied = -1;
}

static void pointer_motion(void *data, struct wl_pointer *pointer, uint32_t time_ms, wl_fixed_t surface_x, wl_fixed_t surface_y)
{
    (void)pointer;
    (void)time_ms;

    struct waylandforge_app *app = data;
    app->pointer_x = wl_fixed_to_int(surface_x);
    app->pointer_y = wl_fixed_to_int(surface_y);
}

static void pointer_button(void *data, struct wl_pointer *pointer, uint32_t serial, uint32_t time_ms, uint32_t button, uint32_t state)
{
    (void)pointer;
    (void)serial;
    (void)time_ms;

    struct waylandforge_app *app = data;
    uint32_t bit = 0;
    switch (button) {
    case BTN_LEFT:
        bit = 1u << 0;
        break;
    case BTN_RIGHT:
        bit = 1u << 1;
        break;
    case BTN_MIDDLE:
        bit = 1u << 2;
        break;
    default:
        break;
    }

    if (bit != 0) {
        if (state == WL_POINTER_BUTTON_STATE_PRESSED) {
            app->pointer_buttons |= bit;
        } else {
            app->pointer_buttons &= ~bit;
        }
    }
}

static void pointer_axis(void *data, struct wl_pointer *pointer, uint32_t time_ms, uint32_t axis, wl_fixed_t value)
{
    (void)pointer;
    (void)time_ms;

    struct waylandforge_app *app = data;
    if (axis == WL_POINTER_AXIS_VERTICAL_SCROLL) {
        int delta = wl_fixed_to_int(value);
        if (delta == 0) {
            delta = value > 0 ? 1 : -1;
        }
        app->scroll_delta += delta * 18;
        app->scroll_serial++;
    }
}

static void pointer_frame(void *data, struct wl_pointer *pointer)
{
    (void)data;
    (void)pointer;
}

static void pointer_axis_source(void *data, struct wl_pointer *pointer, uint32_t axis_source)
{
    (void)data;
    (void)pointer;
    (void)axis_source;
}

static void pointer_axis_stop(void *data, struct wl_pointer *pointer, uint32_t time_ms, uint32_t axis)
{
    (void)data;
    (void)pointer;
    (void)time_ms;
    (void)axis;
}

static void pointer_axis_discrete(void *data, struct wl_pointer *pointer, uint32_t axis, int32_t discrete)
{
    (void)data;
    (void)pointer;
    (void)axis;
    (void)discrete;
}

static const struct wl_pointer_listener pointer_listener = {
    .enter = pointer_enter,
    .leave = pointer_leave,
    .motion = pointer_motion,
    .button = pointer_button,
    .axis = pointer_axis,
    .frame = pointer_frame,
    .axis_source = pointer_axis_source,
    .axis_stop = pointer_axis_stop,
    .axis_discrete = pointer_axis_discrete,
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

    if ((capabilities & WL_SEAT_CAPABILITY_POINTER) && app->pointer == NULL) {
        app->pointer = wl_seat_get_pointer(seat);
        wl_pointer_add_listener(app->pointer, &pointer_listener, app);
    } else if (!(capabilities & WL_SEAT_CAPABILITY_POINTER) && app->pointer != NULL) {
        wl_pointer_destroy(app->pointer);
        app->pointer = NULL;
        app->pointer_inside = 0;
        app->pointer_buttons = 0;
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

static struct waylandforge_shm_buffer *create_shm_buffer(struct waylandforge_app *app)
{
    struct waylandforge_shm_buffer *target = calloc(1, sizeof(*target));
    if (target == NULL) {
        return NULL;
    }

    int stride_bytes = app->stride_pixels * 4;
    target->owner = app;
    target->bytes = stride_bytes * app->height;

    int fd = make_shm_file((size_t)target->bytes);
    if (fd < 0) {
        free(target);
        return NULL;
    }

    target->pixels = mmap(NULL, (size_t)target->bytes, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
    if (target->pixels == MAP_FAILED) {
        perror("mmap");
        target->pixels = NULL;
        close(fd);
        free(target);
        return NULL;
    }

    struct wl_shm_pool *pool = wl_shm_create_pool(app->shm, fd, target->bytes);
    target->buffer = wl_shm_pool_create_buffer(pool, 0, app->width, app->height, stride_bytes, WL_SHM_FORMAT_ARGB8888);
    if (target->buffer != NULL) {
        wl_buffer_add_listener(target->buffer, &buffer_listener, target);
    }
    wl_shm_pool_destroy(pool);
    close(fd);

    if (target->buffer == NULL) {
        destroy_shm_buffer(target);
        return NULL;
    }

    return target;
}

static int resize_buffers(struct waylandforge_app *app, int width, int height)
{
    if (width < 1) {
        width = 1;
    }
    if (height < 1) {
        height = 1;
    }

    if (app->buffers[0] != NULL && app->buffers[1] != NULL &&
        app->width == width && app->height == height) {
        return 0;
    }

    struct waylandforge_shm_buffer *old_buffers[2] = { app->buffers[0], app->buffers[1] };
    int old_width = app->width;
    int old_height = app->height;
    int old_stride_pixels = app->stride_pixels;
    app->buffers[0] = NULL;
    app->buffers[1] = NULL;

    app->width = width;
    app->height = height;
    app->stride_pixels = app->width;

    for (size_t i = 0; i < sizeof(app->buffers) / sizeof(app->buffers[0]); i++) {
        app->buffers[i] = create_shm_buffer(app);
        if (app->buffers[i] == NULL) {
            for (size_t j = 0; j < sizeof(app->buffers) / sizeof(app->buffers[0]); j++) {
                retire_shm_buffer(app->buffers[j]);
                app->buffers[j] = NULL;
            }

            app->buffers[0] = old_buffers[0];
            app->buffers[1] = old_buffers[1];
            app->width = old_width;
            app->height = old_height;
            app->stride_pixels = old_stride_pixels;
            return -1;
        }
    }

    for (size_t i = 0; i < sizeof(old_buffers) / sizeof(old_buffers[0]); i++) {
        retire_shm_buffer(old_buffers[i]);
    }

    return 0;
}

static void cleanup(struct waylandforge_app *app)
{
    if (app->frame_callback) {
        wl_callback_destroy(app->frame_callback);
    }
    for (size_t i = 0; i < sizeof(app->buffers) / sizeof(app->buffers[0]); i++) {
        destroy_shm_buffer(app->buffers[i]);
        app->buffers[i] = NULL;
    }
    if (app->keyboard) {
        wl_keyboard_destroy(app->keyboard);
    }
    if (app->pointer) {
        wl_pointer_destroy(app->pointer);
    }
    if (app->seat) {
        wl_seat_destroy(app->seat);
    }
    if (app->cursor_surface) {
        wl_surface_destroy(app->cursor_surface);
    }
    if (app->cursor_theme) {
        wl_cursor_theme_destroy(app->cursor_theme);
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

int waylandforge_run(int width, int height, const char *title, waylandforge_render_callback render, waylandforge_input_callback input)
{
    if (width <= 0 || height <= 0 || render == NULL) {
        return 2;
    }

    struct waylandforge_app app = {
        .width = width,
        .height = height,
        .running = 1,
        .render = render,
        .input = input,
        .cursor_hidden_applied = -1,
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

    if (resize_buffers(&app, app.width, app.height) != 0) {
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
