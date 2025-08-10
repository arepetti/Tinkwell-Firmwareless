#pragma once

#include "esp_err.h"
#include "settings.h"
#include "networking.h"

struct app_settings_t
{
    struct settings_t schema;
    enum network_connection connection;
    char device_id[40];
    char mqtt_broker_address[46];
    char mqtt_server_certificate[4 * 1024];
    char mqtt_client_certificate[4 * 1024];
    char mqtt_client_key_certificate[2 * 1024];
    char wifi_ssid[33];
    char wifi_password[64];
};

struct app_settings_t* app_settings_get(void);
esp_err_t app_settings_save(void);
