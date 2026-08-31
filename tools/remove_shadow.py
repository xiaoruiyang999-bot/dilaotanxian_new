#!/usr/bin/env python3
"""tools/remove_shadow.py — 去除序列帧中烘焙的蓝灰地面阴影（v0.7.5 素材管线补丁）

判别：alpha>0 且 B-R > --blue-gap（默认 3，蓝灰特征）且 max-min < --spread（默认 25，低饱和）
且亮度 mean < --max-bright（默认 210）。角色暖棕描边/纯黑描边 R>=B 不受影响。
处理：命中像素 alpha 清零 → 小连通域碎屑过滤 → 边缘 bleed 可选（复用 cleanup_frames 逻辑，
本脚本只清阴影+碎屑）。输出接触表到 tools/preview/ 供目检。

用法：
  python tools/remove_shadow.py "Assets/Art/Characters/Warrior/Walk/*.png" [--dry-run]
"""
import sys, glob, os, argparse
import numpy as np
from PIL import Image
from scipy import ndimage

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("pattern")
    ap.add_argument("--blue-gap", type=int, default=3)
    ap.add_argument("--spread", type=int, default=25)
    ap.add_argument("--max-bright", type=int, default=210)
    ap.add_argument("--min-blob", type=int, default=16)
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    files = sorted(glob.glob(args.pattern))
    if not files:
        print(f"无匹配文件: {args.pattern}"); sys.exit(1)

    thumbs = []
    for path in files:
        im = Image.open(path).convert("RGBA")
        a = np.array(im).astype(np.int16)
        rgb, alpha = a[:,:,:3], a[:,:,3]
        r, g, b = rgb[:,:,0], rgb[:,:,1], rgb[:,:,2]
        mx = rgb.max(axis=2); mn = rgb.min(axis=2); mean = rgb.mean(axis=2)
        shadow = (alpha>0) & ((b-r) > args.blue_gap) & ((mx-mn) < args.spread) & (mean < args.max_bright)
        # 阴影碎屑过滤：清掉阴影后暴露的孤立小块
        n_shadow = int(shadow.sum())
        removed_crumbs = 0
        if not args.dry_run:
            out = np.array(im)
            out[:,:,3][shadow] = 0
            remain = out[:,:,3] > 0
            lbl, n = ndimage.label(remain, structure=np.ones((3,3)))
            for i in range(1, n+1):
                m = lbl == i
                if m.sum() < args.min_blob:
                    out[:,:,3][m] = 0
                    removed_crumbs += int(m.sum())
            Image.fromarray(out, "RGBA").save(path)
        print(f"{os.path.basename(path)}: 阴影 {n_shadow} px, 碎屑 {removed_crumbs} px"
              + (" (dry-run)" if args.dry_run else " 已写盘"))
        # 目检缩略图（灰底）
        if not args.dry_run:
            th = Image.open(path).convert("RGBA")
            bg = Image.new("RGB", th.size, (128,128,128))
            bg.paste(th, (0,0), th)
            thumbs.append(bg)

    if thumbs:
        sheet = Image.new("RGB", (thumbs[0].width*len(thumbs), thumbs[0].height), (128,128,128))
        for i, t in enumerate(thumbs):
            sheet.paste(t, (thumbs[0].width*i, 0))
        os.makedirs("tools/preview", exist_ok=True)
        out_name = "tools/preview/noshadow_" + os.path.basename(os.path.dirname(files[0])) + ".png"
        sheet.save(out_name)
        print(f"接触表: {out_name}")

if __name__ == "__main__":
    main()
