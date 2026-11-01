#include "ImportGate.h"
#include <pthread.h>
#include <stdint.h>
static pthread_mutex_t lock=PTHREAD_MUTEX_INITIALIZER;
static pthread_cond_t changed=PTHREAD_COND_INITIALIZER;
static pthread_once_t once=PTHREAD_ONCE_INIT;
static pthread_key_t worker;
static pthread_key_t ticket;
static uintptr_t generation;
static int paused;
static void create_key(void){pthread_key_create(&worker,NULL);pthread_key_create(&ticket,NULL);}
void legacy_import_begin_work(void){pthread_once(&once,create_key);pthread_mutex_lock(&lock);pthread_setspecific(ticket,(void *)(generation+1));pthread_mutex_unlock(&lock);}
void legacy_import_interrupt(void){pthread_mutex_lock(&lock);generation++;pthread_mutex_unlock(&lock);}
int legacy_import_interrupted(void){
    pthread_once(&once,create_key);if(!pthread_getspecific(worker))return 0;
    pthread_mutex_lock(&lock);int result=(uintptr_t)pthread_getspecific(ticket)!=generation+1;pthread_mutex_unlock(&lock);return result;
}
void legacy_import_worker_register(void){pthread_once(&once,create_key);pthread_setspecific(worker,(void *)1);legacy_import_begin_work();}
void legacy_import_pause(int value){pthread_mutex_lock(&lock);paused=value!=0;if(!paused)pthread_cond_broadcast(&changed);pthread_mutex_unlock(&lock);}
void legacy_import_checkpoint(void){
    pthread_once(&once,create_key);if(!pthread_getspecific(worker))return;
    pthread_mutex_lock(&lock);while(paused)pthread_cond_wait(&changed,&lock);pthread_mutex_unlock(&lock);
}
