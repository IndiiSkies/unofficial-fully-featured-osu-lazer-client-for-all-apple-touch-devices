#ifndef LEGACY_GPU_COMMANDS_H
#define LEGACY_GPU_COMMANDS_H
#include <math.h>
// Arity includes the opcode. Shape values are already represented as floats
// by ES 1.1; one ordered stream avoids a managed/native call per primitive.
static int legacy_gpu_command_size(float opcode){
    if(opcode==1)return 10; /* rect */
    if(opcode==2)return 9;  /* circle */
    if(opcode==3)return 10; /* arc */
    if(opcode==4)return 9;  /* ellipse */
    if(opcode==5)return 4;  /* push x y angle */
    if(opcode==6)return 1;  /* pop */
    return 0;
}
static int legacy_gpu_commands_valid(const float *commands,int count){
    if(!commands || count<0 || count>16384)return 0;
    for(int i=0;i<count;){int size=legacy_gpu_command_size(commands[i]);if(!size || size>count-i)return 0;for(int j=1;j<size;j++)if(!isfinite(commands[i+j]))return 0;i+=size;}return 1;
}
#endif
