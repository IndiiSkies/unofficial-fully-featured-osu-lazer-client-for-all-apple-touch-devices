#!/usr/bin/env python3
import pathlib,tarfile,hashlib,subprocess,concurrent.futures,os
root=pathlib.Path(__file__).resolve().parent.parent
archive=root/'toolchain/vendor/bearssl-0.6.tar.gz'
assert hashlib.sha256(archive.read_bytes()).hexdigest()=='6705bba1714961b41a728dfc5debbe348d2966c117649392f8c8139efc83ff14'
work=root/'build/bearssl';source=work/'bearssl-0.6';out=work/'armv6';out.mkdir(parents=True,exist_ok=True)
if not source.exists():
 with tarfile.open(archive) as package:package.extractall(work,filter='data')
env=dict(os.environ,IOS_SDK=str(root/'toolchain/iPhoneOS4.2.sdk'),IOS_TARGET='armv6-apple-ios4.2.1',IOS_CRT='crt1.3.1.o')
def build(path):
 obj=out/(str(path.relative_to(source)).replace('/','_')+'.o')
 if not obj.exists() or obj.stat().st_mtime<path.stat().st_mtime:
  subprocess.run([str(root/'toolchain/armv6-clang'),'-Os','-DBR_USE_URANDOM=1','-DBR_USE_WIN32_RAND=0','-I'+str(source/'inc'),'-I'+str(source/'src'),'-c',str(path),'-o',str(obj)],env=env,check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
 return str(obj)
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:objects=list(pool.map(build,sorted((source/'src').rglob('*.c'))))
subprocess.run(['/usr/bin/ar','rcs',str(out/'libbearssl.a')]+objects,check=True)
print('Built BearSSL 0.6 for ARMv6/iOS 4.2.1')
