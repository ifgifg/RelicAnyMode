using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RelicAnyMode.Common.Configs;
using RelicAnyMode.Common.Data;
using RelicAnyMode.Common.Systems;
using Terraria;
using Terraria.ModLoader;

namespace RelicAnyMode.Common.GlobalItems
{
	/// <summary>
	/// 在 Boss 遗物的 Tooltip 末尾追加一段「被击败语录」（按遗物内部名匹配，缺失时不追加）。
	/// </summary>
	public class RelicTooltipGlobalItem : GlobalItem
	{
		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (item == null) return;
			if (!(RelicAnyModeConfig.Instance?.ShowDefeatQuotes ?? true)) return;
			if (!RelicLootEnforcer.IsRelicItem(item.type)) return;

			string key = RelicLootEnforcer.GetItemInternalName(item.type);
			if (string.IsNullOrEmpty(key)) return;

			// 有专属语录就用专属的；没有（例如整合包里新加的 Boss）则用通用语录
			if (!RelicQuotes.Quotes.TryGetValue(key, out string quote) || string.IsNullOrEmpty(quote))
				quote = RelicQuotes.FallbackQuote;

			tooltips.Add(new TooltipLine(Mod, "RelicDefeatQuote", "「" + quote + "」")
			{
				OverrideColor = new Color(255, 194, 96)
			});
		}
	}
}
