#ifndef LEGACY_STARTUP_GATE_H
#define LEGACY_STARTUP_GATE_H
/* Decoder readiness and visible presentation can arrive in either order.
   Two display-link callbacks allow a complete presentation opportunity after
   attaching the opaque intro surface; no media may advance before both. */
typedef struct {
    int presented_frames, playable, foreground, requested, cancelled;
} LegacyStartupGate;
static int legacy_startup_gate_take(LegacyStartupGate *gate) {
    if(gate->cancelled || gate->requested || !gate->foreground || !gate->playable || gate->presented_frames<2)return 0;
    gate->requested=1;return 1;
}
#endif
