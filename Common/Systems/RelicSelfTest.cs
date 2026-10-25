using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using RelicAnyMode.Common.Conditions;
using RelicAnyMode.Common.Data;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace RelicAnyMode.Common.Systems
{
	/// <summary>
	/// 无头服务器自检（仅当存档目录存在 relic_test.flag 时触发）：
	/// 1) 打印扫描统计；
	/// 2) 对 flag 里指定的 Boss 模拟结算掉落，检查遗物是否掉出。
	///
	/// 触发机制：专用服务器在没有玩家连接时不会跑 Main.Update（因此 PostUpdateEverything 不执行），
	/// 这里反射挂到 Main.OnTickForThirdPartySoftwareOnly —— 服务器主循环每次都会调用它。
	/// 结果写入 flag 所在目录的 relic_test_result.txt，并写日志前缀 [RELIC-TEST]。
	/// </summary>
	public class RelicSelfTest : ModSystem
	{
		private static readonly FieldInfo TickActionField =
			typeof(Main).GetField("OnTickForThirdPartySoftwareOnly", BindingFlags.NonPublic | BindingFlags.Static);

		private static Action _tickHandler;

		private int _ticks;
		private bool _ran;
		private string _flagPath;
		private string _resultDir;

		public override void OnWorldLoad()
		{
			_ticks = 0;
			_ran = false;
			_flagPath = FindFlagFile(out _resultDir);
			if (_flagPath == null) return;

			if (TickActionField != null)
			{
				if (_tickHandler == null) _tickHandler = OnServerTick;
				var existing = (Action)TickActionField.GetValue(null);
				TickActionField.SetValue(null, Delegate.Combine(existing, _tickHandler));
				Mod.Logger.Info("[RELIC-TEST] 检测到 relic_test.flag，自检已挂到服务器 tick：" + _flagPath);
			}
			else
			{
				Mod.Logger.Warn("[RELIC-TEST] 找不到 Main.OnTickForThirdPartySoftwareOnly，自检无法定时触发。");
			}
		}

		public override void OnWorldUnload()
		{
			DetachTick();
		}

		public override void OnModUnload()
		{
			DetachTick();
		}

		private void DetachTick()
		{
			if (TickActionField == null || _tickHandler == null) return;
			try
			{
				var existing = (Action)TickActionField.GetValue(null);
				TickActionField.SetValue(null, Delegate.Remove(existing, _tickHandler));
			}
			catch { }
		}

		private void OnServerTick()
		{
			if (_ran) return;
			if (++_ticks < 120) return;
			_ran = true;
			try
			{
				RunTest();
			}
			catch (Exception e)
			{
				Mod.Logger.Error("[RELIC-TEST] 自检异常：" + e);
			}
			DetachTick();
		}

		private void RunTest()
		{
			var lines = new List<string>();
			lines.Add($"# RelicAnyMode self test @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			lines.Add($"# masterMode={Main.masterMode} expertMode={Main.expertMode} gameMode={Main.GameMode} netMode={Main.netMode} savePath={Main.SavePath}");
			lines.Add($"# flag={_flagPath}");

			var enforcer = ModContent.GetInstance<RelicLootEnforcer>();
			enforcer.TryApply("self test");
			lines.Add($"# scannedNpcTypes={enforcer.ScannedNpcTypes} relicDrops={enforcer.RelicDropCount} bypassedGates={enforcer.BypassedGateCount} neutralizedDuplicates={enforcer.NeutralizedDuplicateCount} failSafeRules={enforcer.FailSafeAddedCount}");
			foreach (string detail in enforcer.BypassDetails)
				lines.Add("# gate: " + detail);

			AppendAudit(lines, enforcer);

			var identifiers = File.ReadAllLines(_flagPath)
				.Select(l => l.Trim())
				.Where(l => l.Length > 0 && !l.StartsWith("#"))
				.ToList();
			if (identifiers.Count == 0)
				identifiers.AddRange(new[] { "KingSlime", "DesertScourgeHead" });

			if (identifiers.Any(id => id.Equals("ALL", StringComparison.OrdinalIgnoreCase)))
			{
				TestAllBosses(lines, enforcer);
			}
			else
			{
				foreach (string identifier in identifiers)
				{
					try { TestNpc(identifier, lines); }
					catch (Exception e) { lines.Add($"[{identifier}] TEST EXCEPTION: {e}"); }
				}
			}

			string resultPath = Path.Combine(_resultDir, "relic_test_result.txt");
			File.WriteAllLines(resultPath, lines);
			foreach (string line in lines)
				Mod.Logger.Info("[RELIC-TEST] " + line);
			try { File.Move(_flagPath, _flagPath + ".done", overwrite: true); } catch { }
		}

		/// <summary>
		/// 审计：列出全部遗物规则的最终门控状态，并检查「每件遗物至少有一条规则在任何模式下都会掉」，
		/// 同时统计所有 Boss 类 NPC 中哪些没有遗物规则（用于回答“所有 BOSS”覆盖情况）。
		/// </summary>
		private void AppendAudit(List<string> lines, RelicLootEnforcer enforcer)
		{
			int relicRuleCount = 0;
			var itemGroups = new Dictionary<(int netId, int itemId), List<RelicLootEnforcer.RelicDrop>>();
			foreach (var pair in enforcer.RelicsByNpc)
			{
				int netId = pair.Key;
				string npcName = DescribeNpcByNetId(netId);
				foreach (var relic in pair.Value)
				{
					relicRuleCount++;
					var states = relic.Gates
						.Select(g => RelicLootEnforcer.DescribeCondition(g.Condition) + (g.Condition is AlwaysMetCondition ? ":BYPASSED" : (g.Condition is NeverMetCondition ? ":NEUTRALIZED" : ":ORIGINAL")))
						.ToList();
					bool alwaysOn = relic.Gates.Count == 0 || relic.Gates.All(g => g.Condition is AlwaysMetCondition);
					lines.Add($"# audit: {npcName} relic {relic.ItemName}#{relic.ItemId} gates=[{string.Join(", ", states)}] alwaysOn={alwaysOn}");

					var key = (netId, relic.ItemId);
					if (!itemGroups.TryGetValue(key, out var group))
						itemGroups[key] = group = new List<RelicLootEnforcer.RelicDrop>();
					group.Add(relic);
				}
			}

			int itemsTotal = 0, itemsWithAlwaysOn = 0;
			foreach (var group in itemGroups)
			{
				itemsTotal++;
				bool anyAlwaysOn = group.Value.Any(d => d.Gates.Count == 0 || d.Gates.All(g => g.Condition is AlwaysMetCondition));
				if (anyAlwaysOn) itemsWithAlwaysOn++;
				else lines.Add($"# audit-BAD: {DescribeNpcByNetId(group.Key.netId)} relic {group.Value[0].ItemName}#{group.Key.itemId} 没有恒真规则！");
			}
			lines.Add($"# audit-summary: relicRules={relicRuleCount} relicItemGroups={itemsTotal} everyGroupHasAlwaysOnRule={itemsWithAlwaysOn}/{itemsTotal}");

			// Boss 覆盖统计
			int bossCount = 0;
			var noRelicBosses = new List<string>();
			foreach (var pair in ContentSamples.NpcsByNetId)
			{
				NPC npc = pair.Value;
				if (npc == null || !npc.boss) continue;
				bossCount++;
				if (!enforcer.RelicsByNpc.ContainsKey(pair.Key))
				{
					string persisted = ContentSamples.NpcPersistentIdsByNetIds.TryGetValue(pair.Key, out string pid) ? pid : pair.Key.ToString();
					noRelicBosses.Add($"{Lang.GetNPCNameValue(npc.type)}#{pair.Key} [{persisted}]");
				}
			}
			lines.Add($"# boss-coverage: totalBossNpcs={bossCount} withRelicRules={bossCount - noRelicBosses.Count} withoutRelicRules={noRelicBosses.Count}");
			foreach (string boss in noRelicBosses.Take(120))
				lines.Add("# boss-no-relic: " + boss);

			// 反查：游戏里所有看起来是“遗物”的物品里，有哪些没有出现在我们的掉落规则扫描结果中
			var coveredItems = new HashSet<int>(enforcer.RelicsByNpc.Values.SelectMany(v => v.Select(d => d.ItemId)));
			var uncoveredRelics = new List<string>();
			foreach (var pair in ContentSamples.ItemPersistentIdsByNetIds)
			{
				int itemId = pair.Key;
				if (itemId <= 0) continue;
				if (!ContentSamples.ItemsByType.TryGetValue(itemId, out Item item) || item == null) continue;
				if (!(item.master || item.rare == Terraria.ID.ItemRarityID.Master)) continue;
				if (!RelicLootEnforcer.IsRelicItem(itemId)) continue;
				if (coveredItems.Contains(itemId)) continue;
				uncoveredRelics.Add($"{Lang.GetItemNameValue(itemId)}#{itemId} [{pair.Value}]");
			}
			lines.Add($"# uncovered-relic-items: {uncoveredRelics.Count}");
			foreach (string relic in uncoveredRelics.Take(60))
				lines.Add("# uncovered-relic-item: " + relic);

			// 语录覆盖率：每件被覆盖的遗物都应该在 RelicQuotes 里有“被击败语录”
			int quoteCovered = 0;
			var quoteMissing = new List<string>();
			foreach (int itemId in coveredItems)
			{
				string key = RelicLootEnforcer.GetItemInternalName(itemId);
				if (!string.IsNullOrEmpty(key) && RelicQuotes.Quotes.ContainsKey(key))
					quoteCovered++;
				else
					quoteMissing.Add($"{Lang.GetItemNameValue(itemId)}#{itemId} [{key}]");
			}
			lines.Add($"# quote-coverage: {quoteCovered}/{coveredItems.Count} missing={quoteMissing.Count}");
			foreach (string missing in quoteMissing.Take(60))
				lines.Add("# quote-MISSING: " + missing);

			// 内容探测：确认某些“应该有遗物”的模组内容是否真的加载进了游戏
			string[] probeItems =
			{
				"CalamityOverhaul/SeaShrimpRelic",
				"CalamityMod/AstrumDeusRelic",
				"CalamityMod/PrimordialWyrmRelic",
				"CalamityMod/THELORDERelic",
				"FargowiltasSouls/MutantRelic"
			};
			string[] probeNpcs =
			{
				"CalamityOverhaul/SeaShrimpBoss",
				"CJMOD/AstrumDeusHeadSpectral",
				"CalamityMod/PrimordialWyrmHead"
			};
			var itemNames = new HashSet<string>(ContentSamples.ItemPersistentIdsByNetIds.Values);
			var npcNames = new HashSet<string>(ContentSamples.NpcPersistentIdsByNetIds.Values);
			foreach (string probe in probeItems)
				lines.Add($"# content-probe-item: {probe} = {(itemNames.Contains(probe) ? "present" : "MISSING")}");
			foreach (string probe in probeNpcs)
				lines.Add($"# content-probe-npc: {probe} = {(npcNames.Contains(probe) ? "present" : "MISSING")}");
		}

		/// <summary>
		/// 全量测试：对掉落表里所有「有独立遗物掉落规则」的 Boss NPC，各连续击杀两次，断言每次恰好掉 1 件。
		/// 多段 Boss 的身体/尾巴部件（只有条件下副本、没有主规则）不算在内。
		/// </summary>
		private void TestAllBosses(List<string> lines, RelicLootEnforcer enforcer)
		{
			var entries = enforcer.RelicsByNpc.OrderBy(p => p.Key).ToList();
			int tested = 0, pass = 0, failed = 0, skipped = 0;
			var failures = new List<string>();
			var skippedList = new List<string>();

			lines.Add($"# all-boss-test: start (npcAssociations={entries.Count})");

			foreach (var entry in entries)
			{
				int netId = entry.Key;
				NPC sampleNpc = ContentSamples.NpcsByNetId[netId];
				int type = sampleNpc.type;
				string label = $"{Lang.GetNPCNameValue(type)}#{netId}";

				var expectedItems = new List<int>();
				foreach (var group in entry.Value.GroupBy(d => d.ItemId))
				{
					bool hasAlwaysOn = group.Any(d => d.Gates.Count == 0 || d.Gates.All(g => g.Condition is AlwaysMetCondition));
					if (hasAlwaysOn) expectedItems.Add(group.Key);
				}

				if (expectedItems.Count == 0)
				{
					skipped++;
					skippedList.Add($"{label} [{DescribePersistentNpc(netId)}]（只有模式副本，无独立遗物掉落）");
					continue;
				}

				tested++;
				var relicTypes = new HashSet<int>(expectedItems);
				int first = KillOnceAndCollect(type, relicTypes, 1, lines, log: false);
				int second = KillOnceAndCollect(type, relicTypes, 2, lines, log: false);
				bool ok = first == 1 && second == 1;
				if (ok) pass++;
				else
				{
					failed++;
					failures.Add($"{label} items=[{string.Join(", ", expectedItems.Select(RelicLootEnforcer.GetItemInternalName))}] kill1={first} kill2={second}");
				}
				lines.Add($"# boss-test: {label} items=[{string.Join(", ", expectedItems.Select(RelicLootEnforcer.GetItemInternalName))}] kill1={first} kill2={second} result={(ok ? "PASS" : "FAIL")}");
			}

			lines.Add($"# all-boss-test-summary: tested={tested} pass={pass} fail={failed} skippedPartsOrDuplicateOnly={skipped}");
			foreach (string failure in failures)
				lines.Add("# all-boss-test-FAIL: " + failure);
			foreach (string skip in skippedList.Take(40))
				lines.Add("# all-boss-test-skip: " + skip);
		}

		private static string DescribePersistentNpc(int netId)
			=> ContentSamples.NpcPersistentIdsByNetIds.TryGetValue(netId, out string pid) ? pid : netId.ToString();

		private static string DescribeNpcByNetId(int netId)
		{
			if (ContentSamples.NpcsByNetId.TryGetValue(netId, out NPC npc) && npc != null)
				return $"{Lang.GetNPCNameValue(npc.type)}#{netId}";
			return $"#{netId}";
		}

		private void TestNpc(string identifier, List<string> lines)
		{
			int netId = ResolveNetId(identifier);
			if (netId < 0)
			{
				lines.Add($"[{identifier}] NOT FOUND");
				return;
			}

			NPC sampleNpc = ContentSamples.NpcsByNetId[netId];
			int type = sampleNpc.type;
			string npcName = Lang.GetNPCNameValue(type);
			List<IItemDropRule> rules = Main.ItemDropsDB.GetRulesForNPCID(netId, includeGlobalDrops: false);
			var relics = RelicLootEnforcer.DiscoverRelicDrops(rules);

			lines.Add($"[{identifier}] npcType={type} netId={netId} name={npcName} rules={rules.Count} relics={relics.Count}");
			foreach (var relic in relics)
			{
				var gateStates = relic.Gates
					.Select(g => RelicLootEnforcer.DescribeCondition(g.Condition) + (g.Condition is AlwaysMetCondition ? ":BYPASSED" : (g.Condition is NeverMetCondition ? ":NEUTRALIZED" : ":ORIGINAL")))
					.ToList();
				lines.Add($"  relic {relic.ItemName} (itemId={relic.ItemId}) gates=[{string.Join(", ", gateStates)}] branches={relic.Branches.Count}");
			}

			var relicTypes = new HashSet<int>(relics.Select(r => r.ItemId));
			if (relicTypes.Count == 0)
			{
				lines.Add("  DROP TEST: skipped (no relic drop rule found)");
				return;
			}

			// 连续击杀两次，验证“无论第几次打都掉一个”
			int successRounds = 0;
			for (int round = 1; round <= 2; round++)
			{
				int dropped = KillOnceAndCollect(type, relicTypes, round, lines);
				if (dropped > 0) successRounds++;
			}

			lines.Add(successRounds == 2
				? "  REPEAT-KILL TEST: SUCCESS（第 1 次和第 2 次击杀均掉落）"
				: $"  REPEAT-KILL TEST: FAILED（2 次击杀中只有 {successRounds} 次掉落）");
		}

		/// <summary>生成一次 Boss、结算掉落、清场，返回掉出的遗物数量。</summary>
		private int KillOnceAndCollect(int type, HashSet<int> relicTypes, int round, List<string> lines, bool log = true)
		{
			int spawnX = (Main.spawnTileX + 8) * 16;
			int spawnY = (Main.spawnTileY - 6) * 16;

			var activeBefore = new HashSet<int>();
			for (int i = 0; i < Main.npc.Length; i++)
			{
				NPC n = Main.npc[i];
				if (n != null && n.active) activeBefore.Add(i);
			}

			NPC spawned = null;
			try
			{
				spawned = NPC.NewNPCDirect(new EntitySource_DebugCommand("RelicAnyMode self test"), new Vector2(spawnX, spawnY), type);
			}
			catch (Exception e)
			{
				if (log) lines.Add($"  KILL #{round}: SPAWN EXCEPTION: {e.Message}");
				return 0;
			}
			if (spawned == null)
			{
				if (log) lines.Add($"  KILL #{round}: SPAWN FAILED");
				return 0;
			}

			var dropped = new List<string>();
			try
			{
				spawned.SpawnedFromStatue = false;
				var info = new DropAttemptInfo
				{
					npc = spawned,
					item = -1,
					player = FindPlayer(),
					rng = new UnifiedRandom(),
					IsInSimulation = false,
					IsExpertMode = Main.expertMode,
					IsMasterMode = Main.masterMode
				};
				Main.ItemDropSolver.TryDropping(info);
			}
			catch (Exception e)
			{
				if (log) lines.Add($"  KILL #{round}: LOOT RESOLVER EXCEPTION: {e.Message}");
			}
			finally
			{
				try { spawned.active = false; } catch { }
				// 清理本次生成过程中产生的额外 NPC（多段 Boss 的子部件等），避免堆积影响后续测试
				for (int i = 0; i < Main.npc.Length; i++)
				{
					NPC extra = Main.npc[i];
					if (extra != null && extra.active && !activeBefore.Contains(i)) extra.active = false;
				}
			}

			Vector2 spawnCenter = new Vector2(spawnX, spawnY);
			for (int i = 0; i < Main.item.Length; i++)
			{
				Item item = Main.item[i];
				if (item == null || !item.active) continue;
				bool nearSpawn = Vector2.Distance(item.Center, spawnCenter) < 2400f;
				if (relicTypes.Contains(item.type))
				{
					dropped.Add($"{Lang.GetItemNameValue(item.type)} x{item.stack} (itemId={item.type}, rare={item.rare}, master={item.master})");
					item.active = false;
				}
				else if (nearSpawn)
				{
					item.active = false; // 清掉测试产生的普通掉落，避免堆积
				}
			}

			if (log)
			{
				lines.Add(dropped.Count > 0
					? $"  KILL #{round}: SUCCESS -> " + string.Join(" | ", dropped)
					: $"  KILL #{round}: FAILED (no relic item spawned)");
			}
			return dropped.Count;
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

		/// <summary>存档目录下找 relic_test.flag，兼容 Main.SavePath 是否包含 tModLoader 层级。</summary>
		private static string FindFlagFile(out string resultDir)
		{
			resultDir = null;
			var candidates = new List<string>();
			try
			{
				string savePath = Main.SavePath;
				if (!string.IsNullOrEmpty(savePath))
				{
					candidates.Add(Path.Combine(savePath, "relic_test.flag"));
					candidates.Add(Path.Combine(savePath, "tModLoader", "relic_test.flag"));
				}
			}
			catch { }

			foreach (string candidate in candidates)
			{
				try
				{
					if (File.Exists(candidate))
					{
						resultDir = Path.GetDirectoryName(candidate);
						return candidate;
					}
				}
				catch { }
			}
			return null;
		}

		private static int ResolveNetId(string identifier)
		{
			if (int.TryParse(identifier, out int number))
			{
				if (ContentSamples.NpcsByNetId.ContainsKey(number)) return number;
				foreach (var pair in ContentSamples.NpcsByNetId)
					if (pair.Value.type == number) return pair.Key;
			}

			foreach (var pair in ContentSamples.NpcPersistentIdsByNetIds)
			{
				if (!string.IsNullOrEmpty(pair.Value) &&
					pair.Value.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0)
					return pair.Key;
			}

			foreach (var pair in ContentSamples.NpcsByNetId)
			{
				string name = Lang.GetNPCNameValue(pair.Value.type);
				if (!string.IsNullOrEmpty(name) &&
					name.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0)
					return pair.Key;
			}
			return -1;
		}
	}
}
