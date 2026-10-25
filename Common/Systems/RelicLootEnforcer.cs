using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RelicAnyMode.Common.Conditions;
using RelicAnyMode.Common.Configs;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace RelicAnyMode.Common.Systems
{
	/// <summary>
	/// 核心逻辑：扫描所有 NPC 的掉落规则树，找出「Boss 遗物（Master 稀有度、可放置的 Relic 雕像）」，
	/// 把它们路径上的「大师模式 / 复仇模式」门控条件替换成恒真条件，从而在任何世界难度下都能掉落。
	///
	/// 只替换「当前不满足、但在大师模式下会满足」的条件，因此：
	///   - 不会影响首杀限制、概率、其它附加条件；
	///   - 已经是大师模式的世界保持原版行为（遗物照常掉）；
	///   - 灾厄的「复仇模式或大师模式」条件在普通/专家世界也会被打开。
	/// </summary>
	public class RelicLootEnforcer : ModSystem
	{
		private static readonly FieldInfo MasterOverrideField =
			typeof(Main).GetField("_overrideForMasterMode", BindingFlags.NonPublic | BindingFlags.Static);

		private static readonly FieldInfo ExpertOverrideField =
			typeof(Main).GetField("_overrideForExpertMode", BindingFlags.NonPublic | BindingFlags.Static);

		private const BindingFlags AllInstanceFields =
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

		public sealed class GateRef
		{
			public object Node;
			public FieldInfo Field;
			public IItemDropRuleCondition Condition;
		}

		public sealed class BranchRef
		{
			public object Node;
			public string MasterFieldName;
			public string[] MirroredFieldNames;
		}

		public sealed class RelicDrop
		{
			public int NpcNetId;
			public int ItemId;
			public List<GateRef> Gates = new List<GateRef>();
			public List<BranchRef> Branches = new List<BranchRef>();
			public string ItemName => GetItemInternalName(ItemId);
		}

		private readonly Dictionary<int, List<RelicDrop>> _relicsByNpc = new Dictionary<int, List<RelicDrop>>();
		private readonly List<string> _bypassDetails = new List<string>();
		private bool _appliedThisWorld;
		private int _scannedNpcTypes;
		private int _relicDropCount;
		private int _bypassedGateCount;
		private int _neutralizedDuplicateCount;
		private int _failSafeAdded;

		public int ScannedNpcTypes => _scannedNpcTypes;
		public int RelicDropCount => _relicDropCount;
		public int BypassedGateCount => _bypassedGateCount;
		public int NeutralizedDuplicateCount => _neutralizedDuplicateCount;
		public int FailSafeAddedCount => _failSafeAdded;
		public IReadOnlyList<string> BypassDetails => _bypassDetails;
		public IReadOnlyDictionary<int, List<RelicDrop>> RelicsByNpc => _relicsByNpc;

		public override void OnWorldLoad()
		{
			_appliedThisWorld = false;
			TryApply("world load");
		}

		public override void PostWorldLoad()
		{
			// WorldFile.LoadWorld 里 OnWorldLoad 之后还会调用 PostWorldLoad；若上一次因为掉落表未就绪没成功，这里再试。
			TryApply("post world load");
		}

		public override void OnWorldUnload()
		{
			_appliedThisWorld = false;
		}

		public override void PostUpdateEverything()
		{
			// 兜底：某些联机流程里 OnWorldLoad 可能早于掉落表可用，再尝试一次。
			if (!_appliedThisWorld)
				TryApply("post update");
		}

		public void TryApply(string reason)
		{
			if (_appliedThisWorld) return;

			var cfg = RelicAnyModeConfig.Instance;
			if (cfg != null && !cfg.ForceRelicDrops)
			{
				_appliedThisWorld = true;
				Mod.Logger.Info("[RelicAnyMode] 配置已关闭，跳过遗物掉落修正。");
				return;
			}

			var db = Main.ItemDropsDB;
			Mod.Logger.Info($"[RelicAnyMode] 开始处理掉落表（{reason}）：gameMenu={Main.gameMenu}, netMode={Main.netMode}, dedServ={Main.dedServ}, masterMode={Main.masterMode}, expertMode={Main.expertMode}, db={(db != null)}, npcSamples={(ContentSamples.NpcsByNetId?.Count ?? -1)}");
			if (db == null) return;

			_appliedThisWorld = true;
			_relicsByNpc.Clear();
			_bypassDetails.Clear();
			_scannedNpcTypes = 0;
			_relicDropCount = 0;
			_bypassedGateCount = 0;
			_neutralizedDuplicateCount = 0;
			_failSafeAdded = 0;

			try
			{
				foreach (var pair in ContentSamples.NpcsByNetId)
				{
					int netId = pair.Key;
					NPC sampleNpc = pair.Value;
					if (sampleNpc == null) continue;
					_scannedNpcTypes++;

					List<IItemDropRule> rules;
					try { rules = db.GetRulesForNPCID(netId, includeGlobalDrops: false); }
					catch { continue; }
					if (rules == null || rules.Count == 0) continue;

					var drops = DiscoverRelicDrops(rules);
					if (drops.Count == 0) continue;

					foreach (var drop in drops) drop.NpcNetId = netId;
					_relicDropCount += drops.Count;

					// 同一 NPC、同一遗物可能有多条规则（原版门控 + 各类模组的“额外模式”副本，
					// 例如 Fargo's Souls 永恒模式副本）。只留一条作为主规则并打开其难度门控，
					// 其余副本全部关闭，保证任何模式下都只掉一份遗物。
					foreach (var group in drops.GroupBy(d => d.ItemId))
					{
						var candidates = group.ToList();
						RelicDrop primary = ChoosePrimaryDrop(candidates, sampleNpc);
						OpenPrimaryDrop(primary, netId, sampleNpc);
						foreach (var duplicate in candidates)
						{
							if (ReferenceEquals(duplicate, primary)) continue;
							NeutralizeDuplicateDrop(duplicate, netId, sampleNpc);
						}
						EnsureGuaranteedDrop(group.Key, candidates, drops, netId, sampleNpc);
					}

					_relicsByNpc[netId] = drops;
				}
			}
			catch (Exception e)
			{
				Mod.Logger.Error("[RelicAnyMode] 扫描掉落表时出错：" + e);
			}

			Mod.Logger.Info($"[RelicAnyMode] 掉落表处理完成（{reason}）：扫描 {_scannedNpcTypes} 个 NPC，发现 {_relicDropCount} 条遗物掉落规则，打开 {_bypassedGateCount} 个难度门控，关闭 {_neutralizedDuplicateCount} 条重复规则，兜底补规则 {_failSafeAdded} 条。");
			if ((RelicAnyModeConfig.Instance?.VerboseLogging ?? true) && _bypassDetails.Count > 0)
			{
				foreach (string detail in _bypassDetails)
					Mod.Logger.Info("[RelicAnyMode]   " + detail);
			}
		}

		// ------------------------------------------------------------------
		// 规则树遍历
		// ------------------------------------------------------------------

		public static List<RelicDrop> DiscoverRelicDrops(IEnumerable<IItemDropRule> rules)
		{
			var found = new List<RelicDrop>();
			var visited = new HashSet<object>();
			foreach (var rule in rules)
			{
				if (rule == null) continue;
				Walk(rule, new List<GateRef>(), new List<BranchRef>(), found, visited);
			}
			return found;
		}

		private static void Walk(object node, List<GateRef> parentGates, List<BranchRef> parentBranches,
			List<RelicDrop> found, HashSet<object> visited)
		{
			if (node == null) return;
			var type = node.GetType();
			if (!type.IsClass || node is string || node is IItemDropRuleCondition) return;
			if (!visited.Add(node)) return;

			var gates = new List<GateRef>(parentGates);
			var branches = new List<BranchRef>(parentBranches);

			FieldInfo[] fields;
			try { fields = type.GetFields(AllInstanceFields); }
			catch { return; }

			// 1) 节点自身的条件（例如 ItemDropWithConditionRule.condition / LeadingConditionRule.condition）
			foreach (var field in fields)
			{
				if (!typeof(IItemDropRuleCondition).IsAssignableFrom(field.FieldType)) continue;
				object value;
				try { value = field.GetValue(node); } catch { continue; }
				if (value is IItemDropRuleCondition condition)
					gates.Add(new GateRef { Node = node, Field = field, Condition = condition });
			}

			// 2) 节点自身直接掉落的物品
			foreach (int itemId in CollectOwnItemIds(node, fields))
			{
				if (!IsRelicItem(itemId)) continue;
				found.Add(new RelicDrop
				{
					ItemId = itemId,
					Gates = new List<GateRef>(gates),
					Branches = new List<BranchRef>(branches)
				});
			}

			// 3) 链式后续规则（LeadingConditionRule 之后挂的 CommonDrop 等）
			if (node is IItemDropRule dropRule)
			{
				List<IItemDropRuleChainAttempt> chained;
				try { chained = dropRule.ChainedRules; } catch { chained = null; }
				if (chained != null)
				{
					foreach (var chain in chained)
					{
						if (chain == null) continue;
						try { Walk(chain.RuleToChain, gates, branches, found, visited); } catch { }
					}
				}
			}

			// 4) 其它嵌套规则字段（例如 DropBasedOnMasterMode.ruleForDefault / ruleForMasterMode）
			foreach (var field in fields)
			{
				if (typeof(IItemDropRuleCondition).IsAssignableFrom(field.FieldType)) continue;
				object value;
				try { value = field.GetValue(node); } catch { continue; }
				if (value == null) continue;

				if (value is IItemDropRule nestedRule)
				{
					var childBranches = branches;
					if (field.Name == "ruleForMasterMode")
					{
						childBranches = new List<BranchRef>(branches);
						childBranches.Add(new BranchRef
						{
							Node = node,
							MasterFieldName = field.Name,
							MirroredFieldNames = new[] { "ruleForDefault", "ruleForExpertmode" }
						});
					}
					Walk(nestedRule, gates, childBranches, found, visited);
				}
				else if (value is IEnumerable enumerable)
				{
					foreach (object element in enumerable)
					{
						try
						{
							if (element is IItemDropRule childRule) Walk(childRule, gates, branches, found, visited);
							else if (element is IItemDropRuleChainAttempt chainAttempt) Walk(chainAttempt.RuleToChain, gates, branches, found, visited);
						}
						catch { }
					}
				}
			}
		}

		private static IEnumerable<int> CollectOwnItemIds(object node, FieldInfo[] fields)
		{
			var seen = new HashSet<int>();
			foreach (var field in fields)
			{
				string name = field.Name;
				if (field.FieldType == typeof(int) &&
					(name.Equals("itemId", StringComparison.OrdinalIgnoreCase) || name.Equals("item", StringComparison.OrdinalIgnoreCase)))
				{
					int value;
					try { value = (int)field.GetValue(node); } catch { continue; }
					if (value > 0 && seen.Add(value)) yield return value;
				}
				else if (field.FieldType == typeof(int[]) &&
					(name.Equals("options", StringComparison.OrdinalIgnoreCase) ||
					 name.Equals("itemIds", StringComparison.OrdinalIgnoreCase) ||
					 name.Equals("items", StringComparison.OrdinalIgnoreCase)))
				{
					int[] values;
					try { values = (int[])field.GetValue(node); } catch { continue; }
					if (values == null) continue;
					foreach (int value in values)
						if (value > 0 && seen.Add(value)) yield return value;
				}
			}
		}

		// ------------------------------------------------------------------
		// 遗物判定
		// ------------------------------------------------------------------

		public static bool IsRelicItem(int itemId)
		{
			if (itemId <= 0) return false;
			if (!ContentSamples.ItemsByType.TryGetValue(itemId, out Item sample) || sample == null) return false;

			// 遗物在大师难度物品里：稀有度为 Master(-13) 或标记了 Item.master
			bool masterLook = sample.master || sample.rare == ItemRarityID.Master;
			if (!masterLook) return false;

			string internalName = GetItemInternalName(itemId);
			if (!string.IsNullOrEmpty(internalName))
			{
				// 模组遗物：内部名通常含 Relic；原版遗物的内部名是 *MasterTrophy（如 KingSlimeMasterTrophy）。
				if (internalName.IndexOf("relic", StringComparison.OrdinalIgnoreCase) >= 0) return true;
				if (internalName.IndexOf("mastertrophy", StringComparison.OrdinalIgnoreCase) >= 0) return true;
			}

			// 兜底：本地化名字里带 Relic / 遗物
			string displayName = Lang.GetItemNameValue(itemId);
			if (string.IsNullOrEmpty(displayName)) return false;
			return displayName.IndexOf("relic", StringComparison.OrdinalIgnoreCase) >= 0 || displayName.Contains("遗物");
		}

		public static string GetItemInternalName(int itemId)
		{
			if (ContentSamples.ItemPersistentIdsByNetIds.TryGetValue(itemId, out string persistentId) &&
				!string.IsNullOrEmpty(persistentId))
				return persistentId;
			var modItem = ItemLoader.GetItem(itemId);
			return modItem?.Name;
		}

		public static string DescribeNpc(int netId, NPC sampleNpc)
		{
			int type = sampleNpc?.type ?? netId;
			string name = Lang.GetNPCNameValue(type);
			if (string.IsNullOrEmpty(name)) name = "NPC";
			return $"{name}#{netId}";
		}

		// ------------------------------------------------------------------
		// 条件判定与替换
		// ------------------------------------------------------------------

		private static bool TryReplaceGate(GateRef gate, IItemDropRuleCondition replacement, Mod mod)
		{
			try
			{
				gate.Field.SetValue(gate.Node, replacement);
				gate.Condition = replacement; // 同步缓存引用，后续审计/重复处理读到的才是最终状态
				return true;
			}
			catch (Exception e)
			{
				mod.Logger.Warn($"[RelicAnyMode] 替换条件失败 {gate.Node.GetType().Name}.{gate.Field.Name}: {e.Message}");
				return false;
			}
		}

		/// <summary>Fargo's Souls 永恒模式追加的大师门控副本规则（EModeNotMasterDropCondition）。</summary>
		private static bool IsFargoEternityDuplicate(IItemDropRuleCondition condition)
		{
			string name = condition.GetType().Name;
			return name.IndexOf("EModeNotMasterDropCondition", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		// ------------------------------------------------------------------
		// 同一遗物的多条规则：挑一条主规则，其余作为副本关闭
		// ------------------------------------------------------------------

		private RelicDrop ChoosePrimaryDrop(List<RelicDrop> candidates, NPC sampleNpc)
		{
			if (candidates.Count == 1) return candidates[0];

			// 1) 已经有恒真门控的（上一次处理过的世界重新加载时）
			foreach (var drop in candidates)
				if (drop.Gates.Any(g => g.Condition is AlwaysMetCondition)) return drop;

			// 2) 带“可绕过的难度门控”的，即原版/灾厄/Catalyst 那条真正的主规则
			foreach (var drop in candidates)
				foreach (var gate in drop.Gates)
				{
					if (gate.Condition is NeverMetCondition) continue;
					if (IsFargoEternityDuplicate(gate.Condition)) continue;
					if (IsBypassableModeGate(gate.Condition, sampleNpc, Mod)) return drop;
				}

			// 3) 在经典/大师模拟下本来就能掉落的规则
			foreach (var drop in candidates)
				if (DropActiveInStandardMode(drop, sampleNpc)) return drop;

			return candidates[0];
		}

		private void OpenPrimaryDrop(RelicDrop drop, int netId, NPC sampleNpc)
		{
			foreach (var gate in drop.Gates)
			{
				if (gate.Condition is AlwaysMetCondition || gate.Condition is NeverMetCondition) continue;
				if (!IsBypassableModeGate(gate.Condition, sampleNpc, Mod)) continue;
				if (TryReplaceGate(gate, AlwaysMetCondition.Instance, Mod))
				{
					_bypassedGateCount++;
					_bypassDetails.Add($"{DescribeNpc(netId, sampleNpc)} -> {drop.ItemName}  [{DescribeCondition(gate.Condition)}]");
				}
			}
			foreach (var branch in drop.Branches)
				TryFlattenBranch(branch, Mod);
		}

		private void NeutralizeDuplicateDrop(RelicDrop duplicate, int netId, NPC sampleNpc)
		{
			if (duplicate.Gates.Count == 0)
			{
				Mod.Logger.Warn($"[RelicAnyMode] {DescribeNpc(netId, sampleNpc)} 的遗物 {duplicate.ItemName} 有一条没有条件、无法关闭的重复规则（忽略）。");
				return;
			}

			bool changed = false;
			foreach (var gate in duplicate.Gates)
			{
				if (gate.Condition is NeverMetCondition) continue;
				if (TryReplaceGate(gate, NeverMetCondition.Instance, Mod)) changed = true;
			}
			if (!changed) return;

			_neutralizedDuplicateCount++;
			_bypassDetails.Add($"[去重] {DescribeNpc(netId, sampleNpc)} -> {duplicate.ItemName}  [{string.Join(", ", duplicate.Gates.Select(g => DescribeCondition(g.Condition)))}]");
		}

		/// <summary>
		/// 兜底保证：如果某个遗物在所有模式下都没有可用规则（例如遇到未知的自定义门控导致判定失败），
		/// 且它看起来确实是一条“大师/复仇模式主掉率”，就补一条 1/1 的无条件掉落规则。
		/// 对多段 Boss 的身体/尾巴部件（只有副本规则、没有主规则）不会触发。
		/// </summary>
		private void EnsureGuaranteedDrop(int itemId, List<RelicDrop> candidates, List<RelicDrop> allDropsOfNpc, int netId, NPC sampleNpc)
		{
			bool hasAlwaysOn = candidates.Any(d => d.Gates.Count == 0 || d.Gates.All(g => g.Condition is AlwaysMetCondition));
			if (hasAlwaysOn) return;

			bool hasBypassedMasterGate = candidates.Any(d => d.Gates.Any(g => g.Condition is AlwaysMetCondition));
			var remainingGates = candidates
				.SelectMany(d => d.Gates)
				.Where(g => !(g.Condition is AlwaysMetCondition) && !(g.Condition is NeverMetCondition))
				.ToList();

			// 情况 A：活动 Boss（南瓜月/霜月小 Boss）的遗物被“波次掉落概率”门控。
			// 补一条恒定规则 + 关掉原遗物规则，保留奖杯的波次门控（奖杯规则不受影响）。
			if (hasBypassedMasterGate && remainingGates.Count > 0 && remainingGates.All(g => IsWaveGateCondition(g.Condition)))
			{
				foreach (var drop in candidates)
					foreach (var gate in drop.Gates)
						if (gate.Condition is AlwaysMetCondition)
							TryReplaceGate(gate, NeverMetCondition.Instance, Mod);
				AddGuaranteedRule(itemId, allDropsOfNpc, netId, sampleNpc, "活动 Boss 不限波次");
				return;
			}

			// 情况 B：看起来是一条被未知条件挡住的主规则（例如自定义门控判定失败）
			bool looksLikePrimaryMasterRule = candidates.Any(d => d.Gates.Count == 1 && IsMasterLikeCondition(d.Gates[0].Condition));
			if (!looksLikePrimaryMasterRule) return;

			foreach (var drop in candidates)
				foreach (var gate in drop.Gates)
					if (!(gate.Condition is NeverMetCondition))
						TryReplaceGate(gate, NeverMetCondition.Instance, Mod);
			AddGuaranteedRule(itemId, allDropsOfNpc, netId, sampleNpc, "自定义门控兜底");
		}

		private void AddGuaranteedRule(int itemId, List<RelicDrop> allDropsOfNpc, int netId, NPC sampleNpc, string reason)
		{
			try
			{
				Main.ItemDropsDB.RegisterToNPCNetId(netId, ItemDropRule.Common(itemId, 1, 1, 1));
				_failSafeAdded++;
				_bypassDetails.Add($"[保底] {DescribeNpc(netId, sampleNpc)} -> {GetItemInternalName(itemId)}  {reason}：已补一条 1/1 无条件遗物规则");
				// 让缓存/审计也知道这件遗物现在一定有恒定掉落规则
				allDropsOfNpc.Add(new RelicDrop { NpcNetId = netId, ItemId = itemId });
			}
			catch (Exception e)
			{
				Mod.Logger.Warn($"[RelicAnyMode] 无法为 {DescribeNpc(netId, sampleNpc)} 的 {GetItemInternalName(itemId)} 补兜底规则：{e.Message}");
			}
		}

		private static bool IsWaveGateCondition(IItemDropRuleCondition condition)
		{
			string typeName = condition.GetType().Name;
			if (typeName.IndexOf("GatingChance", StringComparison.OrdinalIgnoreCase) >= 0 ||
				typeName.IndexOf("DropGating", StringComparison.OrdinalIgnoreCase) >= 0)
				return true;
			string description = DescribeCondition(condition);
			return description.IndexOf("varies by Wave", StringComparison.OrdinalIgnoreCase) >= 0 || description.Contains("波次");
		}

		private static bool IsMasterLikeCondition(IItemDropRuleCondition condition)
		{
			string typeName = condition.GetType().Name;
			if (typeName.IndexOf("MasterMode", StringComparison.OrdinalIgnoreCase) >= 0) return true;
			string description = DescribeCondition(condition);
			return description.IndexOf("master", StringComparison.OrdinalIgnoreCase) >= 0 || description.Contains("大师");
		}

		private static bool DropActiveInStandardMode(RelicDrop drop, NPC sampleNpc)
		{
			if (drop.Gates.Count == 0) return true;
			bool classic = drop.Gates.All(g => SafeCanDrop(g.Condition, sampleNpc, expertMode: false, masterMode: false));
			if (classic) return true;
			return drop.Gates.All(g => SafeCanDrop(g.Condition, sampleNpc, expertMode: true, masterMode: true));
		}

		private static bool SafeCanDrop(IItemDropRuleCondition condition, NPC sampleNpc, bool expertMode, bool masterMode)
		{
			try { return EvaluateCondition(condition, sampleNpc, expertMode, masterMode, applyOverrides: true); }
			catch { return false; }
		}

		public static string DescribeCondition(IItemDropRuleCondition condition)
		{
			string name = condition.GetType().Name;
			if (condition is IProvideItemConditionDescription provider)
			{
				try
				{
					string description = provider.GetConditionDescription();
					if (!string.IsNullOrEmpty(description)) return name + ":" + description;
				}
				catch { }
			}
			return name;
		}

		private static bool TryFlattenBranch(BranchRef branch, Mod mod)
		{
			try
			{
				var type = branch.Node.GetType();
				var masterField = type.GetField(branch.MasterFieldName, AllInstanceFields);
				if (masterField == null) return false;
				object masterRule = masterField.GetValue(branch.Node);
				if (masterRule == null) return false;

				bool changed = false;
				foreach (string name in branch.MirroredFieldNames)
				{
					var mirroredField = type.GetField(name, AllInstanceFields);
					if (mirroredField == null) continue;
					if (!ReferenceEquals(mirroredField.GetValue(branch.Node), masterRule))
					{
						mirroredField.SetValue(branch.Node, masterRule);
						changed = true;
					}
				}
				return changed;
			}
			catch (Exception e)
			{
				mod.Logger.Warn("[RelicAnyMode] 展开 Master 分支失败：" + e.Message);
				return false;
			}
		}

		/// <summary>
		/// 判断某条件是不是「经典模式挡住遗物、但大师模式会放行」的难度门控。
		/// 用模拟求值（强制 expert/master 覆盖）而不是当前世界状态判断，这样：
		///   - 大师世界开局时也能识别并替换，之后用 Fargo 世界令牌切回专家也不会失效；
		///   - 复仇模式等“本来就已放行”的条件不会被误判（经典模拟下已为 true）。
		/// </summary>
		private static bool IsBypassableModeGate(IItemDropRuleCondition condition, NPC sampleNpc, Mod mod)
		{
			if (condition == null) return false;
			try
			{
				bool allowedInClassic = EvaluateCondition(condition, sampleNpc, expertMode: false, masterMode: false, applyOverrides: true);
				if (allowedInClassic) return false;

				bool allowedInMaster = EvaluateCondition(condition, sampleNpc, expertMode: true, masterMode: true, applyOverrides: true);
				if (allowedInMaster) return true;

				// 兜底：类型名里明确带 MasterMode 的自定义条件
				return condition.GetType().Name.IndexOf("MasterMode", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			catch (Exception e)
			{
				mod.Logger.Warn("[RelicAnyMode] 条件判定失败 " + condition.GetType().FullName + "：" + e.Message);
				return false;
			}
		}

		public static bool EvaluateCondition(IItemDropRuleCondition condition, NPC npcContext, bool expertMode, bool masterMode, bool applyOverrides)
		{
			object previousMasterOverride = null;
			object previousExpertOverride = null;
			bool masterOverrideChanged = false;
			bool expertOverrideChanged = false;
			try
			{
				if (applyOverrides)
				{
					if (MasterOverrideField != null)
					{
						previousMasterOverride = MasterOverrideField.GetValue(null);
						MasterOverrideField.SetValue(null, masterMode ? (object)true : (object)false);
						masterOverrideChanged = true;
					}
					if (ExpertOverrideField != null)
					{
						previousExpertOverride = ExpertOverrideField.GetValue(null);
						ExpertOverrideField.SetValue(null, expertMode ? (object)true : (object)false);
						expertOverrideChanged = true;
					}
				}

				var info = new DropAttemptInfo
				{
					npc = npcContext,
					item = -1,
					player = FindPlayer(),
					rng = new UnifiedRandom(),
					IsInSimulation = true,
					IsExpertMode = expertMode,
					IsMasterMode = masterMode
				};
				return condition.CanDrop(info);
			}
			finally
			{
				if (masterOverrideChanged)
				{
					try { MasterOverrideField.SetValue(null, previousMasterOverride); } catch { }
				}
				if (expertOverrideChanged)
				{
					try { ExpertOverrideField.SetValue(null, previousExpertOverride); } catch { }
				}
			}
		}

		private static Player FindPlayer()
		{
			var players = Main.player;
			if (players == null) return null;
			for (int i = 0; i < players.Length; i++)
			{
				if (players[i] != null && players[i].active) return players[i];
			}
			return players.Length > 0 ? players[0] : null;
		}
	}
}
