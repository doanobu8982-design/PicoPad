#include "bsp/board.h"
#include "hardware/watchdog.h"
#include "packet_handler.h"
#include "pico/stdlib.h"
#include "tusb.h"
#include "usb_descriptors.h"
#include "xinput_driver.h"
#include <stdio.h>

// Multilayer safety net states
static volatile bool game_mode_active = false;
static volatile bool dtr_established = false;
static volatile uint32_t dtr_low_start_time = 0;

// Controlled reboot with USB disconnect & Scratch register preservation
void reboot_to_mode(uint8_t target_mode) {
  sleep_ms(5);      // Allow pending USB packets to complete
  tud_disconnect(); // Disconnect D+ pull-up to notify host instantly
  sleep_ms(100);    // USB 2.0 host debounce requirement
  watchdog_hw->scratch[0] = target_mode;
  watchdog_reboot(0, 0, 10); // Trigger watchdog reset in 10ms
  while (1) {
    tight_loop_contents();
  }
}

static void init_game_mode(void) {
  game_mode_active = true;
  dtr_established = false;
  dtr_low_start_time = 0;
}

// CDC Line state callback: tracks DTR signal from PC application
void tud_cdc_line_state_cb(uint8_t itf, bool dtr, bool rts) {
  (void)itf;
  (void)rts;
  if (game_mode_active) {
    if (dtr) {
      dtr_established = true; // PC application opened port and asserted DTR
      dtr_low_start_time = 0; // Clear falling edge timer
    } else if (dtr_established) {
      // DTR falling edge detected: start 50ms debounce timer to prevent false
      // positives
      if (dtr_low_start_time == 0) {
        dtr_low_start_time = time_us_32();
      }
    }
  }
}

// Safety watchdog check: reverts to Mode A on application disconnect or 30s
// communication timeout
static void check_safety_watchdog(void) {
  if (!game_mode_active)
    return;

  uint32_t now = time_us_32();

  // 1. DTR low debounce check (50ms continuous low -> host closed port)
  if (dtr_established && dtr_low_start_time != 0) {
    if (now - dtr_low_start_time > 50000) {
      reboot_to_mode(MODE_A);
    }
  }

  // 2. Auxiliary watchdog: 30 seconds communication silence -> revert to Mode A
  if (now - get_last_packet_time_us() > 30000000) {
    reboot_to_mode(MODE_A);
  }
}

int main(void) {
  // Determine operating mode from watchdog Scratch register
  uint8_t mode = get_current_operating_mode();
  if (mode == MODE_B) {
    init_game_mode();
  }

  board_init();
  tusb_init();
  packet_handler_init();

  while (1) {
    tud_task();

    // Process CDC incoming stream with byte realignment protection
    while (tud_cdc_available() >= 6) {
      uint8_t peek_byte;
      tud_cdc_peek(&peek_byte);

      // If first byte is not a known header (0x5A or 0xA5), discard 1 byte to
      // realign
      if (peek_byte != 0x5A && peek_byte != 0xA5) {
        uint8_t dummy;
        tud_cdc_read(&dummy, 1);
        continue;
      }

      uint8_t rx_buf[6];
      uint32_t count = tud_cdc_read(rx_buf, 6);
      if (count == 6) {
        handle_received_packet(rx_buf);
      }
    }

    check_safety_watchdog();
  }

  return 0;
}
