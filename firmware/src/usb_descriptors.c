#include <string.h>
#include "usb_descriptors.h"
#include "pico/unique_id.h"
#include "hardware/watchdog.h"

//--------------------------------------------------------------------+
// Mode Detection
//--------------------------------------------------------------------+
uint8_t get_current_operating_mode(void) {
    uint32_t scratch = watchdog_hw->scratch[0];
    return (scratch == MODE_B) ? MODE_B : MODE_A;
}

//--------------------------------------------------------------------+
// Device Descriptors
//--------------------------------------------------------------------+
static const tusb_desc_device_t desc_device_mode_a = {
    .bLength            = sizeof(tusb_desc_device_t),
    .bDescriptorType    = TUSB_DESC_DEVICE,
    .bcdUSB             = 0x0200,
    .bDeviceClass       = TUSB_CLASS_MISC,
    .bDeviceSubClass    = MISC_SUBCLASS_COMMON,
    .bDeviceProtocol    = MISC_PROTOCOL_IAD,
    .bMaxPacketSize0    = CFG_TUD_ENDPOINT0_SIZE,

    .idVendor           = USB_VID_RPI,
    .idProduct          = USB_PID_MODE_A,
    .bcdDevice          = 0x0100,

    .iManufacturer      = 0x01,
    .iProduct           = 0x02,
    .iSerialNumber      = 0x03,

    .bNumConfigurations = 0x01
};

static const tusb_desc_device_t desc_device_mode_b = {
    .bLength            = sizeof(tusb_desc_device_t),
    .bDescriptorType    = TUSB_DESC_DEVICE,
    .bcdUSB             = 0x0200,
    .bDeviceClass       = TUSB_CLASS_MISC,
    .bDeviceSubClass    = MISC_SUBCLASS_COMMON,
    .bDeviceProtocol    = MISC_PROTOCOL_IAD,
    .bMaxPacketSize0    = CFG_TUD_ENDPOINT0_SIZE,

    .idVendor           = USB_VID_RPI,
    .idProduct          = USB_PID_MODE_B,
    .bcdDevice          = 0x0100,

    .iManufacturer      = 0x01,
    .iProduct           = 0x02,
    .iSerialNumber      = 0x03,

    .bNumConfigurations = 0x01
};

uint8_t const *tud_descriptor_device_cb(void) {
    return (get_current_operating_mode() == MODE_B) ?
           (uint8_t const *)&desc_device_mode_b :
           (uint8_t const *)&desc_device_mode_a;
}

//--------------------------------------------------------------------+
// Configuration Descriptors
//--------------------------------------------------------------------+

// Mode A: CDC ACM only (75 bytes)
#define CONFIG_MODE_A_TOTAL_LEN (TUD_CONFIG_DESC_LEN + TUD_CDC_DESC_LEN)

static const uint8_t desc_configuration_mode_a[] = {
    // Config: 9 bytes
    TUD_CONFIG_DESCRIPTOR(1, 2, 0, CONFIG_MODE_A_TOTAL_LEN, 0x80, 250),

    // CDC: Interface 0 & 1, EP 0x81 (Notif 8B), EP 0x02 (Out 64B), EP 0x82 (In 64B)
    TUD_CDC_DESCRIPTOR(0, 4, 0x81, 8, 0x02, 0x82, 64)
};

// Mode B: XInput (IF0) + CDC ACM (IF1 & IF2) (114 bytes)
#define XINPUT_DESC_LEN (9 + 16 + 7 + 7) // Interface (9) + Vendor (16) + EP In (7) + EP Out (7) = 39
#define CONFIG_MODE_B_TOTAL_LEN (TUD_CONFIG_DESC_LEN + XINPUT_DESC_LEN + TUD_CDC_DESC_LEN)

static const uint8_t desc_configuration_mode_b[] = {
    // Config: 9 bytes (3 interfaces)
    TUD_CONFIG_DESCRIPTOR(1, 3, 0, CONFIG_MODE_B_TOTAL_LEN, 0x80, 500),

    // Interface 0: XInput (39 bytes total)
    // 1. Interface descriptor
    0x09, TUSB_DESC_INTERFACE, 0x00, 0x00, 0x02, 0xFF, 0x5D, 0x01, 0x00,
    // 2. XInput Vendor descriptor (16 bytes)
    0x10, 0x21, 0x10, 0x01, 0x01, 0x24, 0x81, 0x14, 0x03, 0x00, 0x03, 0x13, 0x02, 0x00, 0x03, 0x00,
    // 3. EP In: 0x81, Interrupt, 32 bytes, 1ms polling
    0x07, TUSB_DESC_ENDPOINT, 0x81, TUSB_XFER_INTERRUPT, 0x20, 0x00, 0x01,
    // 4. EP Out: 0x02, Interrupt, 32 bytes, 8ms polling
    0x07, TUSB_DESC_ENDPOINT, 0x02, TUSB_XFER_INTERRUPT, 0x20, 0x00, 0x08,

    // Interface 1 & 2: CDC ACM with IAD (bFirstInterface = 1)
    // Notification: EP 0x83, Out: EP 0x04, In: EP 0x84
    TUD_CDC_DESCRIPTOR(1, 4, 0x83, 8, 0x04, 0x84, 64)
};

