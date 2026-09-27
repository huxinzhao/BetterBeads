# 更好的拼豆 0.16.4 · 美术资源与修改指南

当前统一入口见[美术与文案修改指南](CONTENT_EDITING.md)。运行资源现为42张PNG：下文16张基础资源，加上`assets/patterns`的26张可替换参考方向图。20图纸现在可直接改PNG，无需重编译；文案集中在i18n。以下旧资源路径继续保留。

界面以0.15.4后的固定字号与原生木框规则为准：`UseNativeUi=true`时最外框与豆色格读取游戏素材，内区和普通按钮使用简化绘制；Ui.png并非当前所有控件的唯一皮肤来源。不要按早期说明靠缩小字号塞文字。

沿用现有原创像素美术：拼豆台、普通/精炼豆、成品模板默认图、工具与状态图标、面板和按钮。正式资源替换旧版紫黑占位图。0.15.3增加4×4常用色盒、HSV调色板和带定位柱的圆环拼豆盘。旧成品快照不变；新制造用料规则见FEATURES.md。

## 先看什么，怎么改

| 目的 | 打开的文件 | 操作 |
| --- | --- | --- |
| 看整套资源 | `art/contact-sheet.png` | 工作台、28种豆图标（含未知材质）、家具/武器、帽子四向、24种工具图标 |
| 看界面皮肤 | `art/ui-contact-sheet.png` | 上排：面板/内嵌/强调/普通按钮/悬停；下排：按下/禁用/普通页签/选中页签/背景 |
| 直接画图并保存原PNG | 根目录`打开像素编辑器.cmd` / `art/editor.html` | Edge/Chrome选择素材目录后直接写回，含备份、撤销、填色与图集单格；详见PIXEL_EDITOR.md |
| 保存旧JSON源图 | `art/source-editor.html`的“保存完整源文件” | 旧接口下载后替换`art/source.json`，与直接编辑PNG分开 |
| 旧编辑器导出 | `source-editor.html`的“导出当前图块 PNG”或“导出所属图集 PNG” | 用尺寸一致的PNG替换assets对应文件；新编辑器可直接保存 |
| 修改文字色、图标映射与显示选项 | `BetterBeads/assets/art.json` | 保存并重新载入美术，不需要重新编译 |
| 批量重新导出 | `art/export.mjs` | 在项目根目录执行 `node art/export.mjs`，需要 Node.js；无外部软件包依赖 |
| 使用像素画软件修改 | `BetterBeads/assets/*.png`、`art/palette.gpl` | 支持 PNG 的像素画软件均可；色板为 GIMP Palette 格式 |

**两条编辑路线择一使用。** 直接修改 PNG 的结果不会自动写回 source.json；重新运行导出脚本会用 source.json 覆盖 PNG。编辑页中单颗豆是一个图块，游戏使用的是整张 Beads.png，替换时请导出所属图集。若修改过图集顺序，必须同步 art.json。编辑页导出图集采用随包默认布局。

项目内附带 Node 时，也可使用 `C:/Users/xinzh/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe` 执行脚本；这个路径仅适用于本机，不是模组安装依赖。普通玩家安装 ZIP 不需要 Node。

在游戏的 SMAPI 控制台输入 `betterbeads_reload_art`，下次绘制重新读取贴图与 art.json。也可退出并重新进入游戏。不要同时修改仍在写入中的 PNG。更新模组前备份自己修改过的 assets；更新安装包会替换同名美术文件。

## 视觉规范

整体采用暖木工坊风格：深棕描边、奶油纸色底、铜绿选中态，工具与工作台使用同一组主色。像素簇在原生尺寸绘制，透明背景，避免抗锯齿造成小尺寸边缘变糊。

金属豆保留圆环轮廓，宝石豆采用切角轮廓；普通与精炼同源材质沿用相同主色，精炼版增加底部金色边缘和右上星芒。材料名称、普通/精炼名称及属性说明继续由游戏文字提供，识别不只依赖颜色。

按钮具有普通、悬停、按下/选中、禁用四态；选中页签有底边标记。工具图标与文字并列，空间不足时优先保留完整文字区域。按钮绘制表面在固定点击矩形内产生轻微位移；文字与图标整体居中，常规字号约为游戏小字体的92%，窄按钮按可用宽度适配，超长标签截断。状态图标与原有缺项说明同时显示。

文字不烘焙进贴图，保留中英文翻译与游戏字体。默认配色的计算对比度：普通文字 12.15:1、选中态 10.02:1、禁用文字 5.65:1；这是指定前景/背景色的离线计算，不能代替游戏截图验收。

