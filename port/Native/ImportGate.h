#ifndef LEGACY_IMPORT_GATE_H
#define LEGACY_IMPORT_GATE_H
void legacy_import_worker_register(void);
void legacy_import_pause(int paused);
void legacy_import_checkpoint(void);
void legacy_import_begin_work(void);
void legacy_import_interrupt(void);
int legacy_import_interrupted(void);
#endif
