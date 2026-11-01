#include "../Native/ImportGate.h"
#include <pthread.h>
#include <unistd.h>
#include <stdio.h>
#include <assert.h>
static pthread_mutex_t lock=PTHREAD_MUTEX_INITIALIZER;
static pthread_cond_t changed=PTHREAD_COND_INITIALIZER;
static int started,finished;
static void *run(void *unused){
    (void)unused;legacy_import_worker_register();
    pthread_mutex_lock(&lock);started=1;pthread_cond_signal(&changed);pthread_mutex_unlock(&lock);
    legacy_import_checkpoint();
    assert(legacy_import_interrupted());legacy_import_begin_work();assert(!legacy_import_interrupted());
    pthread_mutex_lock(&lock);finished=1;pthread_mutex_unlock(&lock);return NULL;
}
int main(void){
    pthread_t worker;legacy_import_pause(1);
    legacy_import_checkpoint(); // Gameplay and UI threads must never wait.
    assert(!pthread_create(&worker,NULL,run,NULL));
    pthread_mutex_lock(&lock);while(!started)pthread_cond_wait(&changed,&lock);pthread_mutex_unlock(&lock);
    usleep(20000);pthread_mutex_lock(&lock);assert(!finished);pthread_mutex_unlock(&lock);
    legacy_import_interrupt();assert(!legacy_import_interrupted());
    legacy_import_pause(0);pthread_join(worker,NULL);assert(finished);
    puts("PASS: importer sleeps during gameplay, UI remains unblocked, and worker resumes afterward");return 0;
}
