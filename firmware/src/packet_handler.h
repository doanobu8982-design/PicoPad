#ifndef _PACKET_HANDLER_H_
#define _PACKET_HANDLER_H_

#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct __attribute__((packed)) {
    uint8_t  report_id;    // 0x00
    uint8_t  report_len;   // 0x14 (20 bytes)
    uint16_t buttons;      // XInput button bitmask (Little Endian)
    uint8_t  lt;           // 0..255
    uint8_t  rt;           // 0..255
    int16_t  lx;           // -32768..32767
    int16_t  ly;
    int16_t  rx;
    int16_t  ry;
    uint8_t  reserved[6];  // 0x00
} xinput_report_t;

uint8_t crc8_smbus(const uint8_t *data, size_t len);
void packet_handler_init(void);
void handle_received_packet(const uint8_t *rx_buf);
uint32_t get_last_packet_time_us(void);

#ifdef __cplusplus
}
#endif

#endif // _PACKET_HANDLER_H_

