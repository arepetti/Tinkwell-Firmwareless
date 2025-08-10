#pragma once

#include "esp_err.h"

struct settings_t
{
    int8_t version;
};

typedef void (*set_defaults_fn_t)(struct settings_t* settings);

esp_err_t settings_initialize(void);
esp_err_t settings_load(const char* key, set_defaults_fn_t set_defaults, struct settings_t* settings, size_t settings_size, int8_t schema_version);
esp_err_t settings_save(const char* key, struct settings_t* settings, size_t settings_size);
