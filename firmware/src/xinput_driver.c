#include <string.h>
#include "xinput_driver.h"
#include "device/usbd_pvt.h"

static volatile bool xinput_tx_pending = false;
static uint8_t xinput_out_buf[32];
static uint8_t pending_report_buf[32];
static uint16_t pending_report_len = 0;
static uint8_t xinput_itf_num = 0;

void xinput_driver_init(void) {
    xinput_tx_pending = false;
    pending_report_len = 0;
    memset(xinput_out_buf, 0, sizeof(xinput_out_buf));
    memset(pending_report_buf, 0, sizeof(pending_report_buf));
}

static void xinput_init(void) {
    xinput_driver_init();
}

static void xinput_reset(uint8_t rhport) {
    (void)rhport;
    xinput_driver_init();
}

static uint16_t xinput_open(uint8_t rhport, tusb_desc_interface_t const *itf_desc, uint16_t max_len) {
    // Check if this interface is XInput (0xFF / 0x5D / 0x01)
    if (itf_desc->bInterfaceClass != 0xFF ||
        itf_desc->bInterfaceSubClass != 0x5D ||
        itf_desc->bInterfaceProtocol != 0x01) {
        return 0;
    }

    // Expected descriptor size: 9 (itf) + 16 (vendor) + 7 (ep in) + 7 (ep out) = 39 bytes
    const uint16_t drv_len = 9 + 16 + 7 + 7;
    if (max_len < drv_len) return 0;

    xinput_itf_num = itf_desc->bInterfaceNumber;

    // Parse past the interface and 16-byte vendor descriptor to reach endpoint descriptors
    uint8_t const *p_desc = tu_desc_next(itf_desc);
    p_desc = (uint8_t const *)p_desc + 16; // Skip 16-byte XInput vendor descriptor

    // Open Endpoint IN (0x81) and Endpoint OUT (0x02)
    for (int i = 0; i < 2; i++) {
        tusb_desc_endpoint_t const *ep_desc = (tusb_desc_endpoint_t const *)p_desc;
        if (!usbd_edpt_open(rhport, ep_desc)) {
            return 0;
        }
        p_desc = tu_desc_next(p_desc);
    }

    // Initial arming of OUT endpoint to receive host rumble/LED packets
    usbd_edpt_xfer(rhport, XINPUT_EP_OUT, xinput_out_buf, sizeof(xinput_out_buf));

    return drv_len;
}

static bool xinput_control_xfer_cb(uint8_t rhport, uint8_t stage, tusb_control_request_t const *request) {
    (void)rhport;
    (void)stage;
    (void)request;
    return false;
}

static bool xinput_xfer_cb(uint8_t rhport, uint8_t ep_addr, xfer_result_t result, uint32_t xferred_bytes) {
    (void)xferred_bytes;
    if (result != XFER_RESULT_SUCCESS) return true;

    if (ep_addr == XINPUT_EP_IN) {
        // IN transfer complete: if a report was queued while busy, send it now
        if (xinput_tx_pending) {
            xinput_tx_pending = false;
            usbd_edpt_xfer(rhport, XINPUT_EP_IN, pending_report_buf, pending_report_len);
        }
    } else if (ep_addr == XINPUT_EP_OUT) {
        // OUT packet received (rumble/LED): dummy read and immediately re-arm endpoint
        usbd_edpt_xfer(rhport, XINPUT_EP_OUT, xinput_out_buf, sizeof(xinput_out_buf));
    }

    return true;
}

void xinput_send_report(const uint8_t *report, uint16_t len) {
    if (len > sizeof(pending_report_buf)) len = sizeof(pending_report_buf);

    if (!usbd_edpt_busy(0, XINPUT_EP_IN)) {
        xinput_tx_pending = false;
        usbd_edpt_xfer(0, XINPUT_EP_IN, (uint8_t *)report, len);
    } else {
        // Busy transmitting previous report: save to pending buffer and mark flag
        memcpy(pending_report_buf, report, len);
        pending_report_len = len;
        xinput_tx_pending = true;
    }
}

static const usbd_class_driver_t xinput_class_driver = {
#if CFG_TUSB_DEBUG >= 2
    .name = "XINPUT",
#endif
    .init             = xinput_init,
    .reset            = xinput_reset,
    .open             = xinput_open,
    .control_xfer_cb  = xinput_control_xfer_cb,
    .xfer_cb          = xinput_xfer_cb,
    .sof              = NULL
};

const usbd_class_driver_t *usbd_app_driver_get_cb(uint8_t *driver_count) {
    *driver_count = 1;
    return &xinput_class_driver;
}

