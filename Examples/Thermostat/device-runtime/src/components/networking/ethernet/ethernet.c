#include "ethernet_init.h"
#include "esp_err.h"
#include "esp_eth.h"
#include "esp_log.h"
#include "ethernet.h"

static const char* LOG_TAG = "net_eth";

#if CONFIG_TW_USE_SPI_ETHERNET && CONFIG_TW_SPI_ETHERNETS_NUM > 2
#error Maximum number of supported SPI Ethernet devices is currently limited to 2 by this application.
#endif

esp_err_t ethernet_init(struct ethernet_interface* interfaces[], size_t* interface_count) {

#if CONFIG_TW_USE_OPENETH
    *interface_count = 1;
#else
    *interface_count = INTERNAL_ETHERNETS_NUM + SPI_ETHERNETS_NUM;
#endif

    *interfaces = malloc(*interface_count * sizeof(struct ethernet_interface));

#if CONFIG_TW_USE_OPENETH
    ESP_LOGI(LOG_TAG, "Initializing OpenCores card...");
    *interfaces[0] = ethernet_init_openeth();
#elif CONFIG_TW_USE_INTERNAL_ETHERNET || CONFIG_TW_USE_SPI_ETHERNET
#if CONFIG_TW_USE_INTERNAL_ETHERNET
    ESP_LOGI(LOG_TAG, "Initializing internal card...");
    *interfaces[0] = ethernet_init_internal();
#endif

#if CONFIG_TW_USE_SPI_ETHERNET
    ESP_LOGI(LOG_TAG, "Initializing SPI card(s)...");
    ethernet_init_spi(interfaces, INTERNAL_ETHERNETS_NUM);
#endif
#else
    ESP_LOGD(LOG_TAG, "No Ethernet device selected to init");
#endif

    return ESP_OK;
}
