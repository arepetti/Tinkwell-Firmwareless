#pragma once

#include "esp_err.h"
#include <stdbool.h>

#define IP_ADDRESS_MAX_LENGTH 40

enum network_connection {
    NETWORK_CONNECTION_ETHERNET,
    NETWORK_CONNECTION_WIFI,
};

enum network_nic {
    NETWORK_NIC_DEFAULT,
    NETWORK_NIC_INTERNAL,
    NETWORK_NIC_SPI1,
    NETWORK_NIC_SPI2,
    NETWORK_NIC_OPEN_CORES,
};

struct network_setup_t {
    enum network_connection connection;
    enum network_nic primary_nic;
    bool use_dhcp;
    int dhcp_timeout;
    char static_ip_address[IP_ADDRESS_MAX_LENGTH];
    char static_ip_netmask[IP_ADDRESS_MAX_LENGTH];
    char static_ip_gw_address[IP_ADDRESS_MAX_LENGTH];
};

esp_err_t networking_initialize(void);
esp_err_t networking_setup(const struct network_setup_t* setup);
