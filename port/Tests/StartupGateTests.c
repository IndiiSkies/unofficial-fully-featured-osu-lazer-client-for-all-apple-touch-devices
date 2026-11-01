#include <assert.h>
#include <stdio.h>
#include "../Native/StartupGate.h"
int main(void) {
    /* Exercise both orders of the asynchronous decoder/presentation events,
       including focus loss between any two of them. */
    int checks=0;
    for(int readyAt=0;readyAt<3;readyAt++)for(int pauseAt=0;pauseAt<3;pauseAt++) {
        LegacyStartupGate gate={0};gate.foreground=1;int started=0;
        for(int event=0;event<3;event++) {
            if(event==readyAt)gate.playable=1;else gate.presented_frames++;
            if(event==pauseAt) {
                gate.foreground=0;assert(!legacy_startup_gate_take(&gate));checks++;
                gate.foreground=1;
            }
            int allowed=gate.playable && gate.presented_frames>=2;
            int actual=legacy_startup_gate_take(&gate);
            assert(actual==(allowed && !started));checks++;started|=actual;
        }
        assert(started);checks++;
        assert(!legacy_startup_gate_take(&gate));checks++;
    }
    LegacyStartupGate cancelled={2,1,1,0,1};assert(!legacy_startup_gate_take(&cancelled));checks++;
    LegacyStartupGate notVisible={0,1,1,0,0};assert(!legacy_startup_gate_take(&notVisible));checks++;
    notVisible.presented_frames=1;assert(!legacy_startup_gate_take(&notVisible));checks++;
    LegacyStartupGate notLoaded={2,0,1,0,0};assert(!legacy_startup_gate_take(&notLoaded));checks++;
    printf("PASS: %d intro readiness/order, presentation, focus and cancellation checks\n",checks);
}
