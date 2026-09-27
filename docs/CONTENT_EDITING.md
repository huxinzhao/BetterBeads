# 美术与文案修改入口 · 0.16.4

修改游戏内容从下表开始。已有路径和ID继续兼容；本轮没有重绘作品、重命名内部图纸ID或改动玩法数值。

现可双击根目录`打开像素编辑器.cmd`，选择项目后直接画图并保存原PNG；使用说明见[像素编辑器](PIXEL_EDITOR.md)。旧JSON图块编辑器改为`art/source-editor.html`保留。

| 要改什么 | 修改入口 | 生效方式 |
| --- | --- | --- |
| 工作台、豆子、工具图标 | `BetterBeads/assets/*.png`；也可编辑`art/source.json`再导出 | SMAPI控制台`betterbeads_reload_art`或重启游戏 |
| 界面字色、皮肤开关、图集映射 | `BetterBeads/assets/art.json` | 同上；字段见ART_GUIDE |
| 20张参考图纸，共26张方向PNG | `BetterBeads/assets/patterns/*.png` | `betterbeads_reload_patterns`，关闭后重新打开参考图纸；或重启游戏 |
| 中文名称、按钮、故事、委托、提示 | `BetterBeads/i18n/zh.json` | 重启游戏；默认语言/中文回退同时维护`default.json` |
| 查看所有文案与定位键 | `art/text/catalog.csv` | 审阅索引，**不是游戏输入**；从Key找到i18n中的同名键再改 |
| 查看新增文案的源码位置 | `art/text/copy-index.json` | 开发定位索引，不直接影响游戏 |
| 查看全部运行PNG、尺寸、公开资源名 | `art/content-index.json` | 资源清单，不是配置文件 |
| 参考图纸来源与授权 | `art/PATTERN_SOURCES.md` | 换素材时同步记录作者、许可和改动 |

运行安装包用于游戏；同版本`BetterBeads-Art`包用于编辑。编辑包包含美术源、运行PNG、两份i18n、索引和本指南，不包含运行DLL。不要将整个编辑包放进Mods。

## 参考图纸：直接改PNG，不用编译

文件名是稳定接口。例如：

- `starter-sprout.png`：土豆苗小花园，32×32。
- `robin-chair.png`：花园木椅，16×32。
- `summer-table.png`：海风茶桌，32×32。
- `emily-hat.png`、`emily-hat-right.png`、`emily-hat-left.png`、`emily-hat-back.png`：帽子四向，各20×20。
- `emily-shirt.png`及同样三个方向后缀：上衣四向，各8×8。
- 其余尺寸逐张列在`content-index.json`。文件名不要跟随显示名称修改。

每个像素是一颗豆；透明像素是不放豆。保持原生宽高，不缩放或抗锯齿；透明度仅用0或255。每个方向至少有一颗不透明豆。PNG必须能被游戏读取，推荐RGBA格式。

错误尺寸、缺少方向、损坏文件、全透明或半透明像素，会让该张图纸整套回退内置美术，并在SMAPI日志提示；不会覆盖你的PNG。其他正常图纸继续加载。改成不满足任务最低豆数的图案仍可能无法交付，这是任务规则，不是图片加载失败。

替换只影响重新打开/复制的参考图纸。已复制保存的图纸、当前编辑草稿和已有成品保持自己的内容。想应用新图案请重新复制；不会替玩家批量改作品。

公开资源接口为`Mods/xinzh.BetterBeads/Patterns/<文件名去掉.png>`，例如`Mods/xinzh.BetterBeads/Patterns/emily-hat-back`。内容系统可以编辑这些资源；修改后执行重新加载命令，使新的参考目录读取变更。没有自动监听磁盘文件。

## 美术源与运行图：选择一条编辑路线

**直接修改路线**：编辑`BetterBeads/assets`中的PNG即可。不要随后无意运行源图导出覆盖它。

**维护源图路线**：

