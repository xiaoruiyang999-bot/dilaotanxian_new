#!/usr/bin/env python3
"""从白/浅灰底原图（如 2048×2048 AI 生成图）抠出角色本体并缩放到游戏尺寸。

处理链：
1. 背景+阴影一锅端：从四边出发，把"近白/浅灰低饱和"（min>--bg-min 且
   max-min<--bg-spread）的连通区域置透明（连通域标记取与图像边界相连的块，
   等价于洪水填充；内部白色胸毛/眼睛不连通背景，天然保留）。
2. 剥白色贴纸描边：迭代把"邻接透明区的白色像素"（min>--outline-white-min 且
   max-min<--outline-white-spread）置透明，直到没有可剥像素或达到
   --outline-max-iters（撞上限会报警：可能深色轮廓有缺口、剥进了内部白毛）。
3. 连通域过滤：删除面积 < --min-blob 的块（水印文字、碎屑；2048 尺度建议
   20000，且须 < 主体面积的 1/10，先 dry-run 看 Top5 面积分布）。
4. 边缘 bleed（复用 cleanup_frames.edge_decontaminate，含白色部件保护）。
5. LANCZOS 缩放到 --out-size × --out-size，覆盖保存原路径。

安全闸：全部帧处理完先比对最终主体面积，与 6 帧中位数偏差 >15% 的帧触发
中止（不写盘），说明抠过头或没抠干净。

用法：
    python tools/cutout_white_bg.py "Assets/Art/Characters/Warrior/Walk/warrior_walk_1_*.png" --dry-run
    python tools/cutout_white_bg.py "路径/*.png" --bg-min 150 --bg-spread 40 \
        --outline-white-min 200 --outline-white-spread 40 --min-blob 20000 --out-size 256
"""
import argparse
import glob
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

from cleanup_frames import edge_decontaminate, CONNECTIVITY


def cutout(arr, args):
    """对单帧 2048 RGB(A) 数组执行抠图链，返回 (RGBA 结果, 统计 dict)。"""
    h, w = arr.shape[:2]
    rgb = arr[..., :3].astype(np.int64)
    mn = rgb.min(-1)
    mx = rgb.max(-1)
    spread = mx - mn
    stats = {"size": (w, h)}

    out = np.dstack([arr[..., :3], np.full((h, w), 255, dtype=np.uint8)])

    # 1) 背景+阴影：近白/浅灰低饱和候选区中与图像边界连通的块
    bg_cand = (mn > args.bg_min) & (spread < args.bg_spread)
    labels, num = ndimage.label(bg_cand, structure=CONNECTIVITY)
    border_labels = set(np.unique(np.concatenate([
        labels[0, :], labels[-1, :], labels[:, 0], labels[:, -1]]))) - {0}
    bg_mask = np.isin(labels, list(border_labels)) if border_labels else np.zeros((h, w), bool)
    stats["bg_pixels"] = int(bg_mask.sum())
    stats["bg_components"] = len(border_labels)
    out[bg_mask] = (0, 0, 0, 0)

    # 2) 剥白色贴纸描边：邻接透明区的白色像素，逐层剥皮
    solid = out[..., 3] > 0
    peel_total = 0
    iters = 0
    for it in range(args.outline_max_iters):
        eroded = ndimage.binary_erosion(solid, structure=np.ones((3, 3), bool), border_value=0)
        boundary = solid & ~eroded
        white_ring = boundary & (mn > args.outline_white_min) & (spread < args.outline_white_spread)
        if not white_ring.any():
            break
        out[white_ring] = (0, 0, 0, 0)
        peel_total += int(white_ring.sum())
        solid = out[..., 3] > 0
        iters = it + 1
    stats["peel_pixels"] = peel_total
    stats["peel_iters"] = iters
    stats["peel_hit_cap"] = bool(iters == args.outline_max_iters)

    # 3) 连通域过滤：删小块（水印文字、碎屑）
    labels, num = ndimage.label(solid, structure=CONNECTIVITY)
    sizes = ndimage.sum_labels(solid, labels, index=np.arange(1, num + 1)).astype(np.int64)
    order = np.argsort(-sizes)
    stats["blob_top5"] = [int(sizes[i]) for i in order[:5]]
    stats["blobs_total"] = int(num)
    keep = sizes >= args.min_blob
    kill = solid & ~np.isin(labels, np.nonzero(keep)[0] + 1)
    stats["blob_pixels_removed"] = int(sizes[~keep].sum()) if np.any(~keep) else 0
    stats["blobs_removed"] = int(np.count_nonzero(~keep))
    out[kill] = (0, 0, 0, 0)

    # 4) 边缘 bleed（白色部件保护逻辑复用）
    if not args.no_bleed:
        out, bleed_mask, passes = edge_decontaminate(
            out, out[..., 3] > 0, args.bleed_white_min, args.bleed_white_spread,
            args.bleed_neighbor_frac, args.bleed_passes)
        stats["bleed_pixels"] = int(bleed_mask.sum())
        stats["bleed_passes"] = passes
    else:
        stats["bleed_pixels"] = 0
        stats["bleed_passes"] = []

    stats["body_pixels"] = int((out[..., 3] > 0).sum())
    return out, stats


