# 技术说明与兼容边界

当前代码基线0.15.4。玩法见FEATURES.md，任务接手见START_HERE.md。

## 数据和兼容

Data为不引用游戏的纯数据／规则，Runtime负责SMAPI、游戏实例和绘制。UniqueID为xinzh.BetterBeads，物品稳定ID、旧快照schema1及历史公开资源键不变。普通豆沿用decoration物品ID；wood、hardwood、stone等旧普通材质物品仍注册，MaterialRules.Canonical映射至decoration计费。

SaveProgress新增FavoriteColors，缺字段初始化16色，读取时修补长度并规范为不透明RGBA。旧UnlockedColors、ProcessedSources保留用于兼容，但不再决定玩法。旧配方Data条目和已学条目按精确模组前缀移除，无全局制作计数重置。processing.json原料产量和属性保留，历史武器／效果／精炼配置迁移保留。

快照存最终ActualMaterials、FinalStats和独立Design；旧成品不按新版计费重算。改图和另存不能影响旧成品。图纸和色盒由SaveSession随正常游戏存档落盘。

## 原料事务

InventoryService绑定实际打开的拼豆台对象、地图和坐标；Capture构建背包槽及附近箱子槽的只读描述。范围为Chebyshev距离，即横纵各N格。默认N=5，设置限0～20。箱子必须是确切Chest类型、playerChest、普通或BigChest、非冰箱／giftbox／globalInventory／特殊类型且未锁定。始终只遍历当前地图对象，不跨地图。

InventorySlot记录会话实例身份、物品ID、数量、堆叠上限、品质、是否普通豆、原料目标材质和产量。仅确切Object类型且非任务、非配方、无额外modData的物品可扣。原料品质不增产；豆子只接受普通品质。容器Scope冻结对象身份、坐标、容量及实际列表长度；预览不扩充箱子列表。

MaterialSupply.Stock统一旧豆和可折算原料。Consume先用已有豆，然后按顺序选择原料；普通豆木材／石头／纤维／花卉／硬木，其他材质按槽序，同来源低品质优先。原料按整件向上取，超出需求生成实体余豆。先为成品保留背包槽，再用PlaceBeads合并／放置余豆。NoSpace与NoSurplusSpace区分成品无位和余豆无位。

CheckAvailability与Preview共用Evaluate规则，前者仅省略冻结产物和序列化计划，不跨帧缓存。点击熨烫才产生ManufacturingPlan。提交复查范围、槽位实例／数量／品质、规则、配方、余额及创造模式；创建所有产物和替换实例后再次复查，再应用库存与金币。失败回滚变更槽和原列表长度；TransactionRequests阻止成功后的重复交付及执行中重入。

精炼备料有独立BeadPreparationPlan和同等提交校验，一次从一个原料堆取得至少10豆或该堆现有可产量，实体豆仅放背包。该路径不使用创造模式折扣。精炼仍由原生机器负责耗豆、煤与计时。

## 图纸、自由颜色和界面

DraftAssessment对家具／装饰按计费视图有效格统一计普通豆，不读旧逐格材料或武器底料作为费用；服装只收模板羊毛预算。武器继续逐格材料＋补充底料。所有不透明RGB均可使用，不检查旧颜色解锁。新颜色ID为rgb.RRGGBB，解析入口BeadPalette.Resolve；旧颜色ID和保存RGB继续可读。

16色盒只保存选色快捷方式，不能作为重染全图的索引。PixelImport运行时默认PreserveColors，保留源图不透明颜色；近似基础色为可选项。填充／批量换色按实际RGBA兼容新旧ID。

EditorDocument持有私有草稿，所有对外快照深复制，最多40步撤销。一笔拖动一条历史。IsDirty缓存只依赖私有草稿内容，实际改动、工具、撤销／重做均使其失效。另存先成功写入新身份再切换，保留方向并重置历史。

FrameControls在执行动作前消费旧命中区，取消、布局变化和翻页清除；下一次绘制前不接受旧弹窗按钮或穿透到画布。

