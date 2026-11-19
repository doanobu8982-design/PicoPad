#ifndef _XINPUT_DRIVER_H_
#define _XINPUT_DRIVER_H_

#include <stdint.h>
#include <stdbool.h>
#include "tusb.h"

#ifdef __cplusplus
extern "C" {
#endif

#define XINPUT_EP_IN  0x81
#define XINPUT_EP_OUT 0x02

void xinput_driver_init(void);
void xinput_send_report(const uint8_t *report, uint16_t len);

#ifdef __cplusplus
}
#endif

#endif // _XINPUT_DRIVER_H_