uint8_t const *tud_descriptor_configuration_cb(uint8_t index) {
    (void)index;
    return (get_current_operating_mode() == MODE_B) ?
           desc_configuration_mode_b :
           desc_configuration_mode_a;
}

//--------------------------------------------------------------------+
// Microsoft OS 1.0 Descriptors (Mode B)
//--------------------------------------------------------------------+
#define MS_OS_10_STRING_INDEX 0xEE

// Microsoft OS String Descriptor (18 bytes)
static const uint8_t ms_os_10_string_desc[] = {
    0x12,                           // bLength = 18 bytes
    TUSB_DESC_STRING,               // bDescriptorType = STRING
    'M', 0, 'S', 0, 'F', 0, 'T', 0, // "MSFT100" in UTF-16LE
    '1', 0, '0', 0, '0', 0,
    VENDOR_REQUEST_MS_OS_10,        // bVendorCode = 0x01
    0x00                            // bPad = 0x00
};

// Extended Compatible ID Descriptor (40 bytes)
static const uint8_t ms_os_compat_id_desc[] = {
    // Header (16 bytes)
    0x28, 0x00, 0x00, 0x00,         // dwLength = 40
    0x00, 0x01,                     // bcdVersion = 1.00
    0x04, 0x00,                     // wIndex = 0x0004
    0x01,                           // bCount = 1 section
    0x00, 0x00, 0x00, 0x00,         // Reserved (7 bytes)
    0x00, 0x00, 0x00,

    // Section 1: Interface 0 -> "XUSB10"
    0x00,                           // bFirstInterfaceNumber = 0 (XInput)
    0x01,                           // Reserved (must be 1)
    'X', 'U', 'S', 'B', '1', '0', 0x00, 0x00, // CompatibleID (8 bytes)
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // SubCompatibleID (8 bytes)
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00  // Reserved (6 bytes)
};

// Handle Microsoft OS 1.0 vendor requests
bool tud_vendor_control_xfer_cb(uint8_t rhport, uint8_t stage, tusb_control_request_t const *request) {
    if (stage != CONTROL_STAGE_SETUP) return true;

    if (request->bRequest == VENDOR_REQUEST_MS_OS_10 && request->wIndex == 0x0004) {
        // Return Extended Compatible ID Descriptor
        return tud_control_xfer(rhport, request, (void *)(uintptr_t)ms_os_compat_id_desc, sizeof(ms_os_compat_id_desc));
    }

    return false;
}

//--------------------------------------------------------------------+
// String Descriptors
//--------------------------------------------------------------------+
static char const *string_desc_arr[] = {
    (const char[]){ 0x09, 0x04 },    // 0: Supported language = English (0x0409)
    "Raspberry Pi",                  // 1: Manufacturer
    "Pico Gamepad Emulator",         // 2: Product
    NULL,                            // 3: Serial Number (dynamically populated)
    "Pico CDC Serial"                // 4: CDC Interface
};

static uint16_t _desc_str[33];

uint16_t const *tud_descriptor_string_cb(uint8_t index, uint16_t langid) {
    (void)langid;
    uint8_t chr_count = 0;

    if (index == 0) {
        memcpy(&_desc_str[1], string_desc_arr[0], 2);
        chr_count = 1;
    } else if (index == MS_OS_10_STRING_INDEX && get_current_operating_mode() == MODE_B) {
        // Respond to MS OS String index 0xEE in Mode B
        return (uint16_t const *)ms_os_10_string_desc;
    } else if (index == 3) {
        // Unique board serial number
        char board_id_str[2 * PICO_UNIQUE_BOARD_ID_SIZE_BYTES + 1];
        pico_get_unique_board_id_string(board_id_str, sizeof(board_id_str));

        chr_count = (uint8_t)strlen(board_id_str);
        if (chr_count > 31) chr_count = 31;

        for (uint8_t i = 0; i < chr_count; i++) {
            _desc_str[1 + i] = (uint16_t)board_id_str[i];
        }
    } else {
        if (index >= sizeof(string_desc_arr) / sizeof(string_desc_arr[0])) {
            return NULL;
        }

        const char *str = string_desc_arr[index];
        if (!str) return NULL;

        chr_count = (uint8_t)strlen(str);
        if (chr_count > 31) chr_count = 31;

        for (uint8_t i = 0; i < chr_count; i++) {
            _desc_str[1 + i] = (uint16_t)str[i];
        }
    }

    // First byte is length (including header), second byte is string type (0x03)
    _desc_str[0] = (uint16_t)((TUSB_DESC_STRING << 8) | (2 * chr_count + 2));
    return _desc_str;
}