PalettePanel使用HSV方块与色相条，鼠标可拖动；临时编辑列表只在确认时写回FavoriteColors。梯度纹理仅在打开或色相变化时更新，关闭／返回标题释放。ArtResources的编辑豆粒为32×32程序圆环纹理，缩放大于等于5px仍保留孔洞，返回标题释放；不改变产品像素。ShowBeadHoles继续可控制豆粒表现，BeadMask旧公开资产保留兼容。

## 武器换算与原有绘制

H、W为全部用料包括底料的数量加权平均硬度与重量，整数中点远离零舍入。

| 武器 | 最小／最大伤害 | 速度及范围 | 击退及范围 |
| --- | --- | --- | --- |
| 剑 | 5H／7H | 2−W/3；−4～4 | 0.6＋0.04W；0～2 |
| 匕首 | 3H／4H | 3−W/4；−2～5 | 0.2＋0.02W；0～1 |
| 锤 | 7H／10H | 1−W/2；−8～2 | 1＋0.08W；0～3 |

默认暴击2%、倍率3，边界0～25%、1～5。先应用选中效果和超量惩罚，再舍入并限制伤害1～120。银河之魂按保存基础值每级累计10%，最多30%，不重复乘1.1。翡翠吸血按实际HP损失，满血／标题清理小数累计。

ProductPatches继续使用可嵌套线程上下文并finalizer恢复，只匹配确切模组成品ID。TextureCache最多128项，淘汰GPU资源延迟Rendered释放，不能提前销毁正在引用的纹理。

帽子四向20×20，上衣四向8×8映射256×32原生图集，顺序前右左后；裤子16×16印花映射原生裤型192×688动作与体型遮罩。试穿用克隆Farmer，关闭释放临时Renderer。椅子底8像素作为坐姿前景，桌子使用原生放物行为，均为单方向。

ReferenceReader只适配标准Object／ColoredObject／Furniture／Hat／Clothing／MeleeWeapon，冻结当次像素。不承诺自定义子类和特殊modData绘制兼容。自制成品恢复设计，不复制强化或最终数值。

## 验证边界

checks直接链接Data，不启动游戏。接口检查读取本机程序集元数据，增加Chest库存、容量、锁与特殊容器过滤入口核对；这些检查不能证明游戏中跨容器提交或GPU实际行为。布局PNG采用真实布局坐标但不是游戏截图。旧功能与新流程的实际验收都见PROGRESS。

按钮动效：Data/ButtonMotion为与帧率无关的有界过渡；ArtResources在菜单每帧统一计时，超过2秒不再绘制的控件清除状态，退出菜单清空缓存。视觉表面在原点击矩形内移动，正文、图标同步位移，原操作和确认事务不由动画驱动。

## 0.15.4 UI布局约定

EditorLayout统一框架、页头、盘面工具、工作栏、覆盖侧栏与页脚；ColorPickerLayout统一染色色区、预览、色盒及两排紧凑操作；MaterialPanelLayout提供普通／服装用料摘要，PickerLayout.Materials提供武器分页与批量替换来源。绘制代码不再重新收缩或居中面板。

EditorPanel.HasOverlay包括模态窗口和侧栏；WorkbenchMenu先路由覆盖层输入，FrameControls在覆盖层绘制前清除底层按钮，滚轮、右键、中键、快捷键均阻止穿透。窗口变动关闭侧栏、清除捕获与旧命中区，染色草稿继续保留。颜色列表仍只在保存时写回FavoriteColors，无新增存档格式。

ArtResources区分外框、浅色内容槽、下划线页签、普通按钮与主按钮。TextLine只使用20／18两档实际字体高度；ButtonText留12水平、8垂直边距，长文字截断而不缩字。悬停约120毫秒、按下位移上限3像素，命中矩形固定；UseNativeUi关闭时主按钮使用旧Ui图集高亮格回退。
