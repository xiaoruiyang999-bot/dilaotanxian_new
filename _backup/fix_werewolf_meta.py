# -*- coding: utf-8 -*-
"""修正 Werewolf 纹理 meta：清除自动切片残留，写入 Single 主 sprite 注册（21300000）。"""
import glob

count = 0
for meta in glob.glob('Assets/Art/Werewolf/*/*.png.meta'):
    meta = meta.replace(chr(92), '/')
    name = meta.split('/')[-1].replace('.png.meta', '')
    lines = open(meta, encoding='utf-8', newline='').read().split('\n')
    out, i, changed = [], 0, False
    while i < len(lines):
        ln = lines[i]
        if ln.strip() == 'sprites:' and i > 0 and lines[i-1].strip() == 'spriteSheet:':
            out.append('    sprites: []')
            i += 1
            while i < len(lines) and (lines[i].startswith('    - ') or (lines[i].startswith('      ') and lines[i].strip())):
                i += 1
            changed = True
            continue
        if ln.strip() == 'internalIDToNameTable:':
            out.append('  internalIDToNameTable:')
            out.append('  - first:')
            out.append('      213: 21300000')
            out.append('    second: ' + name)
            i += 1
            while i < len(lines) and (lines[i].startswith('  - ') or lines[i].startswith('    second:') or lines[i].startswith('      213:')):
                i += 1
            changed = True
            continue
        out.append(ln)
        i += 1
    if changed:
        open(meta, 'w', encoding='utf-8', newline='').write('\n'.join(out))
        count += 1
print('fixed %d meta files' % count)
