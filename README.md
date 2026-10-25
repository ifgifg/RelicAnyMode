# 圣遗物全模式掉落（RelicAnyMode）

> 《泰拉瑞亚》tModLoader 模组 —— 让 Boss 遗物在任意难度、任意击杀次数下都掉落，并附带每件遗物的「被击败语录」。
>
> **作者：AAA狙神夏乌拉（Bilibili）**

- 创意工坊：发布后补充
- GitHub：https://github.com/ifgifg/RelicAnyMode
- 安装说明：见 [README.txt](README.txt)

## 功能

- 任意难度（经典/专家/大师/旅途）下，Boss 遗物都会掉落，保持 Master 稀有度颜色（“彩色”名字）；
- **不限击杀次数**：第 1 次、第 2 次、第 N 次击杀都恰好掉 1 件，没有首杀限制，也不会因多个模组重复规则而双掉；
- 活动 Boss（南瓜月/霜月小 Boss）解除波次掉落限制；
- 兼容法狗魂石永恒模式（自动去重）；
- 支持原版 Boss、灾厄本体、灾厄附属（催化剂/灾厄熵/灾厄大修/CJMOD）等模组的遗物；
- **每件遗物附带被击败语录**（参考游戏文本与 Wiki 撰写），可在配置中关闭。

## 安装

见 [README.txt](README.txt)。简而言之：

1. 创意工坊订阅后，启动 tModLoader 并在「模组」列表确认启用；
2. 或手动把 `RelicAnyMode.tmod` 放进 `Documents/My Games/Terraria/tModLoader/Mods/` 后启用。

## 构建

```powershell
dotnet build .\RelicAnyMode.csproj
```

## 目录结构

```
RelicAnyMode.csproj     项目文件
build.txt               模组元数据
description.txt         创意工坊 / 模组列表描述
changelog.txt           更新日志
icon.png                模组图标
icon_workshop.png       创意工坊封面
Common/
  Systems/RelicLootEnforcer.cs   核心：扫描并改写掉落规则树
  Systems/RelicSelfTest.cs       无头自检
  Data/RelicQuotes.cs            每件遗物的被击败语录
  GlobalItems/RelicTooltipGlobalItem.cs  Tooltip 追加语录
  Conditions/AlwaysMetCondition.cs / NeverMetCondition.cs
  Configs/RelicAnyModeConfig.cs
Localization/           中英文本地化
_tools/                 图标生成脚本
```

## 许可

MIT License，详见 [LICENSE](LICENSE)。
