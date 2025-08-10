#include "driver/gpio.h"
#include "esp_check.h"
#include "esp_log.h"
#include "esp_mac.h"
#include "ethernet_init.h"
#include <assert.h>

static const char* LOG_TAG = "net_eth";

struct ethernet_interface ethernet_init_internal(void) {
    ESP_LOGI(LOG_TAG, "Creating interface INT_ETH1...");
    esp_netif_inherent_config_t esp_netif_config = ESP_NETIF_INHERENT_DEFAULT_ETH();
    esp_netif_config.if_key = "INT_ETH1";
    esp_netif_config.if_desc = "INT_ETH1";
    esp_netif_config_t cfg = {
        .base = &esp_netif_config,
        .stack = ESP_NETIF_NETSTACK_DEFAULT_ETH
    };
    esp_netif_t* netif = esp_netif_new(&cfg);
    assert(netif);

    ESP_LOGI(LOG_TAG, "Configuring peripherals...");
    eth_mac_config_t mac_config = ETH_MAC_DEFAULT_CONFIG();
    eth_phy_config_t phy_config = ETH_PHY_DEFAULT_CONFIG();

    phy_config.phy_addr = CONFIG_TW_ETH_PHY_ADDR;
    phy_config.reset_gpio_num = CONFIG_TW_ETH_PHY_RST_GPIO;
    eth_esp32_emac_config_t esp32_emac_config = ETH_ESP32_EMAC_DEFAULT_CONFIG();
    esp32_emac_config.smi_gpio.mdc_num = CONFIG_TW_ETH_MDC_GPIO;
    esp32_emac_config.smi_gpio.mdio_num = CONFIG_TW_ETH_MDIO_GPIO;
#if CONFIG_TW_USE_SPI_ETHERNET
    // The DMA is shared resource between EMAC and the SPI. Therefore, adjust
    // EMAC DMA burst length when SPI Ethernet is used along with EMAC.
    esp32_emac_config.dma_burst_len = ETH_DMA_BURST_LEN_4;
#endif // CONFIG_TW_USE_SPI_ETHERNET
    esp_eth_mac_t* mac = esp_eth_mac_new_esp32(&esp32_emac_config, &mac_config);
#if CONFIG_TW_ETH_PHY_GENERIC
    esp_eth_phy_t* phy = esp_eth_phy_new_generic(&phy_config);
#elif CONFIG_TW_ETH_PHY_IP101
    esp_eth_phy_t* phy = esp_eth_phy_new_ip101(&phy_config);
#elif CONFIG_TW_ETH_PHY_RTL8201
    esp_eth_phy_t* phy = esp_eth_phy_new_rtl8201(&phy_config);
#elif CONFIG_TW_ETH_PHY_LAN87XX
    esp_eth_phy_t* phy = esp_eth_phy_new_lan87xx(&phy_config);
#elif CONFIG_TW_ETH_PHY_DP83848
    esp_eth_phy_t* phy = esp_eth_phy_new_dp83848(&phy_config);
#elif CONFIG_TW_ETH_PHY_KSZ80XX
    esp_eth_phy_t* phy = esp_eth_phy_new_ksz80xx(&phy_config);
#endif
    ESP_LOGI(LOG_TAG, "Installing Ethernet driver...");
    esp_eth_handle_t eth_handle = NULL;
    esp_eth_config_t config = ETH_DEFAULT_CONFIG(mac, phy);
    ESP_ERROR_CHECK(esp_eth_driver_install(&config, &eth_handle));
    assert(eth_handle);

    esp_eth_netif_glue_handle_t eth_glue = esp_eth_new_netif_glue(eth_handle);
    assert(eth_glue);
    ESP_ERROR_CHECK(esp_netif_attach(eth_netif, eth_netif_glue));

    struct ethernet_interface result = { .interface = eth_netif, .handle = eth_handle };
    return result;
}