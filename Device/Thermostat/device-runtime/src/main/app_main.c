#include "settings.h"
#include "networking.h"
#include "provisioning.h"
#include "esp_log.h"

static const char* APP_LOG_TAG = "app";

void app_main(void)
{
    ESP_LOGI(APP_LOG_TAG, "Initializing components...");
    ESP_ERROR_CHECK(settings_initialize());
    ESP_ERROR_CHECK(networking_initialize());
    ESP_ERROR_CHECK(provisioning_initialize());

    ESP_LOGI(APP_LOG_TAG, "Starting...");
    ESP_ERROR_CHECK(provisioning_wait_if_needed());

    ESP_LOGI(APP_LOG_TAG, "Application started");
}