设计参考与本项目的应用：

- [Lospec：彩色轮廓与对比](https://lospec.com/articles/pixel-art-outlines-part-2-using-color)：使用与物体同色系的深色轮廓，保持背景上的轮廓辨识度。未复制示例图片。
- [Game Accessibility Guidelines：文字和界面背景的高对比](https://gameaccessibilityguidelines.com/provide-high-contrast-between-text-ui-and-background/)：文字区采用干净底色，交互状态使用文字、边框与颜色共同表达。
- [Aseprite：Sprite sheet](https://www.aseprite.org/docs/sprite-sheet/)：资源以固定单元图集组织，位置映射独立保存，方便替换与扩展。
- [Aseprite：Slices](https://www.aseprite.org/docs/slices/)：界面采用切片思路，边角与中心分别绘制，适应不同窗口大小。

以上资料用于学习设计方法，不作为可以复制他人素材的授权。像素资源由项目中的绘制代码和可编辑像素源文件原创生成；封面使用内置 imagegen 生成。

## 运行资源清单

基础图集共 **16张运行 PNG、1份美术配置、82个可编辑图块**；另有26张参考图纸方向PNG，总计42张运行PNG。封面和编辑器仅放在编辑包中，不加载到游戏内。

| 文件（相对 assets） | 尺寸 | 内容与接入状态 |
| --- | --- | --- |
| Board.png | 24×24 | 0.13.0新增拼豆板九宫格木框；公开资源名后缀Board |
| Workbench.png | 16×32 | 地图、背包和手持拼豆台；保留一格占地 |
| Beads.png | 128×64 | 每格16×16，每行8格；27种材料＋1未知材质回退，剩余4格透明 |
| Products/Picture.png | 16×16 | 拼豆画默认模板图 |
| Products/WoodOrnament.png | 16×16 | 木摆件默认模板图 |
| Products/StoneStatue.png | 16×16 | 石雕默认模板图 |
| Products/Sword.png | 16×16 | 剑默认模板图 |
| Products/Dagger.png | 16×16 | 匕首默认模板图 |
| Products/Hammer.png | 16×16 | 锤默认模板图 |
| Products/Hat.png | 20×80 | 帽子四向默认图；当前项目接口顺序为正/右/左/后。已接入正式帽子制造及四向编辑 |
| Ui.png | 240×24 | 10个24×24九宫格；切角8像素。主框、按钮、页签已接入；内嵌/强调等样式供扩展使用 |
| Icons.png | 192×32 | 24个16×16工具/功能图标；工具、材料、保存、熨烫及页签按空间显示 |
| Status.png | 96×12 | 8个12×12状态图标；编辑页底栏状态已接入，精炼独立状态图标供扩展使用 |
| BeadMask.png | 8×8 | 历史豆孔遮罩接口保留；新版圆环豆粒由代码绘制 |
| ScrollTrack.png | 8×8 | 滚动轨道资源预留；当前分页/滚轮逻辑沿用原实现 |
| ScrollThumb.png | 8×16 | 滚动滑块资源预留；尚未增加拖动滚动条功能 |

模板默认图只用于无作品快照时的模板纹理。有效成品继续读取自身快照，不会把玩家自创武器替换成这里画的示例剑。调试命令生成的旧色块样例也保留为测试图案。

## 可修改配置

`assets/art.json` 是纯美术接口，Schema 当前为1；数值平衡仍在 processing.json、config.json 和对应规则中。

| 字段 | 默认值/含义 | 修改约束 |
| --- | --- | --- |
| Schema | 1 | 未支持的版本使用安全的界面默认值 |
| BeadColumns | 8 | 1～128；按每格16像素组织，列数必须与图集相符 |
| Beads | 材料ID → 图块序号 | 序号从0开始，先左后右、再向下；新增材质可新增映射；缺失映射使用unknown图块 |
| Ui | 样式名 → 图块序号 | 每格24×24；当前 panel/inset/accent/button-normal/button-hover/button-pressed/button-disabled/tab-normal/tab-selected/background |
| Icons | 功能名 → 图块序号 | 每格16×16；键对应源文件的icon分组 |
| Status | 状态名 → 图块序号 | 每格12×12；键对应源文件的status分组 |
| Ink | 392D32 | 六位RGB十六进制，不带#；主要文字 |
| MutedInk | 55493D | 禁用文字 |
| Canvas | 4D5158 | 画布外区域 |
| CheckerLight / CheckerDark | E5DFD0 / CFC8B8 | 透明格棋盘背景；不改变作品色卡 |
| ShowIcons | true | false时隐藏附加工具和状态图标，保留文字 |
| ShowBeadHoles | true | 默认在单元至少5像素时绘制带阴影的圆环豆粒，支持边缘裁切；false时盘面回退实心像素，色盒仍显示彩色豆粒。成品/导出像素不变 |

自定义图集样式在Ui.png/source.json中修改；默认启用的原生外框不由这张图决定，普通按钮与浅凹槽另有代码绘制。Ink等配置只负责文字与画布颜色，不会把图集自动重新染色。不要删掉仍在使用的映射。

PNG保持规定尺寸和透明通道。界面及图标的越界映射有回退；工作台和原生模板图片直接由游戏内容系统读取，错误文件或尺寸仍可能导致加载/裁切错误，请用检查脚本核对后替换。

## 二次开发接口

所有公开资源名以 `Mods/xinzh.BetterBeads/` 开头：

| 资源名后缀 | 内容 |
| --- | --- |
| Art | art.json；可通过内容系统编辑其字段 |
| Workbench | 拼豆台PNG |
| SourceBeads | 静态源图集Beads.png；推荐改这里 |
| Beads | 按当前材料目录重新组装的运行图集；不要依赖其材料位置顺序 |
| Ui / Icons / Status / BeadMask / ScrollTrack / ScrollThumb | 同名PNG |
| PicturePlaceholder / WoodOrnamentPlaceholder / StoneStatuePlaceholder | 三种家具默认图；保留旧名称兼容既有模板 |
| SwordPlaceholder / DaggerPlaceholder / HammerPlaceholder / HatPlaceholder | 对应武器/帽子默认图；名称中Placeholder是历史接口名，内容已经换为正式像素图 |

资源以低优先级注册，允许美术内容包覆盖；修改 SourceBeads 或 Art 时会使生成的 Beads 缓存失效。内容包需要自己声明对应依赖和资源路径，本模组不自动安装其他模组。

`Data/ArtDefinition.cs` 负责美术清单校验与按材料ID定位；`Runtime/ArtResources.cs` 负责资源注册、九宫格、按钮状态和图标绘制。拓展布局时继续使用 `Data/EditorLayout.cs` 及其他Layout类的矩形作为绘制和点击的共同接口，不在贴图内固定整屏坐标。

`art/source.json` 的每个图块包含 width、height、palette 和 pixels。pixels每行一个字符串，每个字符索引本图块palette中的RGBA颜色；6位颜色为不透明RGB，8位含Alpha，00000000为透明。`source-data.js` 是导出脚本自动生成的编辑页数据副本，不要只改这个副本。

`node art/export.mjs --reset-sprite=product/Dagger` 可以把指定源图块恢复为程序内置默认图，并写回 source.json；这是显式重置操作，正常导出不要添加该参数。正常导出不重建已有source.json，也不覆盖已有art.json。

## 验证与尚待验收

- 505项数据检查通过，包括27材质映射完整性、顺序独立、未知材质、图集越界、整数溢出及坏配置回退。
- 16张PNG的尺寸/透明通道、28个豆孔、11个精炼标记、编辑页脚本语法/元素绑定/源文件一致性检查通过。
- 资源总览和界面样式图已在离线图像预览中检查。
- Release编译、原生接口与安装包一致性检查的结果记录在根目录PROGRESS.md。
- 未启动游戏；需要本机验收地图工作台、各菜单缩放、悬停/按下、背包图标、替换资源刷新及旧作品保存重进。
- 浏览器工具的安全策略拦截file页面，编辑页未完成浏览器内点击验收；已完成静态与资源数据检查。双击本地editor.html即可自行检查，不需要部署网站。
- 长袖、新版型和多方向功能家具需随对应功能扩展资源。


## 动态服装与家具资源

运行图集仍为16张PNG、82个可编辑图块；新服装和椅桌外观按玩家图案生成，不需要手绘每件成品。新增 `assets/clothing.json` 为服装预算配置，不属于纯外观设置。模板布局、试穿、裤型遮罩和家具前景见[当前功能说明](FEATURES.md)。资源包为 `dist/BetterBeads-Art-0.15.4.zip`。

## 0.15.3 拼豆盘与配色预览

盘面默认隐藏像素网格，显示定位柱、圆环豆粒和半透明待放豆。圆环纹理由ArtResources生成并缓存；修改旧BeadMask.png不会替换该纹理。4×4常用色盒的16个颜色由玩家在HSV调色板中设置并随存档保存，不由美术图集限制。

art/layout-preview.png、art/layout-compact.png和art/palette-preview.png、art/palette-compact.png为按布局数据生成的离线示意，不是游戏截图。

0.15.3将选色窗口统一命名为「拼豆染色」，限制大窗口面板尺寸并居中，增加染色前后豆粒对照、色区边框与铜绿色豆格选中状态。右侧常用色以圆环豆粒展示，点击选择后可拖动色相和明暗区修改。

## 0.15.2 排版与按钮反馈

主界面缩短页签和工具按钮，侧栏使用内容高度并与盘面居中；统一内边距与文字垂直对齐。拼豆染色的大窗口采用28像素外边距与单排操作按钮，小窗口保留紧凑两排。按钮阴影、底座和高光由ArtResources绘制，不改贴图源文件。悬停约0.13秒完成90%的过渡，按下最大位移4像素；动画按经过时间计算，松开后复位，点击区域不移动。

## 0.15.3 原生星露谷边框

assets/art.json新增UseNativeUi，默认true：主框、弹窗、内嵌框与按钮使用Game1.menuTexture中的原生菜单边框（0,256,60,60）及drawTextureBox接口；常用豆格读取原生背包第10格。物品和拼豆盘图案仍使用项目美术，原Ui.png继续保留；设为false恢复该自定义皮肤。原生贴图仅在运行时读取，不复制到安装包。

主要按钮提高到44～48像素并限制横向长度，正文留出6～8像素上下空间，再根据字体实际高度缩放。侧栏标题和操作区的内边距同步增加。

离线预览可先执行checks/export-native-ui.ps1，只读本机MenuTiles.xnb并将参考数据写入.tools/ui-reference，再运行art/preview-layout.mjs；没有参考数据时退回旧皮肤示意。该目录不进入两个发布包。布局PNG是离线示意，不是游戏截图。

## 0.15.4 双栏、紧凑侧栏与染色双页

本节覆盖前述旧版排版：主框围绕内容定尺寸，最大高度800；右栏256，栏距24，常规外边距24，控件间距8。正文和按钮采用20／18两档实际字体高度，普通44、主操作48、紧凑40高；不随控件宽度缩小字。仅最外层框与主操作强调原生边框，内区改浅凹槽、页签改下划线，豆色才使用背包物品格。旧Ui图集继续为非原生外框和主按钮提供回退，内区与普通按钮由代码绘制。

小屏通过豆色／材料覆盖侧栏选取，染色分豆色盒／染色双页；切页保留草稿，完整颜色值在右侧预览分行显示。悬停约120毫秒，按下最多3像素，命中矩形不动。

8张离线示意：layout-preview、layout-compact、sidebar-compact、palette-preview、palette-compact、palette-dye-compact、materials-preview、clothing-preview（均为art目录PNG）。前三组布局取自C#导出的JSON，字体使用微软雅黑近似，豆粒为示意；不能代替游戏内字体、动效和交互验证。运行包不含这些示意或原生游戏贴图。

## 0.16.1 图纸预览

新增art/pattern-detail-wide.png（1280×900）、pattern-detail-compact.png（480×420）与reference-patterns.png（20图纸正面总览），均已标明离线预览、非游戏截图。源数据由checks的`--workshop-preview`导出到art/workshop-layouts.json，使用art/preview-workshop.mjs绘制。窗口尺寸来自PatternDetailLayout，图案来自ReferencePatterns；近似字体不能代替实机检查。少量操作时菜单随内容收紧。

0.16.1 图纸全部按成品尺寸重绘，详情页提供可切换方向、大图、来源和预计用料。20张图纸的逐项来源、作者、CC0授权与修改记录见[图纸来源](../art/PATTERN_SOURCES.md)。已保存的旧图纸和成品快照不会被新图案替换。

## 0.16.2 精细图纸

八张挂画改为原生32×32精细构图，使用新增独立模板，不放大或覆盖旧16×16图纸。其余成品沿用原生网格，重新绘制层次与轮廓。详情页默认显示成品图案，可切换浅色底盘的拼豆视图。art/pattern-highlights.png展示六张细节，reference-patterns.png为20张总览；均为离线预览，非游戏截图。实际CC0素材和逐图改动见art/PATTERN_SOURCES.md。
