"""Content inventory and explicit PNG publishing. Python standard library only.
Usage: python art/content-tools.py [--publish-patterns]
Never runs the art generators and never modifies saves or installed Mods.
"""
from pathlib import Path
import csv, json, re, shutil, struct, sys

root = Path(__file__).resolve().parent.parent
assets = root / 'BetterBeads/assets'
source = root / 'art/patterns'
catalog = json.loads((root / 'BetterBeads/i18n/default.json').read_text(encoding='utf-8-sig'))
uses = json.loads((root / 'art/text/copy-index.json').read_text(encoding='utf-8'))
by_key = {}
for row in uses:
    by_key.setdefault(row['Key'], []).append(row['File'] + ':' + str(row['Line']))

def size(path):
    data = path.read_bytes()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError(f'Not PNG: {path}')
    return struct.unpack('>II', data[16:24])

if '--publish-patterns' in sys.argv:
    # Only the shipped names; accidental extra files cannot silently become new templates.
    runtime = list((assets / 'patterns').glob('*.png'))
    if len(runtime) != 26:
        raise ValueError('Expected 26 runtime pattern views')
    for target in runtime:
        original = source / target.name
        if size(original) != size(target):
            raise ValueError(f'Dimensions changed: {original.name}')
    for target in runtime:
        shutil.copy2(source / target.name, target)
    print('Published 26 pattern views from art/patterns to assets/patterns.')

rows = []
for path in sorted(assets.rglob('*.png')):
    relative = path.relative_to(assets).as_posix()
    if relative.startswith('patterns/'):
        role, edit, asset = '可替换参考图纸', '修改本PNG或从art/patterns显式发布', 'Patterns/' + path.stem
    elif relative in ('ScrollTrack.png', 'ScrollThumb.png', 'BeadMask.png'):
        role, edit, asset = '历史/预留接口', '当前主要流程不使用，不表示缺失资源', path.stem
    elif relative == 'Ui.png':
        role, edit, asset = '自定义皮肤/回退图集', '原生外框优先；UseNativeUi=false时才使用对应自定义外框', path.stem
    elif relative.startswith('Products/'):
        role, edit, asset = '成品默认模板，非玩家快照', 'art/source.json或本PNG', path.stem + 'Placeholder'
    else:
        role, edit, asset = '游戏运行资源', 'art/source.json或本PNG', 'SourceBeads' if path.stem == 'Beads' else path.stem
    w, h = size(path)
    rows.append({'File': 'BetterBeads/assets/' + relative, 'Width': w, 'Height': h, 'Role': role, 'Edit': edit,
                 'Asset': 'Mods/xinzh.BetterBeads/' + asset})
(root / 'art/content-index.json').write_text(json.dumps({'Schema': 1, 'Assets': rows}, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
# Classic script works offline from file:// without a module loader or local server.
names = {}
reference_code = root/'BetterBeads/Data/ReferencePatterns.cs'
if reference_code.exists():
    for ident, key in re.findall(r'new\("([^"]+)",ContentText.Get\("([^"]+)"', reference_code.read_text(encoding='utf-8')):
        names[ident.replace('.', '-')] = catalog[key]
old_editor_index = root/'art/pixel-assets.js'
previous = {}
if old_editor_index.exists():
    previous = {p['path']: p['name'] for p in json.loads(old_editor_index.read_text(encoding='utf-8').split('=',1)[1].strip().removesuffix(';'))}
base_names = {'PaintingFrame':'挂画木框（8×8边框素材）','Pipette':'吸管图标（24×24，可直接修改）','Board':'拼豆盘边框','Workbench':'拼豆台','Beads':'普通与精炼豆图集','Ui':'界面皮肤图集','Icons':'工具图标图集','Status':'状态图标图集',
              'BeadMask':'豆孔遮罩（预留）','ScrollTrack':'滚动轨道（预留）','ScrollThumb':'滚动滑块（预留）','Picture':'默认拼豆画','WoodOrnament':'默认摆件','StoneStatue':'默认雕像','Sword':'默认剑','Dagger':'默认匕首','Hammer':'默认重锤','Hat':'默认帽子四向'}
editor_assets = []
for row in rows:
    path = row['File'].removeprefix('BetterBeads/assets/')
    stem = Path(path).stem
    if path.startswith('patterns/'):
        direction = next((v for v in ['right','left','back'] if stem.endswith('-'+v)), None)
        ident = stem[:-(len(direction)+1)] if direction else stem
        label = names.get(ident)
        name = label+(' · '+{'right':'右侧','left':'左侧','back':'背面'}[direction] if direction else '') if label else previous.get(path,stem)
    else:
        name = base_names.get(stem,stem)
    tile, mapping = {'Beads.png':(16,'Beads'),'Icons.png':(16,'Icons'),'Ui.png':(24,'Ui'),'Status.png':(12,'Status'),'Products/Hat.png':(20,None)}.get(path,(None,None))
    editor_assets.append({'path':path,'name':name,'width':row['Width'],'height':row['Height'],'tile':tile,'map':mapping})
old_editor_index.write_text('window.BEAD_EDITOR_ASSETS='+json.dumps(editor_assets,ensure_ascii=False,indent=2)+';\n',encoding='utf-8')
(root/'art/text-data.js').write_text('window.BEAD_COPY_CATALOG='+json.dumps(catalog,ensure_ascii=False,indent=2)+';\n',encoding='utf-8')
with (root / 'art/text/catalog.csv').open('w', encoding='utf-8-sig', newline='') as f:
    writer = csv.writer(f)
    writer.writerow(['Key', 'DefaultText', 'Format', 'SourceLocations'])
    for key, value in catalog.items():
        writer.writerow([key, value, '复合占位符 {0}/{1}' if key.startswith('copy.') and re.search(r'\{\d', value)
                         else 'SMAPI占位符 {{name}}' if '{{' in value else '普通文本', ' | '.join(by_key.get(key, []))])
print(f'Indexed {len(rows)} PNGs and {len(catalog)} text keys. CSV/index files are review aids, not runtime inputs.')
