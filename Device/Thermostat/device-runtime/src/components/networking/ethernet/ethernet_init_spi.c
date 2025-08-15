#include "driver/gpio.h"
#include "driver/spi_master.h"
#include "esp_check.h"
#include "esp_log.h"
#include "esp_mac.h"
#include "ethernet_init.h"
#include <assert.h>
#include <stdlib.h>

static const char* LOG_TAG = "net_eth";

#define INIT_SPI_ETH_MODULE_CONFIG(eth_module_config, num)                                                             \
    do {                                                                                                               \
        eth_module_config[num].spi_cs_gpio = CONFIG_TW_ETH_SPI_CS##num##_GPIO;                                         \
        eth_module_config[num].int_gpio = CONFIG_TW_ETH_SPI_INT##num##_GPIO;                                           \
        eth_module_config[num].polling_ms = CONFIG_TW_ETH_SPI_POLLING##num##_MS;                                       \
        eth_module_config[num].phy_reset_gpio = CONFIG_TW_ETH_SPI_PHY_RST##num##_GPIO;                                 \
        eth_module_config[num].phy_addr = CONFIG_TW_ETH_SPI_PHY_ADDR##num;                                             \
    } while (0)

typedef struct {
    uint8_t spi_cs_gpio;
    int8_t int_gpio;
    uint32_t polling_ms;
    int8_t phy_reset_gpio;
    uint8_t phy_addr;
    uint8_t* mac_addr;
} spi_eth_module_config_t;

void spi_bus_init(void) {
    ESP_LOGI(LOG_TAG, "Initializing SPI bus...");
#if (CONFIG_TW_ETH_SPI_INT0_GPIO >= 0) || (CONFIG_TW_ETH_SPI_INT1_GPIO > 0)
    // Install GPIO ISR handler to be able to service SPI Eth modules interrupts
    esp_err_t ret = gpio_install_isr_service(0);
    if (ret == ESP_ERR_INVALID_STATE) {
        ESP_LOGW(LOG_TAG, "GPIO ISR handler has been already installed");
    } else if (ret != ESP_OK) {
        ESP_ERROR_CHECK(ret);
    }
#endif

    spi_bus_config_t buscfg = {
        .miso_io_num = CONFIG_TW_ETH_SPI_MISO_GPIO,
        .mosi_io_num = CONFIG_TW_ETH_SPI_MOSI_GPIO,
        .sclk_io_num = CONFIG_TW_ETH_SPI_SCLK_GPIO,
        .quadwp_io_num = -1,
        .quadhd_io_num = -1,
    };
    ESP_ERROR_CHECK(spi_bus_initialize(CONFIG_TW_ETH_SPI_HOST, &buscfg, SPI_DMA_CH_AUTO));
}

