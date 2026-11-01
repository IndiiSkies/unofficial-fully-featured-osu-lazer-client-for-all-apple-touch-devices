/* Mono 3.0 includes this optional private header unconditionally on Darwin.
 * The iOS 4.2 SDK does not ship it. Mono uses only this policy selector,
 * and looks up pthread_mutexattr_setpolicy_np dynamically before calling it.
 * FIRSTFIT's value is 3 in Apple's pthread headers. This supplies no function
 * implementation and does not assert that the target exports that function.
 */
#ifndef OSU_LEGACY_PTHREAD_SPIS_H
#define OSU_LEGACY_PTHREAD_SPIS_H
#define _PTHREAD_MUTEX_POLICY_FIRSTFIT 3
#endif
