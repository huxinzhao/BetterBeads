"""Targeted source/PNG parity check for the editable content interface (requires Pillow)."""
from pathlib import Path
from PIL import Image
import base64, json, re

root = Path(__file__).resolve().parent.parent
source = (root/'BetterBeads/Data/ReferencePatternArt.g.cs').read_text(encoding='utf-8')
matches = re.findall(r'\["([^|]+)\|([^"\n]+)"\] = new\((\d+),(\d+),new uint\[\]\{([^}]+)\},"([^"]+)"\)', source)
assert len(matches) == 26
expected = set()
for ident, view, w, h, colors, cells in matches:
    name = ident.replace('.', '-') + ('' if view == 'front' else '-' + view) + '.png'
    expected.add(name)
    image = Image.open(root/'BetterBeads/assets/patterns'/name).convert('RGBA')
    assert image.size == (int(w), int(h)), name
    palette = [int(c.removesuffix('u'), 16) for c in colors.split(',')]
    expected_pixels = [palette[i] for i in base64.b64decode(cells)]
    pixels = [(r<<24|g<<16|b<<8|a) if a else 0 for r,g,b,a in image.get_flattened_data()]
    assert all((c&255) in (0,255) for c in pixels), name
    assert pixels == expected_pixels, name
assert expected == {p.name for p in (root/'BetterBeads/assets/patterns').glob('*.png')}
index = json.loads((root/'art/content-index.json').read_text(encoding='utf-8'))
assert {p['File'] for p in index['Assets']} == {p.relative_to(root).as_posix() for p in (root/'BetterBeads/assets').rglob('*.png')}
for row in index['Assets']:
    assert Image.open(root/row['File']).size == (row['Width'], row['Height'])
catalog = json.loads((root/'BetterBeads/i18n/default.json').read_text(encoding='utf-8'))
copy = json.loads((root/'art/text/copy-index.json').read_text(encoding='utf-8'))
assert all(p['Key'] in catalog and p['Text'] == catalog[p['Key']] for p in copy)
print(f'PASS: 26 editable PNGs exactly match built-in pixels; {len(index["Assets"])} assets indexed; {len(copy)} copy uses match catalog.')
