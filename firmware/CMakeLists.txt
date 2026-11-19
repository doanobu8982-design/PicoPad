cmake_minimum_required(VERSION 3.13)

# Include Pico SDK import helper
include(pico_sdk_import.cmake)

project(gamepad_firmware C CXX ASM)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)

if (COMMAND pico_sdk_init)
    pico_sdk_init()

    add_executable(gamepad_firmware
        src/main.c
        src/usb_descriptors.c
        src/xinput_driver.c
        src/packet_handler.c
    )

    target_include_directories(gamepad_firmware PRIVATE
        ${CMAKE_CURRENT_LIST_DIR}
        ${CMAKE_CURRENT_LIST_DIR}/src
    )

    target_link_libraries(gamepad_firmware
        pico_stdlib
        hardware_watchdog
        tinyusb_device
        tinyusb_board
    )

    # Low-latency execution entirely in SRAM
    pico_set_binary_type(gamepad_firmware copy_to_ram)

    # Configure stdio
    pico_enable_stdio_usb(gamepad_firmware 1)
    pico_enable_stdio_uart(gamepad_firmware 0)

    pico_add_extra_outputs(gamepad_firmware)
else ()
    message(WARNING "Pico SDK is not available. Define PICO_SDK_PATH to build the firmware target.")
    add_custom_target(gamepad_firmware
        COMMAND ${CMAKE_COMMAND} -E echo "Pico SDK not configured. Set PICO_SDK_PATH."
    )
endif ()
