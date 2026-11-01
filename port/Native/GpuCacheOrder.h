#import <Foundation/Foundation.h>
// Return an owned key: removing it from either container must not invalidate
// the key while the other container is being updated.
static NSString *legacy_gpu_cache_victim(NSArray *order) {
    for(NSString *key in order)if(![key hasPrefix:@"glyphs:"])return [key retain];
    return [order count]?[[order objectAtIndex:0] retain]:nil;
}
static void legacy_gpu_cache_remove(NSMutableDictionary *textures,NSMutableArray *order,NSString *key) {
    [order removeObject:key];[textures removeObjectForKey:key];
}
