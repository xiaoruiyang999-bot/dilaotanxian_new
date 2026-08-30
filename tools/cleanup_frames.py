#!/usr/bin/env python3
"""清理序列帧 PNG 的抠图残留（如战士行走帧的白色噪点/白边）。

处理流程：读 RGBA -> alpha 阈值（低于 --alpha-threshold 直接清零）
-> 连通域标记（基于阈值后的 alpha）-> 删除面积小于 --min-blob 的连通块
-> 边缘去白边（despill：边界上"高亮低饱和"的白色残留像素，用邻近不透明
   非白像素的平均色替换其 RGB，保留 alpha；白色邻居占比高的像素视为
   真实白色部件如胸毛/爪子，跳过）
-> 可选 1px 腐蚀（--erode 1：清掉最外圈边界像素，并再跑一次小连通块过滤）
-> 保存（覆盖原路径，尺寸不变）。

用法：
    python tools/cleanup_frames.py "Assets/Art/Characters/Warrior/Walk/warrior_walk_1_*.png" --dry-run
    python tools/cleanup_frames.py frame.png --alpha-threshold 16 --min-blob 48 \
        --white-min 180 --white-spread 60 --white-neighbor-frac 0.5 --edge-passes 2 --erode 0

参数：
    inputs                  输入文件（可多个，支持 glob 通配）
    --alpha-threshold       alpha 低于此值直接清零（默认 16）
    --min-blob              alpha>0 连通块面积低于此像素数则清零（默认 48）
    --no-edge-bleed         关闭边缘去白边
    --white-min             判定白边：min(R,G,B) 高于此值（默认 180）
    --white-spread          判定白边：max(R,G,B)-min(R,G,B) 低于此值（默认 60）
    --white-neighbor-frac   候选白边像素的不透明邻居中白色占比超过此值则视为
                            真实白色部件并保护（默认 0.5）
    --edge-passes           去白边迭代次数（默认 2，第 2 轮处理次外圈残留）
    --erode                 腐蚀边界圈数（默认 0；>0 时最外圈 alpha 清零，
                            之后重跑小连通块过滤）
    --dry-run               只统计不修改文件
"""
import argparse
import glob
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

# 4 邻域连通（对角不相连，避免噪点经对角“搭”到主体上）
CONNECTIVITY = np.array([[0, 1, 0],
                         [1, 1, 1],
                         [0, 1, 0]], dtype=bool)
KERNEL3 = np.ones((3, 3), dtype=int)


def label_and_filter(solid, min_blob):
    """连通域标记 + 小块删除。返回 (kill_mask, stats)。"""
    labels, num = ndimage.label(solid, structure=CONNECTIVITY)
    sizes = ndimage.sum_labels(solid, labels, index=np.arange(1, num + 1)).astype(np.int64)
    keep = sizes >= min_blob
    kill_mask = solid & ~np.isin(labels, np.nonzero(keep)[0] + 1)
    stats = {
        "blobs_total": num,
        "blobs_removed": int(np.count_nonzero(~keep)),
        "blob_pixels_removed": int(sizes[~keep].sum()),
        "largest_removed_blob": int(sizes[~keep].max()) if np.any(~keep) else 0,
        "largest_kept_blob": int(sizes[keep].max()) if np.any(keep) else 0,
        "kept_blobs": int(np.count_nonzero(keep)),
    }
    return kill_mask, stats


