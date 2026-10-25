using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace RelicAnyMode.Common.Configs
{
	/// <summary>
	/// 服务端（含单人）配置：是否强制让 Boss 遗物在任何难度下都掉落。
	/// </summary>
	public class RelicAnyModeConfig : ModConfig
	{
		public static RelicAnyModeConfig Instance;

		public override ConfigScope Mode => ConfigScope.ServerSide;

		[DefaultValue(true)]
		public bool ForceRelicDrops { get; set; } = true;

		[DefaultValue(true)]
		public bool ShowDefeatQuotes { get; set; } = true;

		[DefaultValue(true)]
		public bool VerboseLogging { get; set; } = true;

		public override void OnLoaded()
		{
			Instance = this;
		}
	}
}
