# 更好的拼豆 · 美术与文案编辑包 0.16.4

先读`../docs/CONTENT_EDITING.md`。运行PNG与尺寸在`content-index.json`，全部文案在`text/catalog.csv`，实际修改`BetterBeads/i18n`；参考图纸现在直接读取`BetterBeads/assets/patterns`的26张PNG。索引与预览不是游戏输入。

推荐双击编辑包根目录的`打开像素编辑器.cmd`。打开素材文件夹后，`editor.html`可直接修改44张PNG并写回原文件，自动备份。画笔、填色、取色、图集单格和撤销重做均可使用；不需要服务器。详见`../docs/PIXEL_EDITOR.md`。

旧82图块源文件编辑器保留在`source-editor.html`，仍采用下载JSON/PNG方式。新编辑器直接修改运行PNG，不自动同步source.json；请注意再次导出源图会覆盖直接修改的PNG。

安装包和编辑包用途不同：`BetterBeads-0.16.4.zip`用于游戏；`BetterBeads-Art-0.16.4.zip`用于修改美术和文案，请解压到独立目录。编辑包保留art、BetterBeads/assets、BetterBeads/i18n、docs的相对结构，不包含运行DLL。

详细尺寸、改色方式、按材质ID的图集接口、重新载入命令和参考资料见 `../docs/ART_GUIDE.md`。

cover.png为imagegen生成的原创工坊主题封面，不是游戏截图，也没有从这张大图缩小切片作为游戏贴图。游戏内小图使用独立的原生尺寸像素源；封面提示词见cover-prompt.txt。未使用CLI图像生成或复制教程示例素材。

新编辑器已检查浏览器页面与素材搜索，并以临时文件验证保存/备份逻辑；首次目录授权由用户在Edge/Chrome中完成。游戏内尚未实测。

基础源图仍为82个图块；加上26张参考方向PNG，运行图片共42张。更多变化见../docs/FEATURES.md。

布局预览的JSON和PNG保留作检查参考；`preview-layout.mjs` 默认只导出PNG，如需调试中间SVG，显式加 `--svg`。

0.15.4提供8张按C#布局导出的离线示意：双栏主界面、小屏主界面、覆盖豆色侧栏、宽屏染色、小屏色盒、小屏染色页、武器材质、服装用料。均非游戏截图，示意字体采用微软雅黑。

文案可双击根目录的打开文案编辑器.cmd直接修改，支持搜索、分类、占位符检查、原文件保存与自动备份。使用说明见docs/TEXT_EDITOR.md；编辑包内路径为../docs/TEXT_EDITOR.md。


