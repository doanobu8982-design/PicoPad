#ifndef _USB_DESCRIPTORS_H_
#define _USB_DESCRIPTORS_H_

#include <stdint.h>
#include <stdbool.h>
#include "tusb.h"

#ifdef __cplusplus
extern "C" {
#endif

#define MODE_A 0x0A
#define MODE_B 0x0B

// USB Vendor & Product IDs
#define USB_VID_RPI   0x2E8A
#define USB_PID_MODE_A 0x000A
#define USB_PID_MODE_B 0x000B

// Microsoft OS 1.0 Vendor Request definition
#define VENDOR_REQUEST_MS_OS_10 0x01

uint8_t get_current_operating_mode(void);
void reboot_to_mode(uint8_t target_mode);

#ifdef __cplusplus
}
#endif

#endif // _USB_DESCRIPTORS_H_

