# 0.16.2 图纸来源与改动

本版纠正了 0.16.1 仅以素材题材作为参考的做法：四张图纸实际使用 josehzz 的 CC0 像素素材，保留其叶片和花瓣轮廓再调色、重新构图；其余为 BetterBeads 逐格绘制。运行时读取 `ReferencePatternArt.g.cs` 中的原生网格，不放大旧图来增加格数。帽子20×20四向、上衣8×8四向、裤子16×16、武器16×16、椅子16×32、茶桌32×32，八张精细挂画32×32。

## 实际改编素材

[josehzz — Farming crops 16x16](https://opengameart.org/content/farming-crops-16x16)，页面与下载包均明确采用 [CC0](https://creativecommons.org/publicdomain/zero/1.0/)。原文件保存在 `references/crops/Crop_Spritesheet.png`，原作者说明保存在同目录 `README.txt`。以下列行坐标均从0开始，每格16×16，未缩放。

| 图纸 ID | 素材位置 | 改动 |
| --- | --- | --- |
| starter.sprout | 第1列、第7行，土豆植株 | 提亮色阶；新增32格土层、根茎、土豆、小铲和幼苗 |
| starter.heart | 第0列、第6行，草莓肖像 | 以两个原尺寸草莓组合，新增木牌、挂环、铜钉和缎带 |
| spring.flower | 第6列、第0行与第1行，玫瑰和郁金香 | 重新组合花束，新增雏菊、釉面花器、把手与蝶形点缀 |
| spring.pot | 第2列、第2行，番茄生长阶段 | 保留叶簇，改为有明暗的陶盆，保持16格 |

## 逐格原创绘制

| 图纸 ID | 原生尺寸 | 本轮细节 |
| --- | --- | --- |
| robin.house | 32×32 | 屋瓦拼接、烟囱烟气、暖窗、木梁、花丛和石阶 |
| robin.chair | 16×32 | 弧形木框、镂空靠背、绿垫、承重横梁及攀花 |
| emily.hat | 20×20×4向 | 编织层次、帽檐、向日葵、绿色帽带及背面蝴蝶结 |
| emily.shirt | 8×8×4向 | 奶油衣衬、叶绿围裙、背带与侧面缝线 |
| clint.dagger | 16×16 | 斜向铜叶刃、亮刃、叶形护手与缠柄 |
| clint.sword | 16×16 | 加宽钢刃、金色斜护手、红宝石及握柄 |
| refine.hammer | 16×16 | 三组晶簇切面、钢箍和斜向皮革握柄 |
| refine.sign | 32×32 | 砖砌炉膛、三层火光、铁砧、夹钳和木框 |
| marlon.crest | 32×32 | 双层金边、宝蓝盾面、银星及月桂枝 |
| marlon.sword | 16×16 | 弯曲星刃、冰蓝切面、宝石和翼形护手 |
| summer.shell | 32×32 | 放射状贝壳纹、粉白色阶、潮水和海星 |
| summer.table | 32×32 | 桌面透视、蓝布、茶壶、两套杯碟及小花瓶 |
| autumn.pumpkin | 16×16 | 弧形瓜瓣、转折阴影、亮面和藤叶 |
| autumn.pants | 16×16 | 牛皮色面料、腰带扣、两种补丁及裤脚缝线 |
| winter.star | 32×32 | 金属提环、雪帽、黄铜灯框、星光和松枝 |
| winter.snow | 16×16 | 蓝帽、雪身冷暖阴影、围巾、树枝手和纽扣 |

## 轮廓研究参考（未直接贴入成品）

- [Bennyboi_hack — 16x16 weapon sprites free，CC0](https://opengameart.org/content/16x16-weapon-sprites-free)：参考斜向武器构图。文件 `references/weaponpack.png`。
- [.bee — Tables and Misc Props，CC0](https://opengameart.org/content/tables-and-misc-props-16x16)：参考家具透视。文件 `references/tables.png`。

旧图纸ID、奖励和解锁条件保留；已有玩家图纸与成品快照保留旧像素。重新复制八张挂画会得到新的32格独立图纸。实际用料以图案豆数为准，并在详情/熨烫前显示。
