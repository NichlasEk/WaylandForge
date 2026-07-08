#include <errno.h>
#include <math.h>
#include <pthread.h>
#include <signal.h>
#include <stdatomic.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <unistd.h>

#include <pipewire/pipewire.h>
#include <spa/param/audio/format-utils.h>
#include <spa/param/audio/raw.h>
#include <spa/pod/builder.h>
#include <spa/utils/result.h>

#define SAMPLE_RATE 48000
#define CHANNELS 2
#define RING_FRAMES (SAMPLE_RATE * 4)
#define SOCKET_PATH "/tmp/waylandforge-audio.sock"
#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

struct audio_ring {
    float samples[RING_FRAMES * CHANNELS];
    atomic_size_t read_frame;
    atomic_size_t write_frame;
};

struct audiod {
    struct pw_main_loop *main_loop;
    struct pw_stream *stream;
    struct audio_ring ring;
    pthread_t socket_thread;
    atomic_bool running;
};

static struct audiod *g_daemon;

static size_t ring_next(size_t frame)
{
    return (frame + 1) % RING_FRAMES;
}

static bool ring_push_frame(struct audio_ring *ring, float left, float right)
{
    size_t write_frame = atomic_load_explicit(&ring->write_frame, memory_order_relaxed);
    size_t next = ring_next(write_frame);
    size_t read_frame = atomic_load_explicit(&ring->read_frame, memory_order_acquire);
    if (next == read_frame) {
        return false;
    }

    ring->samples[write_frame * CHANNELS + 0] = left;
    ring->samples[write_frame * CHANNELS + 1] = right;
    atomic_store_explicit(&ring->write_frame, next, memory_order_release);
    return true;
}

static bool ring_pop_frame(struct audio_ring *ring, float *left, float *right)
{
    size_t read_frame = atomic_load_explicit(&ring->read_frame, memory_order_relaxed);
    size_t write_frame = atomic_load_explicit(&ring->write_frame, memory_order_acquire);
    if (read_frame == write_frame) {
        return false;
    }

    *left = ring->samples[read_frame * CHANNELS + 0];
    *right = ring->samples[read_frame * CHANNELS + 1];
    atomic_store_explicit(&ring->read_frame, ring_next(read_frame), memory_order_release);
    return true;
}

static void enqueue_test_click(struct audiod *daemon)
{
    const int frames = SAMPLE_RATE / 5;
    const double frequency = 880.0;

    for (int i = 0; i < frames; i++) {
        double t = (double)i / (double)SAMPLE_RATE;
        double fade = 1.0 - ((double)i / (double)frames);
        float sample = (float)(sin(2.0 * M_PI * frequency * t) * 0.22 * fade);
        if (!ring_push_frame(&daemon->ring, sample, sample)) {
            break;
        }
    }
}

static void on_process(void *data)
{
    struct audiod *daemon = data;
    struct pw_buffer *buffer = pw_stream_dequeue_buffer(daemon->stream);
    if (buffer == NULL) {
        return;
    }

    struct spa_buffer *spa_buffer = buffer->buffer;
    if (spa_buffer->n_datas == 0 || spa_buffer->datas[0].data == NULL) {
        pw_stream_queue_buffer(daemon->stream, buffer);
        return;
    }

    float *out = spa_buffer->datas[0].data;
    uint32_t max_frames = spa_buffer->datas[0].maxsize / (sizeof(float) * CHANNELS);
    uint32_t frames = buffer->requested > 0 && buffer->requested < max_frames
        ? buffer->requested
        : max_frames;

    for (uint32_t i = 0; i < frames; i++) {
        float left = 0.0f;
        float right = 0.0f;
        ring_pop_frame(&daemon->ring, &left, &right);
        out[i * CHANNELS + 0] = left;
        out[i * CHANNELS + 1] = right;
    }

    if (spa_buffer->datas[0].chunk != NULL) {
        spa_buffer->datas[0].chunk->offset = 0;
        spa_buffer->datas[0].chunk->stride = sizeof(float) * CHANNELS;
        spa_buffer->datas[0].chunk->size = frames * sizeof(float) * CHANNELS;
    }

    pw_stream_queue_buffer(daemon->stream, buffer);
}

static void on_state_changed(void *data, enum pw_stream_state old_state,
                             enum pw_stream_state state, const char *error)
{
    (void)data;
    fprintf(stderr, "pipewire stream: %s -> %s",
            pw_stream_state_as_string(old_state),
            pw_stream_state_as_string(state));
    if (error != NULL) {
        fprintf(stderr, " (%s)", error);
    }
    fputc('\n', stderr);
}

static const struct pw_stream_events stream_events = {
    PW_VERSION_STREAM_EVENTS,
    .state_changed = on_state_changed,
    .process = on_process,
};

static void handle_client(struct audiod *daemon, int client_fd)
{
    char command[256];
    ssize_t count = read(client_fd, command, sizeof(command) - 1);
    if (count <= 0) {
        return;
    }
    command[count] = '\0';

    if (strstr(command, "PLAY_TEST") != NULL) {
        enqueue_test_click(daemon);
        (void)write(client_fd, "OK PLAY_TEST\n", 13);
        return;
    }
    if (strstr(command, "PING") != NULL) {
        (void)write(client_fd, "PONG\n", 5);
        return;
    }

    (void)write(client_fd, "ERR UNKNOWN\n", 12);
}