def edge_decontaminate(arr, solid, white_min, white_spread, neighbor_frac, passes):
    """边缘去白边：bleed 边界白色残留像素的 RGB（保留 alpha）。

    返回 (修改后的 arr, bleed_mask 累计, 每轮 bleed 数列表)。
    """
    out = arr.copy()
    bleed_total = np.zeros(out.shape[:2], dtype=bool)
    per_pass = []
    for _ in range(passes):
        alpha = out[..., 3]
        rgb = out[..., :3].astype(np.int64)
        solid = alpha > 0
        if not solid.any():
            per_pass.append(0)
            break
        # 边界：不透明且 8 邻域内有透明像素
        eroded = ndimage.binary_erosion(solid, structure=np.ones((3, 3), bool), border_value=0)
        boundary = solid & ~eroded
        mn = rgb.min(-1)
        mx = rgb.max(-1)
        whitish = (mn > white_min) & ((mx - mn) < white_spread)
        cand = boundary & whitish
        if not cand.any():
            per_pass.append(0)
            break
        # 邻居保护：不透明邻居中白色占比高 => 真实白色部件（胸毛/爪子），跳过
        white_op = whitish & solid
        n_white = ndimage.convolve(white_op.astype(int), KERNEL3, mode="constant") - white_op
        n_solid = ndimage.convolve(solid.astype(int), KERNEL3, mode="constant") - solid
        frac = np.zeros(out.shape[:2], dtype=float)
        np.divide(n_white, np.maximum(n_solid, 1), out=frac)
        fringe = cand & (frac <= neighbor_frac)
        if not fringe.any():
            per_pass.append(0)
            break
        # bleed：取 3x3（不够则 5x5）内不透明、非白像素的平均色
        donor = solid & ~whitish
        cnt = ndimage.convolve(donor.astype(int), KERNEL3, mode="constant")
        sums = [ndimage.convolve(np.where(donor, rgb[..., c], 0).astype(np.int64),
                                 KERNEL3, mode="constant") for c in range(3)]
        ok = fringe & (cnt > 0)
        if ok.any():
            for c in range(3):
                out[..., c][ok] = (sums[c][ok] // cnt[ok]).astype(np.uint8)
        # 3x3 无供体的个别像素用 5x5 兜底
        rest = fringe & (cnt == 0)
        if rest.any():
            k5 = np.ones((5, 5), dtype=int)
            cnt5 = ndimage.convolve(donor.astype(int), k5, mode="constant")
            sums5 = [ndimage.convolve(np.where(donor, rgb[..., c], 0).astype(np.int64),
                                      k5, mode="constant") for c in range(3)]
            ok5 = rest & (cnt5 > 0)
            for c in range(3):
                out[..., c][ok5] = (sums5[c][ok5] // cnt5[ok5]).astype(np.uint8)
        n = int(fringe.sum())
        per_pass.append(n)
        bleed_total |= fringe
    return out, bleed_total, per_pass


def process_frame(path, args):
    """处理单帧，返回统计信息 dict。"""
    img = Image.open(path).convert("RGBA")
    arr = np.array(img)
    h, w = arr.shape[:2]
    alpha = arr[..., 3]
    total_pixels = h * w

    # 1) alpha 阈值：低于阈值的半透明残留直接清零
    solid = alpha >= args.alpha_threshold
    thresh_cleared = int(np.count_nonzero((alpha > 0) & ~solid))

    # 2) 连通域标记 + 小连通块删除
    kill_mask, blob_stats = label_and_filter(solid, args.min_blob)

    clear = ((alpha > 0) & ~solid) | kill_mask  # 阈值清除 + 小块清除

    out = arr.copy()
    out[clear] = (0, 0, 0, 0)

    # 3) 边缘去白边（bleed）
    bleed_pixels = 0
    bleed_passes = []
    if not args.no_edge_bleed:
        live = out.copy()
        live[clear, 3] = 0
        live, bleed_mask, bleed_passes = edge_decontaminate(
            live, live[..., 3] > 0,
            args.white_min, args.white_spread, args.white_neighbor_frac,
            args.edge_passes)
        bleed_pixels = int(bleed_mask.sum())
        out[..., :3] = live[..., :3]

    # 4) 可选腐蚀：清掉最外圈边界像素，再跑一次小连通块过滤
    erode_pixels = 0
    erode_blob_stats = None
    if args.erode > 0:
        live_alpha = out[..., 3] > 0
        for _ in range(args.erode):
            eroded = ndimage.binary_erosion(live_alpha, structure=np.ones((3, 3), bool),
                                            border_value=0)
            ring = live_alpha & ~eroded
            erode_pixels += int(ring.sum())
            out[ring] = (0, 0, 0, 0)
            live_alpha = out[..., 3] > 0
        kill2, erode_blob_stats = label_and_filter(live_alpha, args.min_blob)
        out[kill2] = (0, 0, 0, 0)

    pixels_removed = thresh_cleared + blob_stats["blob_pixels_removed"] + erode_pixels
    if erode_blob_stats:
        pixels_removed += erode_blob_stats["blob_pixels_removed"]

    stats = {
        "path": path,
        "size": (w, h),
        "thresh_pixels_cleared": thresh_cleared,
        "pixels_removed": pixels_removed,
        "removed_pct": 100.0 * pixels_removed / total_pixels,
        "bleed_pixels": bleed_pixels,
        "bleed_passes": bleed_passes,
        "erode_pixels": erode_pixels,
    }
    stats.update(blob_stats)

    # 5) 保存（非 dry-run 时覆盖原路径，尺寸不变）
    if not args.dry_run and (pixels_removed > 0 or bleed_pixels > 0):
        Image.fromarray(out, "RGBA").save(path)

    return stats


def main():
    ap = argparse.ArgumentParser(description="清理序列帧抠图残留（噪点 + 边缘白边）")
    ap.add_argument("inputs", nargs="+", help="输入文件（可多个，支持 glob）")
    ap.add_argument("--alpha-threshold", type=int, default=16)
    ap.add_argument("--min-blob", type=int, default=48)
    ap.add_argument("--no-edge-bleed", action="store_true", help="关闭边缘去白边")
    ap.add_argument("--white-min", type=int, default=180)
    ap.add_argument("--white-spread", type=int, default=60)
    ap.add_argument("--white-neighbor-frac", type=float, default=0.5)
    ap.add_argument("--edge-passes", type=int, default=2)
    ap.add_argument("--erode", type=int, default=0)
    ap.add_argument("--dry-run", action="store_true", help="只统计不改图")
    args = ap.parse_args()

    files = []
    for pat in args.inputs:
        matched = sorted(glob.glob(pat))
        files.extend(matched if matched else [pat])

    warn = False
    all_stats = []
    for f in files:
        st = process_frame(f, args)
        all_stats.append(st)
        print(f"{st['path']}:")
        print(f"  连通块总数 {st['blobs_total']}，删除 {st['blobs_removed']} 块 / "
              f"{st['blob_pixels_removed']} px；阈值清除 {st['thresh_pixels_cleared']} px")
        print(f"  去白边 bleed {st['bleed_pixels']} px（各轮 {st['bleed_passes']}）；"
              f"腐蚀 {st['erode_pixels']} px；共删 {st['pixels_removed']} px（{st['removed_pct']:.3f}%）")
        print(f"  最大删除块 {st['largest_removed_blob']} px；最大保留块 {st['largest_kept_blob']} px；"
              f"保留 {st['kept_blobs']} 块")
        if st["removed_pct"] > 5.0:
            print(f"  [警告] 删除像素超过画面 5%（{st['removed_pct']:.2f}%），请人工确认后再正式运行！")
            warn = True
        if st["largest_removed_blob"] > 500:
            print(f"  [警告] 删除了面积 >500 的连通块（{st['largest_removed_blob']} px），可能误伤主体部件！")
            warn = True
        if st["bleed_pixels"] > st["size"][0] * st["size"][1] * 0.01:
            print(f"  [警告] bleed 像素超过画面 1%（{st['bleed_pixels']} px），白边判定可能过宽！")
            warn = True

    if warn:
        print("\n存在警告帧：建议检查参数或素材后再执行非 dry-run 清理。", file=sys.stderr)
        sys.exit(2)
    mode = "DRY-RUN（未修改文件）" if args.dry_run else "已写回文件"
    print(f"\n{mode}，共处理 {len(all_stats)} 帧。")


if __name__ == "__main__":
    main()
