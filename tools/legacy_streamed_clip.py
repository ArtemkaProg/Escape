"""Read verified Unity4 streamed cubic animation channels from private source."""
import struct,math
def read_streamed_clip(shared, clipid, Reader):
    clip=next(o for o in shared.objects if o['id']==clipid and o['class_id']==74)
    clipraw=shared.data[clip['offset']:clip['offset']+clip['size']]
    r = Reader(clipraw, '<')
    name=r.aligned_string(); assert (r.i32(),r.u8(),r.u8()) == (2,0,1)
    r.align(); assert [r.i32() for _ in range(6)] == [0] * 6
    assert r.f32() == 60 and r.i32() == 0
    assert not any(r.f32() for _ in range(6))
    muscle_bytes = r.u32()
    def xform(): r.read(48)
    def array():
        n = r.i32(); assert 0 <= n < 500000; return [r.f32() for _ in range(n)]
    xform(); r.read(32)
    for _ in range(r.i32()): xform(); r.read(8)
    for _ in range(2): xform(); array(); r.read(16)
    array()
    for _ in range(5): xform()
    r.read(16)
    nw = r.i32(); words = r.read(nw * 4); ns = r.u32()
    assert (r.i32(), r.u32(), r.f32(), r.f32()) == (1, 0, 60., 0.)
    assert not array() and not array() and r.i32() == 0
    start, duration, *extra = [r.f32() for _ in range(6)]
    assert start == 0 and not any(extra)
    r.read(r.i32() * 4); r.read(r.i32() * 8)
    flags = [r.u8() for _ in range(9)]; r.align()
    assert flags == [0,0,0,0,0,0,0,1,0]
    bindings = []
    for _ in range(r.i32()):
        b = dict(pathHash=r.u32(), attribute=r.u32(), scriptFileId=r.i32(), scriptId=r.i32(),
                 classId=r.u16(), custom=r.u8(), pptr=r.u8())
        r.align(); bindings.append(b)
    assert r.i32() == r.i32() == 0 and r.p == len(clipraw)
    streams = [[] for _ in range(ns)]; p = 0
    while p < len(words):
        time, n = struct.unpack_from('<fi', words, p); p += 8
        assert 0 <= n <= ns
        for _ in range(n):
            index, a, b, c, d = struct.unpack_from('<iffff', words, p); p += 20
            assert 0 <= index < ns and all(math.isfinite(v) for v in (a,b,c,d))
            if 0 <= time <= duration: streams[index].append(dict(time=time, a=a, b=b, c=c, d=d))
    assert p == len(words)  and all(s and s[0]['time'] == 0 for s in streams)

    return dict(name=name,duration=duration,bindings=bindings,streams=[dict(keys=s) for s in streams])
