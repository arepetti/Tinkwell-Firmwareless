#include "include/networking.h"
#include "esp_eth.h"
#include "esp_event.h"
#include "esp_log.h"
#include "esp_netif.h"
#include "esp_timer.h"
#include "ethernet/ethernet.h"
#include "lwip/ip_addr.h"
#include "settings.h"
#include <assert.h>
#include <stdbool.h>

#define GOT_IP_BIT BIT0
#define DEFAULT_CONNECTION_TIMEOUT 5000

static const char* LOG_TAG = "net";
static const struct network_setup_t* g_setup;
static bool g_setup_completed;
static EventGroupHandle_t s_network_event_group;

static const char* network_nic_to_key(enum network_nic nic) {
    switch (nic) {
    case NETWORK_NIC_OPEN_CORES:
        return "OPC_ETH1";
    case NETWORK_NIC_INTERNAL:
        return "INT_ETH1";
    case NETWORK_NIC_SPI1:
        return "SPI_ETH1";
    case NETWORK_NIC_SPI2:
        return "SPI_ETH2";
    case NETWORK_NIC_DEFAULT:
        break;
    default:
        assert(false);
    }

    // Default means that we decide: CONFIG_TW_USE_OPENETH has precedence over any other
    // connection (ideally it should be the only one).
    // If CONFIG_TW_USE_INTERNAL_ETHERNET then it has priority (SPI connections are...for support)
    // and if nothing else is available then CONFIG_TW_USE_SPI_ETHERNET. Note that while we support
    // multiple cards (internal and 2 SPIs), in reality we should always have only one (preferibly the internal one
    // or OpenCores when running on QEMU).

#if CONFIG_TW_USE_OPENETH
    return "OPC_ETH1";
#endif

#if CONFIG_TW_USE_INTERNAL_ETHERNET
    return "INT_ETH1";
#endif

#if CONFIG_TW_USE_SPI_ETHERNET
    return "SPI_ETH1";
#endif

    return NULL;
}

static bool ethernet_set_static_ip(esp_netif_t* netif) {
    ESP_LOGI(LOG_TAG, "Stopping DHCP client...");
    if (esp_netif_dhcpc_stop(netif) != ESP_OK) {
        ESP_LOGE(LOG_TAG, "Failed to stop DHCP client");
        return false;
    }

    ESP_LOGI(LOG_TAG, "Setting up static IP address...");
    esp_netif_ip_info_t ip;
    memset(&ip, 0, sizeof(esp_netif_ip_info_t));
    ip.ip.addr = ipaddr_addr(g_setup->static_ip_address);
    ip.netmask.addr = ipaddr_addr(g_setup->static_ip_netmask);
    ip.gw.addr = ipaddr_addr(g_setup->static_ip_gw_address);
    if (esp_netif_set_ip_info(netif, &ip) != ESP_OK) {
        ESP_LOGE(LOG_TAG, "Failed to set IP info");
        return false;
    }

    ESP_LOGD(LOG_TAG, "Success to set static IP: %s (%s)", g_setup->static_ip_address, g_setup->static_ip_netmask);
    ESP_LOGD(LOG_TAG, "Success to set gateway: %s", g_setup->static_ip_gw_address);

    return true;
}

static void ethernet_event_handler(void* arg, esp_event_base_t event_base, int32_t event_id, void* event_data) {
    if (event_base == ETH_EVENT && event_id == ETHERNET_EVENT_CONNECTED) {
        ESP_LOGI(LOG_TAG, "Interface %s connected", esp_netif_get_ifkey((esp_netif_t*)arg));
        if (!g_setup_completed) {
            g_setup_completed = g_setup->use_dhcp || ethernet_set_static_ip(arg);
        }
    } else if (event_base == ETH_EVENT && event_id == ETHERNET_EVENT_DISCONNECTED) {
        ESP_LOGI(LOG_TAG, "Interface %s disconnected", esp_netif_get_ifkey((esp_netif_t*)arg));
    } else if (event_base == IP_EVENT && event_id == IP_EVENT_ETH_GOT_IP) {
        ip_event_got_ip_t* event = (ip_event_got_ip_t*)event_data;
        ESP_LOGI(LOG_TAG, "IP for %s is:" IPSTR, esp_netif_get_ifkey((esp_netif_t*)arg), IP2STR(&event->ip_info.ip));
        xEventGroupSetBits(s_network_event_group, GOT_IP_BIT);
    }
}

esp_err_t networking_initialize(void) {
    s_network_event_group = xEventGroupCreate();

    ESP_LOGI(LOG_TAG, "Initializing TCP/IP stack...");
    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());

    return ESP_OK;
}

esp_err_t networking_setup(const struct network_setup_t* setup) {
    g_setup = setup;

    struct ethernet_interface* interfaces = NULL;
    size_t interface_count;
    if (setup->connection == NETWORK_CONNECTION_ETHERNET) {
        ESP_LOGI(LOG_TAG, "Initializing Ethernet interfaces...");
        ESP_ERROR_CHECK(ethernet_init(&interfaces, &interface_count));
    } else {
        return ESP_ERR_NOT_SUPPORTED;
    }

    esp_netif_t* primary_nic = NULL;
    const char* primary_nic_key = network_nic_to_key(setup->primary_nic);
    esp_event_handler_instance_t instance_any_id;
    esp_event_handler_instance_t instance_got_ip;
    for (size_t i = 0; i < interface_count; ++i) {
        const char* interface_key = esp_netif_get_ifkey(interfaces[i].interface);
        if (strcmp(primary_nic_key, interface_key) == 0) {
            primary_nic = interfaces[i].interface;
            ESP_ERROR_CHECK(esp_event_handler_instance_register(ETH_EVENT, ESP_EVENT_ANY_ID, &ethernet_event_handler,
                                                                primary_nic, &instance_any_id));
            assert(instance_any_id);
            ESP_ERROR_CHECK(esp_event_handler_instance_register(IP_EVENT, IP_EVENT_ETH_GOT_IP, &ethernet_event_handler,
                                                                primary_nic, &instance_got_ip));
            assert(instance_got_ip);
        }

        ESP_LOGI(LOG_TAG, "Starting %s...", interface_key);
        ESP_ERROR_CHECK(esp_eth_start(interfaces[i].handle));
    }

    // Wait for the connection to be estabilished and an IP assigned
    int timeout = setup->use_dhcp && setup->dhcp_timeout > 0 ? setup->dhcp_timeout : DEFAULT_CONNECTION_TIMEOUT;
    EventBits_t bits = xEventGroupWaitBits(s_network_event_group, GOT_IP_BIT, pdFALSE, pdFALSE, pdMS_TO_TICKS(timeout));

    // If we have not been assigned an IP then it's an error but, if we were waiting for DHCP then we can
    // fallback to a manual static IP configuration.
    if (!(bits & GOT_IP_BIT)) {
        ESP_LOGE(LOG_TAG, "Ethernet IP address not set within  %d ms", timeout);
        if (setup->use_dhcp && primary_nic != NULL) {
            ESP_LOGI(LOG_TAG, "Falling back to static IP");
            if (ethernet_set_static_ip(primary_nic)) g_setup_completed = true;
        }
    }

    // We do not need this after the setup phase but we keep general events to log connected/disconnected events.
    if (instance_got_ip != NULL)
        ESP_ERROR_CHECK(esp_event_handler_instance_unregister(IP_EVENT, IP_EVENT_ETH_GOT_IP, instance_got_ip));

    vEventGroupDelete(s_network_event_group);

    return ESP_OK;
}