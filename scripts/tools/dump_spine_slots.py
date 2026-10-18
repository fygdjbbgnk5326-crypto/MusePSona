# -*- coding: utf-8 -*-
"""Dump Spine 3.8 .skel slots, setup attachments, skins, and attachment paths."""
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.dirname(HERE))


class Inp:
    def __init__(self, data):
        self.d = data
        self.i = 0
        self.strings = []

    def u8(self):
        b = self.d[self.i]
        self.i += 1
        return b

    def s8(self):
        v = self.u8()
        return v - 256 if v > 127 else v

    def boolean(self):
        return self.u8() != 0

    def i32(self):
        v = struct.unpack_from(">i", self.d, self.i)[0]
        self.i += 4
        return v

    def f32(self):
        v = struct.unpack_from(">f", self.d, self.i)[0]
        self.i += 4
        return v

    def vint(self, pos=True):
        b = self.u8()
        r = b & 0x7F
        if b & 0x80:
            b = self.u8()
            r |= (b & 0x7F) << 7
            if b & 0x80:
                b = self.u8()
                r |= (b & 0x7F) << 14
                if b & 0x80:
                    b = self.u8()
                    r |= (b & 0x7F) << 21
                    if b & 0x80:
                        r |= (self.u8() & 0x7F) << 28
        if pos:
            return r
        return (r >> 1) ^ -(r & 1)

    def string(self):
        n = self.vint()
        if n == 0:
            return None
        if n == 1:
            return ""
        n -= 1
        s = self.d[self.i : self.i + n].decode("utf-8")
        self.i += n
        return s

    def sref(self):
        idx = self.vint()
        return None if idx == 0 else self.strings[idx - 1]


def skip_vertices(inp, count):
    if not inp.boolean():
        inp.i += 4 * (count * 2)
        return
    for _ in range(count):
        bc = inp.vint()
        for _ in range(bc):
            inp.vint()
            inp.i += 12


def skip_shorts(inp):
    n = inp.vint()
    inp.i += n * 2

image.png
        if nonessential:
            inp.i += 4
    elif t == 5:  # point
        inp.i += 12
        if nonessential:
            inp.i += 4
    elif t == 6:  # clipping
        end = inp.vint()
        path = "end=%s" % end
        vc = inp.vint()
        skip_vertices(inp, vc)
        if nonessential:
            inp.i += 4
    return t, path


def dump(path):
    inp = Inp(open(path, "rb").read())
    h = inp.string()
    ver = inp.string()
    inp.f32()
    inp.f32()
    inp.f32()
    inp.f32()
    non = inp.boolean()
    if non:
        inp.f32()
        inp.string()
        inp.string()
    n = inp.vint()
    inp.strings = [inp.string() for _ in range(n)]
    bones = []
    n = inp.vint()
    print("=== bones %d ===" % n)
    for i in range(n):
        name = inp.string()
        parent = inp.vint() if i else 0
        rot = inp.f32()
        x = inp.f32()
        y = inp.f32()
        sx = inp.f32()
        sy = inp.f32()
        inp.f32()
        inp.f32()
        length = inp.f32()
        inp.vint()
        inp.boolean()
        if non:
            inp.i32()
        bones.append(name)
        low = name.lower()
        if any(k in low for k in ("laser", "body", "flower", "petal_h", "light", "root", "head", "feidie", "normal", "helmet", "horn")):
            print("%2d bone=%-22s par=%-22s rot=%7.2f xy=(%7.2f,%7.2f) scl=(%5.3f,%5.3f) len=%7.2f" % (
                i, name, bones[parent] if i else "-", rot, x, y, sx, sy, length))
    slots = []
    n = inp.vint()
    print("version", ver, "hash", h, "nonessential", non)
    print("=== slots %d ===" % n)
    for i in range(n):
        sn = inp.string()
        bi = inp.vint()
        col = inp.i32() & 0xFFFFFFFF
        dark = inp.i32()
        att = inp.sref()
        blend = inp.vint()
        a = col & 0xFF
        slots.append(sn)
        print("%2d slot=%-22s bone=%-18s setup=%-28s a=%3d blend=%d dark=%s" % (
            i, sn, bones[bi], att, a, blend, dark != -1))

    def skip_ik():
        nn = inp.vint()
        for _ in range(nn):
            inp.string()
            inp.vint()
            inp.boolean()
            for _ in range(inp.vint()):
                inp.vint()
            inp.vint()
            inp.f32()
            inp.f32()
            inp.s8()
            inp.boolean()
            inp.boolean()
            inp.boolean()

    def skip_tf():
        nn = inp.vint()
        for _ in range(nn):
            inp.string()
            inp.vint()
            inp.boolean()
            for _ in range(inp.vint()):
                inp.vint()
            inp.vint()
            inp.boolean()
            inp.boolean()
            inp.i += 4 * 9

    def skip_path():
        nn = inp.vint()
        for _ in range(nn):
            inp.string()
            inp.vint()
            inp.boolean()
            for _ in range(inp.vint()):
                inp.vint()
            inp.vint()
            inp.vint()
            inp.vint()
            inp.vint()
            inp.i += 4 * 5

    skip_ik()
    skip_tf()
    skip_path()

    def read_skin(default):
        if default:
            sc = inp.vint()
            if sc == 0:
                return None, []
            name = "default"
        else:
            name = inp.sref()
            for _ in range(inp.vint()):
                inp.vint()
            for _ in range(3):
                for _ in range(inp.vint()):
                    inp.vint()
            sc = inp.vint()
        atts = []
        for _ in range(sc):
            si = inp.vint()
            for _ in range(inp.vint()):
                an = inp.sref()
                t, path = skip_attachment(inp, non)
                atts.append((si, slots[si] if si < len(slots) else "?", an, t, path))
        return name, atts

    skins = []
    name, atts = read_skin(True)
    if name:
        skins.append((name, atts))
    extra = inp.vint()
    for _ in range(extra):
        skins.append(read_skin(False))
    print("=== skins ===")
    for name, atts in skins:
        print("skin", name, "attachments", len(atts))
        for si, sn, an, t, path in atts:
            kinds = "region bbox mesh linked path point clip".split()
            k = kinds[t] if t < len(kinds) else str(t)
            print("  [%d] %s  name=%s  type=%s  path=%s" % (si, sn, an, k, path))


if __name__ == "__main__":
    p = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        REPO,
        "PlayClient", "Unity", "Assets", "StreamingAssets",
        "MdLooks", "spine", "scene_09", "0905_air", "0905_air.skel",
    )
    dump(p)
