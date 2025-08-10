#pragma once

#include "esp_eth_driver.h"
#include "esp_netif_types.h"

struct ethernet_interface {
    esp_netif_t* interface;
    esp_eth_handle_t handle;
};

esp_err_t ethernet_init(struct ethernet_interface* interfaces[], size_t* interface_count);
