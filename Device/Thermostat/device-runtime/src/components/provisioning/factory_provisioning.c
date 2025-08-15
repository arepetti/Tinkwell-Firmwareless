#include "app_settings.h"
#include "esp_log.h"
#include "esp_system.h"
#include "netinet/in.h"
#include "networking.h"
#include "provisioning_config.h"
#include "sys/socket.h"
#include <string.h>

static const char* LOG_TAG = "prov";

static struct app_provisioning_t* g_provisioning;
static struct app_manifest_t* g_manifest;
static struct app_settings_t* g_app_settings;
static bool g_pending_changes;
static char g_rx_buffer[128];

typedef void (*tcp_command_handler_t)(int sock, const char* value);

struct command_entry_t {
    const char* key;
    tcp_command_handler_t handler;
};

static void trim_end(char* string) {
    char* end = string + strlen(string) - 1;
    while (end >= string && (*end == '\n' || *end == '\r')) {
        *end-- = '\0';
    }
}

static void handle_save(int sock, const char*) {
    provisioning_save_all_settings();
    g_pending_changes = false;
}

static void handle_complete(int sock, const char* mode) {
    g_provisioning->provisioning_mode = strcmp(mode, "ble") == 0 ? PROVISIONING_MODE_BLE : PROVISIONING_MODE_ETHERNET;
    g_provisioning->provisioning_status = PROVISIONING_CONFIGURED;
}

static void handle_help(int sock, const char* mode);

static void handle_vendor(int sock, const char* value) {
    strlcpy(g_manifest->product_name, value, sizeof g_manifest->product_name);
}

static void handle_product(int sock, const char* value) {
    strlcpy(g_manifest->product_name, value, sizeof g_manifest->product_name);
}

static void handle_model(int sock, const char* value) {
    strlcpy(g_manifest->product_name, value, sizeof g_manifest->product_name);
}

static void handle_model_id(int sock, const char* value) {
    strlcpy(g_manifest->product_name, value, sizeof g_manifest->product_name);
}

static void handle_serial_number(int sock, const char* value) {
    strlcpy(g_manifest->product_name, value, sizeof g_manifest->product_name);
}

static void handle_device_id(int sock, const char* value) {
    strlcpy(g_app_settings->device_id, value, sizeof g_app_settings->device_id);
}

static void handle_broker_address(int sock, const char* value) {
    strlcpy(g_app_settings->mqtt_broker_address, value, sizeof g_app_settings->mqtt_broker_address);
}

static void handle_connection(int sock, const char* value) {
    g_app_settings->connection = strcmp(value, "wifi") == 0 ? NETWORK_CONNECTION_WIFI : NETWORK_CONNECTION_ETHERNET;
}

static void handle_framed_input(int sock, const char* delimiter, char* target, size_t target_size) {
    size_t position = 0;
    memset(target, 0, target_size);
    while (true)
    {
        memset(g_rx_buffer, 0, sizeof g_rx_buffer);
        ssize_t len = recv(sock, g_rx_buffer, sizeof(g_rx_buffer) - 1, 0);
        if (len <= 0 || strncmp(g_rx_buffer, delimiter, strlen(delimiter)) == 0) break;

        trim_end(g_rx_buffer);
        size_t length = strlen(g_rx_buffer);

        if (position + length >= target_size) {
            send(sock, "E2\n", 3, 0);
            return;
        }

        ESP_LOGD(LOG_TAG, "Writing %d bytes starting at %d", length, position);
        memcpy(target + position, g_rx_buffer, length);
        position += length;
    }
}

static void handle_mqtt_server_certificate(int sock, const char* value) {
    handle_framed_input(sock, value, g_app_settings->mqtt_server_certificate, sizeof g_app_settings->mqtt_server_certificate);
}

static void handle_mqtt_client_certificate(int sock, const char* value) {
    handle_framed_input(sock, value, g_app_settings->mqtt_client_certificate, sizeof g_app_settings->mqtt_client_certificate);
}

static void handle_mqtt_client_key_certificate(int sock, const char* value) {
    handle_framed_input(sock, value, g_app_settings->mqtt_client_key_certificate, sizeof g_app_settings->mqtt_client_key_certificate);
}

static void handle_wifi_ssid(int sock, const char* value) {
    strlcpy(g_app_settings->wifi_ssid, value, sizeof g_app_settings->wifi_ssid);
}

static void handle_wifi_password(int sock, const char* value) {
    strlcpy(g_app_settings->wifi_password, value, sizeof g_app_settings->wifi_password);
}

static struct command_entry_t g_commands[] = {
    {"help", handle_help},
    {"save", handle_save},
    {"complete", handle_complete},
    {"vendor", handle_vendor},
    {"product", handle_product},
    {"model", handle_model},
    {"model_id", handle_model_id},
    {"serial_number", handle_serial_number},
    {"device_id", handle_device_id},
    {"broker_address", handle_broker_address},
    {"connection", handle_connection},
    {"mqtt_server_cert", handle_mqtt_server_certificate},
    {"mqtt_client_cert", handle_mqtt_client_certificate},
    {"mqtt_client_key_cert", handle_mqtt_client_key_certificate},
    {"wifi_ssid", handle_wifi_ssid},
    {"wifi_password", handle_wifi_password},
};

