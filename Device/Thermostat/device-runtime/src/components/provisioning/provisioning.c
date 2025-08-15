#include "include/provisioning.h"
#include "app_settings.h"
#include "esp_log.h"
#include "networking.h"
#include "provisioning_config.h"
#include "settings.h"
#include <string.h>

#define SCHEMA_VERSION 1

static const char* LOG_TAG = "prov";

static struct app_provisioning_t g_provisioning;
static struct app_manifest_t g_manifest;
static struct app_settings_t* g_app_settings;

void set_manifest_defaults(struct settings_t* settings) {
    memset(settings, 0, sizeof g_manifest);
    settings->version = SCHEMA_VERSION;

    // We set these defaults for simplicity but in the real world they'd
    // omitted here and written during factory provisioning (or, some, being
    // set with CONFIG_ options, not from code).
#if CONFIG_TW_USE_OPENETH
    struct app_manifest_t* manifest = (struct app_manifest_t*)settings;
    strlcpy(manifest->vendor, "Tinkwell", sizeof manifest->vendor);
    strlcpy(manifest->product_name, "Smart Thermostat", sizeof manifest->product_name);
    strlcpy(manifest->model_name, "Basic", sizeof manifest->model_name);
    strlcpy(manifest->serial_number, "1234", sizeof manifest->serial_number);
#endif
}

void set_provisioning_defaults(struct settings_t* settings) {
    memset(settings, 0, sizeof g_provisioning);
    settings->version = SCHEMA_VERSION;

    // These settings are just to speed-up testing with QEMU
#if CONFIG_TW_USE_OPENETH
    ((struct app_provisioning_t*)settings)->provisioning_mode = PROVISIONING_MODE_ETHERNET;
#endif
}

struct app_manifest_t* provisioning_get_manifest(void) {
    return &g_manifest;
}

struct app_provisioning_t* provisioning_get_settings(void) {
    return &g_provisioning;
}

esp_err_t provisioning_initialize(void) {
    ESP_ERROR_CHECK(settings_load("manifest", set_manifest_defaults, (struct settings_t*)&g_manifest, sizeof g_manifest,
                                  SCHEMA_VERSION));

    ESP_ERROR_CHECK(settings_load("provisioning", set_provisioning_defaults, (struct settings_t*)&g_provisioning,
                                  sizeof g_provisioning, SCHEMA_VERSION));

    g_app_settings = app_settings_get();

    ESP_LOGI(LOG_TAG, "Device: %s %s, model %s (%d))", g_manifest.vendor, g_manifest.product_name,
             g_manifest.model_name, g_manifest.model_id);
    ESP_LOGI(LOG_TAG, "Serial number: %s", g_manifest.serial_number);
    ESP_LOGI(LOG_TAG, "Device ID: %s", g_app_settings->device_id);
    ESP_LOGI(LOG_TAG, "MQTT Broker address: %s", g_app_settings->mqtt_broker_address);

    return ESP_OK;
}

void provisioning_save_all_settings(void) {
    ESP_ERROR_CHECK(app_settings_save());
    ESP_ERROR_CHECK(settings_save("manifest", (struct settings_t*)&g_manifest, sizeof g_manifest));
    ESP_ERROR_CHECK(settings_save("provisioning", (struct settings_t*)&g_provisioning, sizeof g_provisioning));
}

esp_err_t start_operational_privisioning(void) {
    ESP_LOGI(LOG_TAG, "Preparing for operational provisioning...");

    return ESP_OK;
}

esp_err_t provisioning_wait_if_needed(void) {
    switch (g_provisioning.provisioning_status) {
    case PROVISIONING_PROVISIONED:
        ESP_LOGI(LOG_TAG, "Device is operational");
        return ESP_OK;
    case PROVISIONING_CONFIGURED:
        return start_operational_privisioning();
    default:
        start_factory_privisioning();
        return ESP_OK;
    }
}
