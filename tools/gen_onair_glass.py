"""Generate On Air glass textures as power-of-two (64x32) to avoid atlas zebra striping."""
from PIL import Image, ImageDraw, ImageFont
import os

# Power-of-two required by VS texture atlas / mipmaps. Face is ~2.25:1; 64x32 is 2:1 (close).
W, H = 64, 32


def draw_glass_bg(img, tint_rgb, alpha):
    px = img.load()
    for y in range(H):
        for x in range(W):
            n = ((x * 3 + y * 5) & 7) - 3
            r = max(0, min(255, tint_rgb[0] + n))
            g = max(0, min(255, tint_rgb[1] + n))
            b = max(0, min(255, tint_rgb[2] + n))
            px[x, y] = (r, g, b, alpha)
    for x in range(3, W - 3):
        r, g, b, a = px[x, 2]
        px[x, 2] = (min(255, r + 40), min(255, g + 40), min(255, b + 40), min(255, a + 20))
        r, g, b, a = px[x, H - 3]
        px[x, H - 3] = (max(0, r - 30), max(0, g - 30), max(0, b - 30), a)


def make_glass(path, tint_rgb, text_rgb, glow=False):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw_glass_bg(img, tint_rgb, 100 if not glow else 160)
    draw = ImageDraw.Draw(img)

    font = ImageFont.load_default()
    for fp in (
        r"C:\Windows\Fonts\lucon.ttf",
        r"C:\Windows\Fonts\consola.ttf",
        r"C:\Windows\Fonts\arialbd.ttf",
    ):
        if os.path.exists(fp):
            font = ImageFont.truetype(fp, 9)
            break

    text = "ON AIR"
    bbox = draw.textbbox((0, 0), text, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    pad_x, pad_y = 8, 7
    x = (W - tw) // 2 - bbox[0]
    y = (H - th) // 2 - bbox[1]
    x = max(pad_x, min(x, W - tw - pad_x))
    y = max(pad_y, min(y, H - th - pad_y))

    if glow:
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            draw.text((x + dx, y + dy), text, fill=(255, 70, 70, 100), font=font)
    draw.text((x, y), text, fill=text_rgb + (240,), font=font)
    img.save(path)
    print(f"{path} {img.size} text={tw}x{th} at=({x},{y})")


base = os.path.normpath(
    os.path.join(os.path.dirname(__file__), "..", "assets", "rpvoicechat", "textures", "block", "radio")
)
make_glass(os.path.join(base, "onairglass.png"), (180, 200, 220), (30, 40, 60), False)
make_glass(os.path.join(base, "onairglass-on.png"), (195, 35, 35), (255, 245, 245), True)
