#pragma once

#include "settings.h"

enum provisioning_status_t
{
    PROVISIONING_UNINITIALIZED,
    PROVISIONING_CONFIGURED,
    PROVISIONING_PROVISIONED
};

enum provisioning_mode_t
{
    PROVISIONING_MODE_ETHERNET,
    PROVISIONING_MODE_BLE,
};

struct app_provisioning_t
{
    struct settings_t schema;
    enum provisioning_status_t provisioning_status;
    enum provisioning_mode_t provisioning_mode;
};

struct app_manifest_t
{
    struct settings_t schema;
    char vendor[32];
    char product_name[32];
    char model_name[32];
    int32_t model_id;
    char serial_number[40];
};

struct app_manifest_t* provisioning_get_manifest(void);
struct app_provisioning_t* provisioning_get_settings(void);
void provisioning_save_all_settings(void);
void start_factory_privisioning(void);
