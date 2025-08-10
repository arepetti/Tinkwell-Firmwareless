#include "settings.h"
#include "nvs_flash.h"
#include "nvs.h"
#include "esp_log.h"

static const char* SETTINGS_LOG_TAG = "settings";

#define NVS_PARTITION "config"
#define NVS_NAMESPACE "default"

esp_err_t settings_initialize(void)
{
    esp_err_t err = nvs_flash_init();
    if (err != ESP_OK)
        return err;

    return ESP_OK;
}

esp_err_t settings_load(const char* key, set_defaults_fn_t set_defaults, struct settings_t* config, size_t settings_size, int8_t schema_version)
{
    ESP_LOGI(SETTINGS_LOG_TAG, "Loading %s config...", key);

    esp_err_t err = nvs_flash_init_partition(NVS_PARTITION);
    if (err == ESP_ERR_NVS_NO_FREE_PAGES || err == ESP_ERR_NVS_NEW_VERSION_FOUND || err ==  ESP_ERR_NVS_KEYS_NOT_INITIALIZED)
    {
        ESP_LOGW(SETTINGS_LOG_TAG, "Reinitializing app settings partition %s because 0x%x...", NVS_PARTITION, err);
        ESP_ERROR_CHECK(nvs_flash_erase_partition(NVS_PARTITION));
        err = nvs_flash_init_partition(NVS_PARTITION);
    }

    if (err != ESP_OK)
    {
        ESP_LOGE(SETTINGS_LOG_TAG, "Failed to initialize because 0x%x...", err);
        return err;
    }

    nvs_handle_t handle;
    err = nvs_open_from_partition(NVS_PARTITION, NVS_NAMESPACE, NVS_READONLY, &handle);
    if (err == ESP_ERR_NVS_NOT_FOUND)
        err = nvs_open_from_partition(NVS_PARTITION, NVS_NAMESPACE, NVS_READWRITE, &handle);

    if (err != ESP_OK)
    {
        ESP_LOGE(SETTINGS_LOG_TAG, "Failed to open partition because 0x%x...", err);
        return err;
    }

    err = nvs_get_blob(handle, key, config, &settings_size);
    if (err == ESP_ERR_NVS_NOT_FOUND || config->version != schema_version)
    {
        set_defaults(config);
        err = ESP_OK;
    }

    if (err != ESP_OK)
        ESP_LOGE(SETTINGS_LOG_TAG, "Failed to load blob because 0x%x...", err);

    nvs_close(handle);
    return err;
}

esp_err_t settings_save(const char* key, struct settings_t* settings, size_t settings_size)
{
    nvs_handle_t handle;
    esp_err_t err = nvs_open_from_partition(NVS_PARTITION, NVS_NAMESPACE, NVS_READWRITE, &handle);
    if (err != ESP_OK)
    {
        ESP_LOGE(SETTINGS_LOG_TAG, "Failed to open partition because 0x%x...", err);
        return err;
    }

    err = nvs_set_blob(handle, key, settings, settings_size);
    if (err == ESP_OK)
        nvs_commit(handle);
    else
        ESP_LOGE(SETTINGS_LOG_TAG, "Failed to save blob because 0x%x...", err);

    nvs_close(handle);
    return err;
}