static void handle_help(int sock, const char* mode) {
    for (int i = 0; i < sizeof(g_commands) / sizeof(g_commands[0]); ++i) {
        send(sock, g_commands[i].key, strlen(g_commands[i].key), 0);
        send(sock, "\n", 1, 0);
    }
}

static void execute_tcp_command(int sock, const char* command, const char* value) {
    ESP_LOGI(LOG_TAG, "Executing command %s", command);
    for (int i = 0; i < sizeof(g_commands) / sizeof(g_commands[0]); ++i) {
        if (strcmp(command, g_commands[i].key) == 0) {
            g_pending_changes = true;
            g_commands[i].handler(sock, value);
            send(sock, "OK\n", 3, 0);
            return;
        }
    }
    send(sock, "E1\n", 3, 0);
}

void start_factory_privisioning(void) {
    ESP_LOGI(LOG_TAG, "Preparing for factory provisioning...");

    g_app_settings = app_settings_get();
    g_provisioning = provisioning_get_settings();
    g_manifest = provisioning_get_manifest();

    // Setup network configuration: for factory provisioning it's expected
    // to use a wired connection, optionally with DHCP.
    struct network_setup_t setup;
    memset(&setup, 0, sizeof setup);

    setup.connection = NETWORK_CONNECTION_ETHERNET;
#ifdef CONFIG_TW_FPROV_USE_DHCP
    setup.use_dhcp = true;
    setup.dhcp_timeout = CONFIG_TW_FPROV_DHCP_TIMEOUT_MS;
#endif
    strlcpy(setup.static_ip_address, CONFIG_TW_FPROV_STATIC_IP_ADDRESS, IP_ADDRESS_MAX_LENGTH);
    strlcpy(setup.static_ip_netmask, CONFIG_TW_FPROV_STATIC_IP_NET_MASK, IP_ADDRESS_MAX_LENGTH);
    strlcpy(setup.static_ip_gw_address, CONFIG_TW_FPROV_STATIC_IP_GATEWAY_ADDRESS, IP_ADDRESS_MAX_LENGTH);

    ESP_ERROR_CHECK(networking_setup(&setup));

    // Now we start a TCP server socket and wait for provisioning commands
    int addr_family = AF_INET;
    int ip_protocol = IPPROTO_IP;

    struct sockaddr_in dest_addr;
    dest_addr.sin_addr.s_addr = htonl(INADDR_ANY);
    dest_addr.sin_family = AF_INET;
    dest_addr.sin_port = htons(CONFIG_TW_FPROV_TCP_PORT);

    ESP_LOGI(LOG_TAG, "Creating TCP server on port %d", CONFIG_TW_FPROV_TCP_PORT);
    int listen_sock = socket(addr_family, SOCK_STREAM, ip_protocol);
    if (listen_sock <= 0) {
        ESP_LOGE(LOG_TAG, "Cannot create a socket: %x", listen_sock);
        ESP_ERROR_CHECK(ESP_FAIL);
    }

    ESP_ERROR_CHECK(bind(listen_sock, (struct sockaddr*)&dest_addr, sizeof(dest_addr)));
    ESP_ERROR_CHECK(listen(listen_sock, 1));

    ESP_LOGI(LOG_TAG, "Server listening on port %d", CONFIG_TW_FPROV_TCP_PORT);

    bool provisioning_in_progress = true;
    while (provisioning_in_progress) {
        struct sockaddr_in source_addr;
        socklen_t addr_len = sizeof(source_addr);
        int sock = accept(listen_sock, (struct sockaddr*)&source_addr, &addr_len);
        inet_ntoa_r(((struct sockaddr_in*)&source_addr)->sin_addr.s_addr, g_rx_buffer, sizeof(g_rx_buffer) - 1);
        ESP_LOGI(LOG_TAG, "Connection from %s", g_rx_buffer);

        while (true) {
            memset(g_rx_buffer, 0, sizeof g_rx_buffer);
            ssize_t len = recv(sock, g_rx_buffer, sizeof(g_rx_buffer) - 1, 0);
            if (len <= 0) break;

            g_rx_buffer[len] = 0;
            trim_end(g_rx_buffer);
            ESP_LOGD(LOG_TAG, "Received: %s", g_rx_buffer);

            if (strcmp(g_rx_buffer, "exit") == 0) {
                provisioning_in_progress = false;
                break;
            }

            char* equal = strchr(g_rx_buffer, '=');
            if (equal) {
                *equal = 0;
                execute_tcp_command(sock,  g_rx_buffer, equal + 1);
            } else {
                execute_tcp_command(sock, g_rx_buffer, NULL);
            }
        }

        close(sock);
        ESP_LOGI(LOG_TAG, "Client disconnected");
    }

    if (g_provisioning->provisioning_status == PROVISIONING_CONFIGURED && !g_pending_changes) {
        ESP_LOGI(LOG_TAG, "Factory provisioning completed, restarting.");
        esp_restart();
    }
}
