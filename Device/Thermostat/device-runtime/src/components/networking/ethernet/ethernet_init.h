#pragma once

#include "ethernet.h"
#include <stdint.h>

#if CONFIG_TW_SPI_ETHERNETS_NUM
#define SPI_ETHERNETS_NUM CONFIG_TW_SPI_ETHERNETS_NUM
#else
#define SPI_ETHERNETS_NUM 0
#endif

#if CONFIG_TW_USE_INTERNAL_ETHERNET
#define INTERNAL_ETHERNETS_NUM 1
#else
#define INTERNAL_ETHERNETS_NUM 0
#endif

#if CONFIG_TW_USE_SPI_ETHERNET
void  ethernet_init_spi(struct ethernet_interface* interfaces[], size_t start_index);
#endif

#if CONFIG_TW_USE_INTERNAL_ETHERNET
struct ethernet_interface ethernet_init_internal(void);
#endif

#if CONFIG_TW_USE_OPENETH
struct ethernet_interface ethernet_init_openeth(void);
#endif