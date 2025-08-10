#include "app_settings.h"
#include <string.h>

#define SCHEMA_VERSION 1

static struct app_settings_t g_settings;

void set_defaults(struct settings_t* settings)
{
    memset(settings, 0, sizeof g_settings);
    settings->version = SCHEMA_VERSION;

    // These settings are just to speed-up testing with QEMU
#if CONFIG_TW_USE_OPENETH
    struct app_settings_t* prov = (struct app_settings_t*)settings;
    strlcpy(prov->device_id, CONFIG_TW_PROVISIONING_DEFAULT_DEVICE_ID, sizeof prov->device_id);
    strlcpy(prov->mqtt_broker_address, CONFIG_TW_FPROV_STATIC_IP_GATEWAY_ADDRESS, sizeof prov->mqtt_broker_address);
#endif
}

struct app_settings_t* app_settings_get(void)
{
    if (g_settings.schema.version != SCHEMA_VERSION)
    {
        ESP_ERROR_CHECK(settings_load(
            "app",
            set_defaults,
            (struct settings_t*)&g_settings,
            sizeof g_settings,
            SCHEMA_VERSION
        ));
    }

    return &g_settings;
}

esp_err_t app_settings_save(void)
{
    return settings_save("app", (struct settings_t*)&g_settings, sizeof g_settings);
}
