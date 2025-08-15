#include "driver/gpio.h"
#include "esp_eth.h"
#include "esp_event.h"
#include "esp_log.h"
#include "esp_mac.h"
#include "esp_netif.h"
#include "ethernet_init.h"

static const char* LOG_TAG = "net_eth";

struct ethernet_interface ethernet_init_openeth(void) {
    ESP_LOGI(LOG_TAG, "Creating interface OPC_ETH1...");
    esp_netif_inherent_config_t esp_netif_config = ESP_NETIF_INHERENT_DEFAULT_ETH();
    esp_netif_config.if_key = "OPC_ETH1";
    esp_netif_config.if_desc = "OpenCores Ethernet";
    esp_netif_config.route_prio = 64;
    esp_netif_config_t netif_config = {.base = &esp_netif_config, .stack = ESP_NETIF_NETSTACK_DEFAULT_ETH};
    esp_netif_t* netif = esp_netif_new(&netif_config);
    assert(netif);

    ESP_LOGI(LOG_TAG, "Configuring peripherals...");
    eth_mac_config_t mac_config = ETH_MAC_DEFAULT_CONFIG();
    mac_config.rx_task_stack_size = CONFIG_TW_ETHERNET_EMAC_TASK_STACK_SIZE;
    eth_phy_config_t phy_config = ETH_PHY_DEFAULT_CONFIG();
    phy_config.phy_addr = CONFIG_TW_ETH_PHY_ADDR;
    phy_config.reset_gpio_num = CONFIG_TW_ETH_PHY_RST_GPIO;
    phy_config.autonego_timeout_ms = 100;
    esp_eth_mac_t* mac = esp_eth_mac_new_openeth(&mac_config);
    esp_eth_phy_t* phy = esp_eth_phy_new_dp83848(&phy_config);

    ESP_LOGI(LOG_TAG, "Installing Ethernet driver...");
    esp_eth_config_t config = ETH_DEFAULT_CONFIG(mac, phy);
    esp_eth_handle_t handle;
    ESP_ERROR_CHECK(esp_eth_driver_install(&config, &handle));
    assert(handle);

    esp_eth_netif_glue_handle_t eth_glue = esp_eth_new_netif_glue(handle);
    assert(eth_glue);
    ESP_ERROR_CHECK(esp_netif_attach(netif, eth_glue));

    struct ethernet_interface result = {.interface = netif, .handle = handle};
    return result;
}
