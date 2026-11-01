#!/usr/bin/env python3
"""Bake lazer IntroTriangles for the ARMv6 device's H.264 decoder.
Uses original Torus glyphs, icons, and LogoAnimation's red-progress/green-alpha
texture shader. Source timing: osu.Game/Screens/Menu/IntroTriangles.cs.
Run with extracted resource folder, output mp4, and optional preview folder.
"""
import argparse, math, random, struct, subprocess
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

W,H,FPS=480,320,30
A=Image.Resampling.LANCZOS

def font_data(root):
    data=(root/'Fonts.Torus.Torus-Light.bin').read_bytes()
    assert data[:4]==b'BMF\x03'
    glyphs={};pos=4
    while pos<len(data):
        kind=data[pos];size=struct.unpack_from('<I',data,pos+1)[0];block=data[pos+5:pos+5+size];pos+=5+size
        if kind==4:
            for off in range(0,size,20):
                ch,x,y,w,h,xo,yo,advance,page,channel=struct.unpack_from('<IHHHHhhhBB',block,off)
                glyphs[chr(ch)]=(x,y,w,h,xo,yo,advance,page)
    return glyphs,Image.open(root/'Fonts.Torus.Torus-Light_0.png').convert('RGBA')

def render_text(text,spacing,glyphs,atlas):
    scale=21/100;parts=[];advance=0
    for ch in text:
        x,y,w,h,xo,yo,step,page=glyphs[ch];assert page==0
        parts.append((atlas.crop((x,y,x+w,y+h)),advance+xo*scale,yo*scale,w*scale,h*scale))
        advance+=step*scale+spacing
    out=Image.new('RGBA',(max(1,math.ceil(advance-spacing)),42))
    for glyph,x,y,w,h in parts:
        if w>0 and h>0:out.alpha_composite(glyph.resize((max(1,round(w)),max(1,round(h))),A),(round(x),round(y)))
    return out.crop(out.getbbox()) if out.getbbox() else out

def centre(frame,image,x=240,y=160):
    frame.alpha_composite(image,(round(x-image.width/2),round(y-image.height/2)))

def frames(root):
    glyphs,atlas=font_data(root)
    icons=[Image.open(root/f'Textures.Icons.Ruleset{name}.png').convert('RGBA') for name in ['Osu','Taiko','Catch','Mania']]
    textures=[np.array(Image.open(root/f'Textures.Intro.Triangles.logo-{name}.png').convert('RGB')) for name in ['highlight','background']]
    rng=random.Random(421)
    triangles=[(1.06+i*.022,rng.uniform(144,336),rng.uniform(134.4,185.6),rng.uniform(.2,1.2)*40,rng.choice([True,False])) for i in range(18)]
    for index in range(90):
        t=index/FPS
        frame=Image.new('RGBA',(W,H),(0,0,0,255))
        if .2<=t<1.45:
            text='wel' if t<.4 else 'welcome' if t<.7 else 'welcome to' if t<.9 else 'welcome to osu!'
            spacing=2.5+max(0,t-.9)/5*22.5
            centre(frame,render_text(text,spacing,glyphs,atlas),y=157.5)
            draw=ImageDraw.Draw(frame,'RGBA')
            for start,x,y,size,outline in triangles:
                age=t-start
                if 0<=age<.12:
                    c=round(255*(1-age/.12));points=[(x,y-size/2),(x-size*.433,y+size/4),(x+size*.433,y+size/4)]
                    if outline:draw.line(points+[points[0]],fill=(c,c,c,255),width=2)
                    else:draw.polygon(points,fill=(c,c,c,255))
        if 1.45<=t<2.08:
            if t<1.65:scale,gap=1,100
            elif t<1.85:scale,gap=2,15
            else:scale,gap=4,5
            group=1-.2*min(1,(t-1.45)) if t<1.85 else .8+.5*min(1,t-1.85)
            size=15*scale*group;gap*=group
            total=4*size+3*gap
            for i,icon in enumerate(icons):centre(frame,icon.resize((round(size),round(size)),A),240-total/2+size/2+i*(size+gap))
        if t>=2.08:
            p=min(1,(t-2.08)/.92)
            late=max(0,(p-.7)/.3)
            scale=(1.2-.2*p*p)*(1.2-.8*late**5)
            size=round(480*scale)
            # Original sh_LogoAnimation.fs: R=appearance threshold, G=alpha.
            for texture,tint in zip(textures,[255,153]):
                alpha=np.where(texture[:,:,0]/255.0<p,texture[:,:,1],0).astype(np.uint8)
                layer=Image.new('RGBA',(texture.shape[1],texture.shape[0]),(tint,tint,tint,0));layer.putalpha(Image.fromarray(alpha))
                centre(frame,layer.resize((size,size),A))
        yield t,frame.convert('RGB')

def main():
    parser=argparse.ArgumentParser();parser.add_argument('resources',type=Path);parser.add_argument('output',type=Path);parser.add_argument('--preview',type=Path);args=parser.parse_args()
    args.output.parent.mkdir(parents=True,exist_ok=True)
    ffmpeg='/opt/homebrew/bin/ffmpeg'
    command=[ffmpeg,'-v','error','-y','-f','rawvideo','-pixel_format','rgb24','-video_size','480x320','-framerate','30','-i','pipe:0','-an','-c:v','libx264','-profile:v','baseline','-level:v','3.0','-pix_fmt','yuv420p','-crf','18','-maxrate','1200k','-bufsize','1200k','-movflags','+faststart',str(args.output)]
    process=subprocess.Popen(command,stdin=subprocess.PIPE)
    samples=[]
    for index,(t,frame) in enumerate(frames(args.resources)):
        process.stdin.write(frame.tobytes())
        if index in [6,12,21,27,36,45,51,57,66,78,84,89]:
            if args.preview:
                args.preview.mkdir(parents=True,exist_ok=True);frame.save(args.preview/f'{index:02d}.png')
            tile=frame.resize((240,160),A);ImageDraw.Draw(tile).text((5,5),f'{t:.2f}s',fill='yellow');samples.append(tile)
    process.stdin.close();assert process.wait()==0
    if args.preview:
        sheet=Image.new('RGB',(960,480))
        for i,sample in enumerate(samples):sheet.paste(sample,((i%4)*240,(i//4)*160))
        sheet.save(args.preview/'contact-sheet.jpg')
    print('Rendered 90 frames: original text stages, glitch triangles, ruleset icons and progress-mask logo zoom.')
if __name__=='__main__':main()
