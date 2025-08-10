#pragma once

#include "esp_err.h"

esp_err_t provisioning_initialize(void);
esp_err_t provisioning_wait_if_needed(void);
