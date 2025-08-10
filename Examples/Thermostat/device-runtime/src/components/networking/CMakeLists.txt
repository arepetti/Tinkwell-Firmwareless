set(srcs "networking.c" "ethernet/ethernet.c")

if(CONFIG_TW_USE_SPI_ETHERNET)
    list(APPEND srcs "ethernet/ethernet_init_spi.c")
endif()

if(CONFIG_TW_USE_INTERNAL_ETHERNET)
    list(APPEND srcs "ethernet/ethernet_init_internal.c")
endif()

if(CONFIG_TW_USE_OPENETH)
    list(APPEND srcs "ethernet/ethernet_init_openeth.c")
endif()

message(STATUS "CONFIG_TW_USE_INTERNAL_ETHERNET = ${CONFIG_TW_USE_INTERNAL_ETHERNET}")
message(STATUS "CONFIG_TW_USE_SPI_ETHERNET = ${CONFIG_TW_USE_SPI_ETHERNET}")
message(STATUS "CONFIG_TW_USE_OPENETH = ${CONFIG_TW_USE_OPENETH}")
message(STATUS "Networking source files: ${srcs}")

idf_component_register(
    SRCS
        ${srcs}
    INCLUDE_DIRS
        "include"
    PRIV_REQUIRES
        esp_netif
        esp_driver_gpio
        esp_eth
        esp_timer
        settings
)
