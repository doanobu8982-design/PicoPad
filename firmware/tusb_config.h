#ifndef _TUSB_CONFIG_H_
#define _TUSB_CONFIG_H_

#ifdef __cplusplus
extern "C" {
#endif

//--------------------------------------------------------------------+
// Common Configuration
//--------------------------------------------------------------------+

#ifndef CFG_TUSB_MCU
#define CFG_TUSB_MCU OPT_MCU_RP2040
#endif

#ifndef CFG_TUSB_OS
#define CFG_TUSB_OS OPT_OS_NONE
#endif

#ifndef CFG_TUSB_RHPORT0_MODE
#define CFG_TUSB_RHPORT0_MODE (OPT_MODE_DEVICE | OPT_MODE_FULL_SPEED)
#endif

#ifndef CFG_TUD_ENABLED
#define CFG_TUD_ENABLED 1
#endif

#define CFG_TUD_MAX_SPEED OPT_MODE_DEFAULT_SPEED
#define CFG_TUD_ENDPOINT0_SIZE 64

//--------------------------------------------------------------------+
// Device Class Configuration
//--------------------------------------------------------------------+

// CDC is enabled for serial communication
#define CFG_TUD_CDC 1
#define CFG_TUD_HID 0
#define CFG_TUD_MSC 0
#define CFG_TUD_MIDI 0
#define CFG_TUD_VENDOR 0

// Custom class driver count: 1 for custom XInput driver
#define CFG_TUD_CUSTOM_CLASS 1

// CDC FIFO size
#define CFG_TUD_CDC_RX_BUFSIZE 128
#define CFG_TUD_CDC_TX_BUFSIZE 128
#define CFG_TUD_CDC_EP_BUFSIZE 64

#ifdef __cplusplus
}
#endif

#endif // _TUSB_CONFIG_H_