static void *socket_thread_main(void *arg)
{
    struct audiod *daemon = arg;
    int server_fd = socket(AF_UNIX, SOCK_STREAM, 0);
    if (server_fd < 0) {
        perror("socket");
        return NULL;
    }

    unlink(SOCKET_PATH);

    struct sockaddr_un addr;
    memset(&addr, 0, sizeof(addr));
    addr.sun_family = AF_UNIX;
    snprintf(addr.sun_path, sizeof(addr.sun_path), "%s", SOCKET_PATH);

    if (bind(server_fd, (struct sockaddr *)&addr, sizeof(addr)) != 0) {
        perror("bind");
        close(server_fd);
        return NULL;
    }

    if (listen(server_fd, 8) != 0) {
        perror("listen");
        close(server_fd);
        unlink(SOCKET_PATH);
        return NULL;
    }

    fprintf(stderr, "audio command socket: %s\n", SOCKET_PATH);

    while (atomic_load_explicit(&daemon->running, memory_order_acquire)) {
        int client_fd = accept(server_fd, NULL, NULL);
        if (client_fd < 0) {
            if (errno == EINTR) {
                continue;
            }
            perror("accept");
            break;
        }

        handle_client(daemon, client_fd);
        close(client_fd);
    }

    close(server_fd);
    unlink(SOCKET_PATH);
    return NULL;
}

static void handle_signal(int signal_number)
{
    (void)signal_number;
    if (g_daemon != NULL) {
        atomic_store_explicit(&g_daemon->running, false, memory_order_release);
        if (g_daemon->main_loop != NULL) {
            pw_main_loop_quit(g_daemon->main_loop);
        }
    }
}

static void wake_socket_thread(void)
{
    int fd = socket(AF_UNIX, SOCK_STREAM, 0);
    if (fd < 0) {
        return;
    }

    struct sockaddr_un addr;
    memset(&addr, 0, sizeof(addr));
    addr.sun_family = AF_UNIX;
    snprintf(addr.sun_path, sizeof(addr.sun_path), "%s", SOCKET_PATH);
    (void)connect(fd, (struct sockaddr *)&addr, sizeof(addr));
    close(fd);
}

int main(int argc, char **argv)
{
    (void)argc;
    (void)argv;

    struct audiod daemon;
    memset(&daemon, 0, sizeof(daemon));
    atomic_store(&daemon.running, true);
    g_daemon = &daemon;

    signal(SIGINT, handle_signal);
    signal(SIGTERM, handle_signal);

    pw_init(&argc, &argv);

    daemon.main_loop = pw_main_loop_new(NULL);
    if (daemon.main_loop == NULL) {
        fprintf(stderr, "failed to create PipeWire main loop\n");
        return 1;
    }

    daemon.stream = pw_stream_new_simple(
        pw_main_loop_get_loop(daemon.main_loop),
        "waylandforge-audiod",
        pw_properties_new(
            PW_KEY_MEDIA_TYPE, "Audio",
            PW_KEY_MEDIA_CATEGORY, "Playback",
            PW_KEY_MEDIA_ROLE, "Game",
            PW_KEY_NODE_NAME, "waylandforge-audiod",
            PW_KEY_NODE_DESCRIPTION, "EutherAudio Sinklet",
            NULL),
        &stream_events,
        &daemon);
    if (daemon.stream == NULL) {
        fprintf(stderr, "failed to create PipeWire stream\n");
        pw_main_loop_destroy(daemon.main_loop);
        return 1;
    }

    uint8_t buffer[1024];
    struct spa_pod_builder builder = SPA_POD_BUILDER_INIT(buffer, sizeof(buffer));
    struct spa_audio_info_raw info = {
        .format = SPA_AUDIO_FORMAT_F32,
        .rate = SAMPLE_RATE,
        .channels = CHANNELS,
        .position = { SPA_AUDIO_CHANNEL_FL, SPA_AUDIO_CHANNEL_FR },
    };
    const struct spa_pod *params[1];
    params[0] = spa_format_audio_raw_build(&builder, SPA_PARAM_EnumFormat, &info);

    int result = pw_stream_connect(
        daemon.stream,
        PW_DIRECTION_OUTPUT,
        PW_ID_ANY,
        PW_STREAM_FLAG_AUTOCONNECT |
            PW_STREAM_FLAG_MAP_BUFFERS |
            PW_STREAM_FLAG_RT_PROCESS,
        params,
        1);
    if (result < 0) {
        fprintf(stderr, "failed to connect PipeWire stream: %s\n", spa_strerror(result));
        pw_stream_destroy(daemon.stream);
        pw_main_loop_destroy(daemon.main_loop);
        return 1;
    }

    if (pthread_create(&daemon.socket_thread, NULL, socket_thread_main, &daemon) != 0) {
        perror("pthread_create");
        pw_stream_destroy(daemon.stream);
        pw_main_loop_destroy(daemon.main_loop);
        return 1;
    }

    fprintf(stderr, "waylandforge-audiod running\n");
    pw_main_loop_run(daemon.main_loop);

    atomic_store_explicit(&daemon.running, false, memory_order_release);
    wake_socket_thread();
    pthread_join(daemon.socket_thread, NULL);

    pw_stream_destroy(daemon.stream);
    pw_main_loop_destroy(daemon.main_loop);
    pw_deinit();
    unlink(SOCKET_PATH);
    return 0;
}
