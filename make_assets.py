import os
import sys
from PIL import Image, ImageDraw

root = os.path.abspath(
    sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__))
)
brand = os.path.join(root, "brand")
assets = os.path.join(root, "assets")
os.makedirs(assets, exist_ok=True)

src = Image.open(os.path.join(brand, "eye-ninja.png")).convert("RGBA")
side = min(src.width, src.height)
left = (src.width - side) // 2
top = (src.height - side) // 2
img = src.crop((left, top, left + side, top + side)).resize((512, 512), Image.LANCZOS)

master = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
mask = Image.new("L", (512, 512), 0)
ImageDraw.Draw(mask).ellipse((0, 0, 512, 512), fill=255)
inner = Image.new("RGBA", (512, 512), (10, 10, 12, 255))
inner.paste(img, (0, 0), mask)
master.alpha_composite(inner)
d = ImageDraw.Draw(master)
d.ellipse((2, 2, 509, 509), outline=(212, 175, 55, 255), width=18)

master.save(os.path.join(assets, "profile.png"), "PNG")
master.save(
    os.path.join(assets, "profile.ico"),
    sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
)
print("assets ok")