def main():
    ap = argparse.ArgumentParser(description="白/浅灰底原图抠出角色本体 -> 缩放透明底 PNG")
    ap.add_argument("inputs", nargs="+", help="输入文件（可多个，支持 glob）")
    ap.add_argument("--bg-min", type=int, default=150, help="背景/阴影判定：min(R,G,B) 高于此值")
    ap.add_argument("--bg-spread", type=int, default=40, help="背景/阴影判定：max-min 低于此值")
    ap.add_argument("--outline-white-min", type=int, default=200)
    ap.add_argument("--outline-white-spread", type=int, default=40)
    ap.add_argument("--outline-max-iters", type=int, default=40)
    ap.add_argument("--min-blob", type=int, default=20000, help="2048 尺度的连通块阈值")
    ap.add_argument("--out-size", type=int, default=256)
    ap.add_argument("--no-bleed", action="store_true")
    ap.add_argument("--bleed-white-min", type=int, default=180)
    ap.add_argument("--bleed-white-spread", type=int, default=60)
    ap.add_argument("--bleed-neighbor-frac", type=float, default=0.5)
    ap.add_argument("--bleed-passes", type=int, default=2)
    ap.add_argument("--dry-run", action="store_true", help="只统计不写盘")
    args = ap.parse_args()

    files = []
    for pat in args.inputs:
        matched = sorted(glob.glob(pat))
        files.extend(matched if matched else [pat])

    results = []
    warn = False
    for f in files:
        arr = np.array(Image.open(f).convert("RGB"))
        out, st = cutout(arr, args)
        results.append((f, out, st))
        print(f"{f}:")
        print(f"  背景+阴影 {st['bg_pixels']} px（{st['bg_components']} 块）；"
              f"剥白边 {st['peel_pixels']} px / {st['peel_iters']} 层"
              f"{'（撞迭代上限！）' if st['peel_hit_cap'] else ''}")
        print(f"  连通块 {st['blobs_total']} 个，Top5 面积 {st['blob_top5']}；"
              f"删除 {st['blobs_removed']} 块 / {st['blob_pixels_removed']} px")
        print(f"  bleed {st['bleed_pixels']} px（各轮 {st['bleed_passes']}）；"
              f"最终主体 {st['body_pixels']} px")
        if st["peel_hit_cap"]:
            print("  [警告] 剥皮达到迭代上限，深色轮廓可能有缺口、剥进内部白毛！")
            warn = True
        if st["blob_top5"] and st["blob_top5"][0] < args.min_blob:
            print("  [警告] 最大连通块小于 min-blob，主体被误删！")
            warn = True

    # 安全闸：主体面积与中位数偏差 >15% 中止
    areas = np.array([st["body_pixels"] for _, _, st in results], dtype=float)
    median = float(np.median(areas))
    for (f, _, st) in results:
        dev = abs(st["body_pixels"] - median) / max(median, 1)
        if dev > 0.15:
            print(f"[警告] {f} 主体面积 {st['body_pixels']} 与中位数 {int(median)} 偏差 {dev:.1%} >15%，中止！",
                  file=sys.stderr)
            warn = True

    if warn:
        print("\n存在警告：未写盘。请调参后重跑。", file=sys.stderr)
        sys.exit(2)

    if args.dry_run:
        print(f"\nDRY-RUN（未写盘），共 {len(results)} 帧；主体面积中位数 {int(median)} px。")
        return

    for f, out, st in results:
        img = Image.fromarray(out, "RGBA").resize((args.out_size, args.out_size), Image.LANCZOS)
        img.save(f)
        print(f"已写回 {f}（{args.out_size}x{args.out_size}）")


if __name__ == "__main__":
    main()