1. 要维护旧图集的JSON源，使用`art/source-editor.html`，保留`art/source.json`后执行`node art/export.mjs`导出；此脚本不导出参考图纸。新`editor.html`走直接修改运行PNG路线。
2. 参考图纸源在`art/patterns/*.png`；直接修改这些PNG后，执行`python art/content-tools.py --publish-patterns`明确发布到`BetterBeads/assets/patterns`。
3. `draw_patterns.py`是整套图纸的绘制生成器，会覆盖`art/patterns`中的PNG及内置C#回退数据。已经手绘改过PNG时不要直接运行它；需要长期保留生成能力时同步修改绘制逻辑。

`python art/content-tools.py`不带参数只重建清单与文案CSV，不会覆盖PNG。脚本只使用Python标准库。索引可以按需重建，不需要每次改字都运行。

封面、总览、离线界面示意在`art/`，不参与游戏绘制。修改这些展示图不会修改游戏中的作品。预览继续标注“非游戏截图”。

## 文案：稳定Key，只改值

目前收录539个文案键；本轮将209处源码表达式接入i18n，包含图纸名称、故事标题/说明、信件、季节用途、奖励反馈和部分原来硬编码的按钮。原来的语义键保持不变；新键以`copy.<模块>.<稳定编号>`组织。用CSV按中文内容搜索，再复制Key定位JSON。

例如修改显示名称：

```json
"copy.ReferencePatterns.8124f7e843": "我的土豆小花园"
```

这只改变新参考名称，不改变`starter.sprout`这个图纸ID，也不改玩家保存的作品名称。

两种占位符不要混用：

| 格式 | 示例 | 修改规则 |
| --- | --- | --- |
| 既有SMAPI命名占位符 | `{{count}}`、`{{name}}` | 保留名称和双括号 |
| 新copy键的复合占位符 | `{0}`、`{1}`、`{0:0.##}` | 保留编号和格式；可调整在句中的位置；编号按原表达式参数排列 |

例如`copy.WorkshopGuidance.7eea797f56`中的`{0}`是整件服装预算，不要把它改成固定24，否则定制预算与提示会不一致。真实数量、费用和奖励由代码传入，改字不改玩法数值。无效复合格式会回退内置句子并记录一次警告；缺少新键也有内置回退。

信件中的`^^`与`[#]`、原生任务定义中的`/`是游戏控制标记，不是普通标点，须保留。JSON中的引号、反斜杠、换行需要按JSON转义。不要把引导句写成不存在的玩法承诺；按钮尽量短，说明放到正文。

中文游戏修改`zh.json`。保持`default.json`键集同步，便于中文缺项或其他语言回退。这里不是新增英文翻译，default目前仍是中文。标题和图纸元数据在启动时建立，当前没有文案热重载命令；统一重启后查看。已保存的当期委托标题保留到下次刷新，不改写历史进度。

## 保留的开发接口和边界

- `ArtResources`与原`Mods/xinzh.BetterBeads/*`资源名保留；`UseNativeUi=true`时外框和豆格使用游戏原生素材，单改Ui.png不会替换原生外框。
- `ReferencePatternOverrides`只接收图像网格；模板、尺寸、奖励位置、解锁条件、材料选择仍由原代码决定。PNG不是新增物品或改奖励的接口。
- `ReferencePatternArt.g.cs`保留作内置回退，通常不直接编辑。纯代码检查没有加载SMAPI图片时仍可使用内置图案。
- `ContentText`只处理展示文字，内置回退仍在源码。文案键不是存档ID。开发者增加硬编码文案时可从仓库根目录运行`dotnet run --project tools/ExtractCopy/ExtractCopy.csproj -c Release`抽取，再执行内容清单脚本；已有键和值不会被抽取器覆盖。
- 美术和文案修改不需要改存档格式，不提供自动修改存档、Mods安装或图纸解锁操作。

更新模组前保留自己的`assets`与i18n修改副本；新包会替换同名文件。优先按稳定文件名/Key合并，避免用旧JSON整份覆盖新增键。

## 本轮验证范围

39项定向检查覆盖文案回退/格式、接入实际JSON后的玩法提示、像素替换、异常图片数据和已有设计隔离；另核对26张PNG与内置像素一致、资源清单和包内容。游戏内图片热重载与中文显示仍未实机验证；未启动游戏或修改Mods与存档。

可视化文案入口：根目录打开文案编辑器.cmd。详细操作见[文案编辑器](TEXT_EDITOR.md)，保存直接更新当前i18n JSON。

