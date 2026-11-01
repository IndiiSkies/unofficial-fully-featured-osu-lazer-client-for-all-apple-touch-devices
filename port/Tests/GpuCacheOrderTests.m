#import "../Native/GpuCacheOrder.h"
#include <assert.h>
int main(void) {
    NSAutoreleasePool *pool=[[NSAutoreleasePool alloc] init];
    NSMutableArray *order=[NSMutableArray arrayWithObjects:@"glyphs:12",@"background:a",@"glyphs:20",@"background:b",nil];
    NSMutableDictionary *cache=[NSMutableDictionary dictionary];
    for(NSString *key in order)[cache setObject:@1 forKey:key];
    const char *expected[]={"background:a","background:b","glyphs:12","glyphs:20"};
    for(int i=0;i<4;i++) {
        NSString *key=legacy_gpu_cache_victim(order);assert([key isEqualToString:[NSString stringWithUTF8String:expected[i]]]);
        legacy_gpu_cache_remove(cache,order,key);assert(![cache objectForKey:key]);assert(![order containsObject:key]);[key release];
        assert([cache count]==[order count]);for(NSString *remaining in order)assert([cache objectForKey:remaining]);
    }
    assert(!legacy_gpu_cache_victim(order));
    puts("PASS: mixed glyph/background eviction keeps both cache containers consistent");[pool drain];return 0;
}
