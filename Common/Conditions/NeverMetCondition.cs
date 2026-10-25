using Terraria.GameContent.ItemDropRules;

namespace RelicAnyMode.Common.Conditions
{
	/// <summary>
	/// 恒假掉落条件：用于关闭「 Fargo's Souls 永恒模式追加的大师门控副本规则 」，
	/// 避免本模组把原规则改成恒真后，永恒模式下同一件遗物掉两份。
	/// </summary>
	public sealed class NeverMetCondition : IItemDropRuleCondition
	{
		public static readonly NeverMetCondition Instance = new NeverMetCondition();

		public bool CanDrop(DropAttemptInfo info) => false;

		public bool CanShowItemDropInUI() => false;

		public string GetConditionDescription() => "";
	}
}
