using System.Collections.Generic;

namespace RelicAnyMode.Common.Data
{
	/// <summary>
	/// Boss 遗物的「被击败语录」。
	/// 键 = 物品的持久化内部名（与 ContentSamples.ItemPersistentIdsByNetIds 一致）：
	///   - 原版遗物：如 KingSlimeMasterTrophy
	///   - 模组遗物：如 CalamityMod/DesertScourgeRelic
	/// 语录参考原版 Bestiary / 灾厄 Wiki / 法狗 Wiki 的文本与设定撰写，用于在遗物 Tooltip 中展示。
	/// </summary>
	public static class RelicQuotes
	{
		/// <summary>没有专属语录的遗物（例如别的整合包里新加的 Boss）使用的通用语录。</summary>
		public const string FallbackQuote = "你击败了它。关于它的传说，从此由你来写。";

		public static readonly Dictionary<string, string> Quotes = new Dictionary<string, string>
		{
			// ---------------- 原版 Boss（28） ----------------
			["KingSlimeMasterTrophy"] = "啵。史莱姆之王也有融化的一天——记得把王冠擦干净再戴回去。",
			["EyeofCthulhuMasterTrophy"] = "那只巨眼终于合上了。它至死都盯着你，像是想把你也一起拖进梦里。",
			["EaterofWorldsMasterTrophy"] = "腐化之地的长虫被一节节斩断，腥臭的风终于停了一会儿。",
			["BrainofCthulhuMasterTrophy"] = "血脑的搏动停息了。可猩红之地的低语，还在你耳边慢慢爬。",
			["SkeletronMasterTrophy"] = "地牢的守门人化作枯骨。它的诅咒，仍在地牢的长廊里回荡。",
			["QueenBeeMasterTrophy"] = "蜂后的嗡鸣消失了。她到死都护着那些还没孵化的幼虫。",
			["WallofFleshMasterTrophy"] = "血肉之墙轰然倒塌——世界被推向了无法回头的边缘。",
			["TwinsMasterTrophy"] = "一对红眼同时熄灭。它们再也不能分头追赶你了。",
			["DestroyerMasterTrophy"] = "钢铁长虫的电路烧断，残骸还在噼啪地冒着火花。",
			["SkeletronPrimeMasterTrophy"] = "四只机械臂逐一垂落，齿轮发出最后一声悲鸣。",
			["PlanteraMasterTrophy"] = "丛林的怒花凋谢了。可地底的根须，仍然不安地扭动。",
			["GolemMasterTrophy"] = "古老的石像沉默了。神庙的机关，重新归于死寂。",
			["DukeFishronMasterTrophy"] = "深海之王的鳞片碎裂，浪涛暂时退去——它只是回到了深海。",
			["LunaticCultistMasterTrophy"] = "邪教徒的仪式被粗暴打断。可月亮的注视，比从前更近了。",
			["MoonLordMasterTrophy"] = "克苏鲁的躯壳崩解，星辰的缺口缓缓闭合。世界终于喘了一口气。",
			["FairyQueenMasterTrophy"] = "光之女皇收拢翅膀。正午的光，第一次显得温柔。",
			["QueenSlimeMasterTrophy"] = "神圣的黏液散去，水晶碎片沉入云海。",
			["DeerclopsMasterTrophy"] = "寒冬的独眼巨人倒下。雪原记住了这声咆哮。",
			["MourningWoodMasterTrophy"] = "哀木的火焰熄灭，夜风里只剩下焦木的味道。",
			["PumpkingMasterTrophy"] = "南瓜王的面孔裂开，笑声散落在南瓜田里。",
			["EverscreamMasterTrophy"] = "常青松的颂歌走了调——它再也不会在冬夜里歌唱。",
			["IceQueenMasterTrophy"] = "冰霜女王碎成漫天冰晶，霜月的寒光黯淡了一瞬。",
			["SantankMasterTrophy"] = "圣诞机器人停摆，礼物散落一地，汽笛声渐渐远去。",
			["FlyingDutchmanMasterTrophy"] = "幽灵船的炮口沉入雾中，船长的诅咒随浪花散了。",
			["UFOMasterTrophy"] = "火星飞碟冒着黑烟坠落，外星人的尖叫戛然而止。",
			["BetsyMasterTrophy"] = "双足飞龙吐出的最后一缕火息，照亮了撒旦军团的旗。",
			["DarkMageMasterTrophy"] = "黑暗法师的图腾熄灭，古老军团的号角低沉了下来。",
			["OgreMasterTrophy"] = "食人魔的棍棒砸在自己脚下，守卫的誓言到此为止。",

			// ---------------- 灾厄本体（31） ----------------
			["CalamityMod/DesertScourgeRelic"] = "沙暴退去，巨虫的甲壳沉入黄沙。荒漠重归寂静——只是暂时。",
			["CalamityMod/CrabulonRelic"] = "菌蟹的孢子散尽，地下真菌的荧光暗了一格。",
			["CalamityMod/HiveMindRelic"] = "腐巢意志被撕开。可它腐烂的梦，还在虫群里沸腾。",
			["CalamityMod/PerforatorsRelic"] = "血肉之墙的孩子倒下。它们用身体堆砌的巢穴，开始崩塌。",
			["CalamityMod/SlimeGodRelic"] = "史莱姆之神被劈成两半——黏稠的信仰，就此失去形状。",
			["CalamityMod/CryogenRelic"] = "极寒的冰晶碎裂。霜冻的叹息，随寒风飘出很远。",
			["CalamityMod/BrimstoneElementalRelic"] = "硫磺元素的烈焰被浇灭，灰烬里残留着硫磺的甜味。",
			["CalamityMod/CalamitasCloneRelic"] = "克隆的灾厄女巫倒下了。可她的眼神像在说——‘我只是影子’。",
			["CalamityMod/LeviathanAnahitaRelic"] = "利维坦的咆哮与阿娜希塔的歌声一同消散，海底的漩涡平息了。",
			["CalamityMod/AstrumAureusRelic"] = "星辉菌主身上的星尘一点点熄灭，像一场迟来的流星雨。",
			["CalamityMod/AstrumDeusRelic"] = "双星之虫被斩断，星空的脉络短暂地痉挛了一下。",
			["CalamityMod/PlaguebringerGoliathRelic"] = "瘟疫巨像的脓疱破裂，毒雾散尽——可瘟疫，从不真正死去。",
			["CalamityMod/RavagerRelic"] = "劫掠者的钢铁骨架散落，复仇的咆哮被埋进碎石里。",
			["CalamityMod/ProvidenceRelic"] = "亵渎女神的光环崩落，硫火与圣光一同归于平静。",
			["CalamityMod/ProfanedGuardiansRelic"] = "三位守护者先后陨落。亵渎废墟的钟声，为他们响了三次。",
			["CalamityMod/StormWeaverRelic"] = "风暴织者被从云层里扯下，雷声渐渐远去。",
			["CalamityMod/CeaselessVoidRelic"] = "无休止的虚空停止了吞噬。那片黑暗，只是暂时收起了牙齿。",
			["CalamityMod/SignusRelic"] = "西格纳斯的身形在暗影中溶解，夜风里只剩下几声轻笑。",
			["CalamityMod/PolterghastRelic"] = "怨魂的锁链断开，地牢深处的哭声终于安静了。",
			["CalamityMod/DevourerOfGodsRelic"] = "吞噬神明者被自己的饥饿反噬。它的尸骸，压塌了整条宇宙走廊。",
			["CalamityMod/YharonRelic"] = "亚龙的火焰熄灭，重生之焰最后一闪——它还会再回来。",
			["CalamityMod/DraedonRelic"] = "机械军团的核心归于沉寂。德雷顿的数据库里，多了一条‘失败’。",
			["CalamityMod/CalamitasRelic"] = "至尊灾厄女巫的火焰熄灭。她的眼泪落在灰烬里，比硫火更烫。",
			["CalamityMod/AquaticScourgeRelic"] = "水灾的躯壳沉入深海，腐海的颜色淡了一点。",
			["CalamityMod/OldDukeRelic"] = "老公爵的毒牙崩断，酸海退潮，留下满地溃烂的贝壳。",
			["CalamityMod/GiantClamRelic"] = "巨蚌的壳碎成珍珠粉，沉没之海的潮声轻了下来。",
			["CalamityMod/GreatSandSharkRelic"] = "沙鲨跃出沙面的最后一刻被定格——沙漠安静了。",
			["CalamityMod/MaulerRelic"] = "狂鲨的鳍沉入沙海，利齿再也咬不到明天的太阳。",
			["CalamityMod/NuclearTerrorRelic"] = "核恐的熔芯熄灭了。酸雨停了几秒，辐射尘落在那副铁壳上。",
			["CalamityMod/CragmawMireRelic"] = "酸池巨口的黏液干涸，泥沼里只剩下一串气泡。",
			["CalamityMod/DragonfollyRelic"] = "龙愚的火息散尽，羽翼上的余烬随风飘向远山。",

			// ---------------- 法狗魂石（16） ----------------
			["FargowiltasSouls/TrojanSquirrelRelic"] = "特洛伊松鼠倒下。它死前还攥着一颗松果，像是在说：值了。",
			["FargowiltasSouls/MutantRelic"] = "突变体被击败了。它笑着合上眼：‘下一局，我可不会留手。’",
			["FargowiltasSouls/DeviRelic"] = "戴薇安收起恶作剧的笑容：‘好吧好吧，这次算你赢。’",
			["FargowiltasSouls/AbomRelic"] = "憎恶的巨斧落地。它低吼着：‘法狗……还没有结束。’",
			["FargowiltasSouls/LifelightRelic"] = "生命之光黯淡了。挑战者的嘶鸣，被风吹散在旷野。",
			["FargowiltasSouls/CursedCoffinRelic"] = "被诅咒的棺木合拢，钉子声停了——里面的人终于睡着了。",
			["FargowiltasSouls/EridanusRelic"] = "宇宙冠军的星环碎裂。他向你点头：‘打得漂亮，凡人。’",
			["FargowiltasSouls/EarthChampionRelic"] = "大地冠军的护甲崩落，地脉的震动缓缓平息。",
			["FargowiltasSouls/LifeChampionRelic"] = "生命冠军的藤蔓枯萎，她留下的最后一片叶子落在你掌心。",
			["FargowiltasSouls/NatureChampionRelic"] = "自然冠军的枝条折断，森林的呼吸轻了一半。",
			["FargowiltasSouls/ShadowChampionRelic"] = "暗影冠军融入黑暗。临走前，他只说了一句：‘别回头。’",
			["FargowiltasSouls/SpiritChampionRelic"] = "灵魂冠军的灵体消散，地宫的烛火一起摇曳。",
			["FargowiltasSouls/TerraChampionRelic"] = "泰拉冠军的巨剑入土。天下的砂砾，都向他低了低头。",
			["FargowiltasSouls/TimberChampionRelic"] = "木之冠军倒下，斧痕留在它自己的年轮上。",
			["FargowiltasSouls/WillChampionRelic"] = "意志冠军终于放弃了抵抗：‘你的意志……比我更强。’",
			["FargowiltasSouls/BaronRelic"] = "被放逐的男爵落败。他摘下礼帽向你致意：‘这出戏，就到这儿吧。’",

			// ---------------- 灾厄熵（6） ----------------
			["CalamityEntropy/AcropolisRelic"] = "卫城机械的核心停止转动。齿轮的余音里，还回荡着守卫的誓言。",
			["CalamityEntropy/ApsychosRelic"] = "阿普西科斯倒下了。他最后看见的，是自己没能算到的未来。",
			["CalamityEntropy/CruiserRelic"] = "巡洋巨舰解体，引擎的余火照亮了深海。",
			["CalamityEntropy/LuminarisRelic"] = "辉光蛾的翅粉散尽。它终于不必再追逐那盏太阳。",
			["CalamityEntropy/NihilityTwinRelic"] = "虚无双子的双翼同时熄灭，虚空里安静得能听见心跳。",
			["CalamityEntropy/ProphetRelic"] = "先知闭上了眼睛：‘我已经看见了结局……只是没想到是你。’",
			["CalamityEntropy/VoidDestroyerRelic"] = "虚空毁灭者坍缩成一颗哑火的奇点——它终于明白，毁灭也需要观众。",

			// ---------------- 催化剂（1） ----------------
			["CatalystMod/AstrageldonRelic"] = "星尘凝胶的折射光散去，星界巨兽坍缩成一颗安静的星核。",
		};
	}
}
