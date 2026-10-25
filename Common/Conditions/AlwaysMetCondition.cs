using Terraria.GameContent.ItemDropRules;

namespace RelicAnyMode.Common.Conditions
{
	/// <summary>
	/// 恒真掉落条件：用于替换遗物掉落树里原本的「大师模式 / 复仇模式」门控条件。
	/// </summary>
	public sealed class AlwaysMetCondition : IItemDropRuleCondition
	{
		public static readonly AlwaysMetCondition Instance = new AlwaysMetCondition();

		public bool CanDrop(DropAttemptInfo info) => true;

		public bool CanShowItemDropInUI() => true;

		public string GetConditionDescription() => "";
	}
}