void nic_init(spi_eth_module_config_t* spi_eth_module_config, struct ethernet_interface* interfaces[], size_t nic_index,
              int spi_index) {
    char* key = malloc(sizeof(char) * 10);
    sprintf(key, "SPI_ETH%d", spi_index);

    ESP_LOGI(LOG_TAG, "Creating interface %s...", key);
    esp_netif_inherent_config_t esp_netif_config = ESP_NETIF_INHERENT_DEFAULT_ETH();
    esp_netif_config.if_key = key;
    esp_netif_config.if_desc = key;
    esp_netif_config.route_prio -= nic_index * 5;
    esp_netif_config_t cfg = {.base = &esp_netif_config, .stack = ESP_NETIF_NETSTACK_DEFAULT_ETH};
    esp_netif_t* netif = esp_netif_new(&cfg);
    assert(netif);

    ESP_LOGI(LOG_TAG, "Configuring peripheral...");
    eth_mac_config_t mac_config = ETH_MAC_DEFAULT_CONFIG();
    eth_phy_config_t phy_config = ETH_PHY_DEFAULT_CONFIG();
    phy_config.phy_addr = spi_eth_module_config->phy_addr;
    phy_config.reset_gpio_num = spi_eth_module_config->phy_reset_gpio;
    spi_device_interface_config_t spi_devcfg = {.mode = 0,
                                                .clock_speed_hz = CONFIG_TW_ETH_SPI_CLOCK_MHZ * 1000 * 1000,
                                                .queue_size = 20,
                                                .spics_io_num = spi_eth_module_config->spi_cs_gpio};

#if CONFIG_TW_USE_KSZ8851SNL
    eth_ksz8851snl_config_t ksz8851snl_config = ETH_KSZ8851SNL_DEFAULT_CONFIG(CONFIG_TW_ETH_SPI_HOST, &spi_devcfg);
    ksz8851snl_config.int_gpio_num = spi_eth_module_config->int_gpio;
    ksz8851snl_config.poll_period_ms = spi_eth_module_config->polling_ms;
    esp_eth_mac_t* mac = esp_eth_mac_new_ksz8851snl(&ksz8851snl_config, &mac_config);
    esp_eth_phy_t* phy = esp_eth_phy_new_ksz8851snl(&phy_config);
#elif CONFIG_TW_USE_DM9051
    eth_dm9051_config_t dm9051_config = ETH_DM9051_DEFAULT_CONFIG(CONFIG_TW_ETH_SPI_HOST, &spi_devcfg);
    dm9051_config.int_gpio_num = spi_eth_module_config->int_gpio;
    dm9051_config.poll_period_ms = spi_eth_module_config->polling_ms;
    esp_eth_mac_t* mac = esp_eth_mac_new_dm9051(&dm9051_config, &mac_config);
    esp_eth_phy_t* phy = esp_eth_phy_new_dm9051(&phy_config);
#elif CONFIG_TW_USE_W5500
    eth_w5500_config_t w5500_config = ETH_W5500_DEFAULT_CONFIG(CONFIG_TW_ETH_SPI_HOST, &spi_devcfg);
    w5500_config.int_gpio_num = spi_eth_module_config->int_gpio;
    w5500_config.poll_period_ms = spi_eth_module_config->polling_ms;
    esp_eth_mac_t* mac = esp_eth_mac_new_w5500(&w5500_config, &mac_config);
    esp_eth_phy_t* phy = esp_eth_phy_new_w5500(&phy_config);
#endif
    ESP_LOGI(LOG_TAG, "Installing Ethernet driver...");
    esp_eth_handle_t eth_handle = NULL;
    esp_eth_config_t eth_config_spi = ETH_DEFAULT_CONFIG(mac, phy);
    ESP_ERROR_CHECK(esp_eth_driver_install(&eth_config_spi, &eth_handle));
    assert(eth_handle);

    // The SPI Ethernet module might not have a burned factory MAC address, we can set it manually.
    if (spi_eth_module_config->mac_addr != NULL) {
        ESP_ERROR_CHECK(esp_eth_ioctl(eth_handle, ETH_CMD_S_MAC_ADDR, spi_eth_module_config->mac_addr));
    }

    esp_eth_netif_glue_handle_t eth_glue = esp_eth_new_netif_glue(eth_handle);
    assert(eth_glue);
    ESP_ERROR_CHECK(esp_netif_attach(eth_netif, eth_netif_glue));

    struct ethernet_interface interface = {.interface = eth_netif, .handle = eth_handle};
    *interfaces[nic_index] = interface;
}

void ethernet_init_spi(esp_netif_t* netifs[], size_t start_index) {
    spi_bus_init();

    ESP_LOGI(LOG_TAG, "Preparing peripherals configuration...");
    spi_eth_module_config_t spi_eth_module_config[CONFIG_TW_SPI_ETHERNETS_NUM] = {0};
    INIT_SPI_ETH_MODULE_CONFIG(spi_eth_module_config, 0);
    // The SPI Ethernet module(s) might not have a burned factory MAC address,
    // hence use manually configured address(es). In this example, Locally
    // Administered MAC address derived from ESP32x base MAC address is used.
    // Note that Locally Administered OUI range should be used only when testing
    // on a LAN under your control!
    uint8_t base_mac_addr[ETH_ADDR_LEN];
    ESP_ERROR_CHECK(esp_efuse_mac_get_default(base_mac_addr));
    uint8_t local_mac_1[ETH_ADDR_LEN];
    esp_derive_local_mac(local_mac_1, base_mac_addr);
    spi_eth_module_config[0].mac_addr = local_mac_1;

#if CONFIG_TW_SPI_ETHERNETS_NUM > 1
    INIT_SPI_ETH_MODULE_CONFIG(spi_eth_module_config, 1);
    uint8_t local_mac_2[ETH_ADDR_LEN];
    base_mac_addr[ETH_ADDR_LEN - 1] += 1;
    esp_derive_local_mac(local_mac_2, base_mac_addr);
    spi_eth_module_config[1].mac_addr = local_mac_2;
#endif

    for (int i = 0; i < CONFIG_TW_SPI_ETHERNETS_NUM; i++) {
        nic_init(&spi_eth_module_config[i], netifs, start_index + i, i + 1);
    }
}
