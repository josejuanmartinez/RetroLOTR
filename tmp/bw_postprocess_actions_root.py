from pathlib import Path
from PIL import Image, ImageOps
import numpy as np


def to_pure_bw(
    img: Image.Image,
    contrast_boost: float = 1.35,
    threshold: int = -1,
    dither: bool = False,
) -> Image.Image:
    g = img.convert("L")
    g = ImageOps.autocontrast(g)
    if contrast_boost and contrast_boost != 1.0:
        lut = [int(max(0, min(255, 128 + (i - 128) * contrast_boost))) for i in range(256)]
        g = g.point(lut, mode="L")

    if threshold is None or int(threshold) < 0:
        hist = np.array(g.histogram(), dtype=np.float64)
        total = g.width * g.height
        sum_total = np.dot(np.arange(256), hist)
        sum_b = 0.0
        w_b = 0.0
        max_between = -1.0
        level = 128
        for t in range(256):
            w_b += hist[t]
            if w_b == 0:
                continue
            w_f = total - w_b
            if w_f == 0:
                break
            sum_b += t * hist[t]
            m_b = sum_b / w_b
            m_f = (sum_total - sum_b) / w_f
            between = w_b * w_f * (m_b - m_f) ** 2
            if between > max_between:
                max_between = between
                level = t
        th = level
    else:
        th = int(threshold)

    if dither:
        bw1 = g.convert("1", dither=Image.FLOYDSTEINBERG)
        bw = bw1.convert("L").point(lambda p: 255 if p > 0 else 0, mode="L")
    else:
        bw = g.point(lambda p: 255 if p > th else 0, mode="L")

    return bw


def main() -> None:
    repo = Path.cwd()
    actions_dir = repo / "Assets" / "Art" / "Cards" / "Actions"
    addressables_path = repo / "Assets" / "AddressableAssetsData" / "AssetGroups" / "Default Local Group.asset"

    files = sorted(
        p for p in actions_dir.iterdir()
        if p.is_file() and p.suffix.lower() in {".png", ".jpg", ".jpeg"}
    )

    addressables = addressables_path.read_text(encoding="utf-8")
    conversions = []

    for src in files:
        with Image.open(src) as img:
            bw = to_pure_bw(img, contrast_boost=1.35, threshold=-1, dither=False)

        dst = src if src.suffix.lower() == ".png" else src.with_suffix(".png")
        bw.save(dst, format="PNG")
        conversions.append((src, dst))

        if dst != src:
            meta_src = Path(str(src) + ".meta")
            meta_dst = Path(str(dst) + ".meta")
            if meta_dst.exists():
                meta_dst.unlink()
            meta_src.rename(meta_dst)
            src.unlink()

            old_rel = src.as_posix().replace(repo.as_posix() + "/", "")
            new_rel = dst.as_posix().replace(repo.as_posix() + "/", "")
            addressables = addressables.replace(old_rel, new_rel)

    addressables_path.write_text(addressables, encoding="utf-8")

    png_in_place = sum(1 for src, dst in conversions if src == dst)
    renamed = sum(1 for src, dst in conversions if src != dst)
    print(f"processed={len(conversions)} png_in_place={png_in_place} renamed_to_png={renamed}")
    for src, dst in conversions:
        if src != dst:
            print(f"RENAMED {src.name} -> {dst.name}")


if __name__ == "__main__":
    main()
