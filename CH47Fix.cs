using System.Collections.Generic;
using UnityEngine;
//Adds dropzone prefabs into path
//Commands
// /CH47Fix.showpath show the points the ch47 flys to.
// /CH47Fix.spawn spawns a ch47 that drops creates at admins location
// /CH47Fix.calldrop  Calls a ch47 to drop a create near where you called it.

namespace Oxide.Plugins
{
	[Info("CH47Fix", "bmgjet", "1.0.0")]
	[Description("Changes CH47 create behavour")]
	class CH47Fix : RustPlugin
	{
		#region Variables

		private List<Vector3> DropZoneNodes = new List<Vector3>();
		private List<HackableLockedCrate> AlreadyDropped = new List<HackableLockedCrate>();
		private List<Vector3> SpawnLocation = new List<Vector3>();
		private List<Vector3> DropLocation = new List<Vector3>();
		public static CH47Fix plugin;

		#endregion

		#region Commands

		[ChatCommand("CH47Fix.showpath")]
		private void DrawPath(BasePlayer player) { if (player.IsAdmin) { foreach (Vector3 vector in DropZoneNodes) { player.SendConsoleCommand("ddraw.sphere", 8f, Color.blue, vector, 2f); } } }

		[ChatCommand("CH47Fix.spawn")]
		private void SpawnCH47(BasePlayer player) { if (player.IsAdmin) { SpawnLocation.Add(player.transform.position); player.SendConsoleCommand("spawn ch47scientists.entity"); } }

		[ChatCommand("CH47Fix.calldrop")]
		private void CallDrop(BasePlayer player)
		{
			if (player.IsAdmin)
			{
				Vector3 TempDropZone = player.transform.position;
				TempDropZone.y = TerrainMeta.HeightMap.GetHeight(TempDropZone);
				DropLocation.Add(TempDropZone);
				SpawnLocation.Add(TerrainMeta.RandomPointOffshore() + new Vector3(0, 200, 0));
				player.SendConsoleCommand("spawn ch47scientists.entity");
				player.ChatMessage("CH47 called to drop near here!");
			}
		}

		#endregion

		#region Hooks

		private void Init() { plugin = this; }

		private void OnServerInitialized()
		{
			foreach (BaseEntity be in BaseEntity.serverEntities.entityList.Values) { if (be is HackableLockedCrate) { AlreadyDropped.Add(be as HackableLockedCrate); } }
			MonumentPath();
		}

		private void Unload() { plugin = null; }

		private void OnEntitySpawned(CH47HelicopterAIController helicopter)
		{
			if (SpawnLocation.Count > 0)
			{
				helicopter.transform.position = SpawnLocation[0];
				SpawnLocation.RemoveAt(0);
			}
			NextTick(() =>
			{
				if (helicopter == null) { return; }
				CH47AIBrain Brain = helicopter.GetComponent<CH47AIBrain>();
				if (Brain == null) { return; }
				BMGJETCH47PathFinder pathfinder = new BMGJETCH47PathFinder();
				pathfinder.brain = Brain;
				Brain.PathFinder = pathfinder;
				Puts("Changed CH47 PathFinding");
			});
		}

		private object CanHelicopterDropCrate(CH47HelicopterAIController helicopter){foreach (HackableLockedCrate droppedat in AlreadyDropped) { if (droppedat.Distance(helicopter) < 300f) { return false; } }return null;}

		private void OnHelicopterDropCrate(CH47HelicopterAIController helicopter) { if (DropLocation.Count != 0) { DropLocation.RemoveAt(0); } }

		private void OnCrateLanded(HackableLockedCrate create) { AlreadyDropped.Add(create); }

		private void OnCrateHackEnd(HackableLockedCrate create) { if (AlreadyDropped.Contains(create)) { AlreadyDropped.Remove(create); } }

		#endregion


		#region Classes

		private void MonumentPath()
		{
			if (TerrainMeta.Path != null && TerrainMeta.Path.Monuments != null && TerrainMeta.Path.Monuments.Count > 0)
			{
				foreach (MonumentInfo monumentInfo in TerrainMeta.Path.Monuments)
				{
					if (monumentInfo.Type != MonumentType.Cave && monumentInfo.Type != MonumentType.WaterWell && monumentInfo.Tier != MonumentTier.Tier0)
					{
						DropZoneNodes.Add(monumentInfo.transform.position + new Vector3(0, 50, 0));
					}
				}
			}
			foreach (CH47DropZone dz in CH47DropZone.dropZones){DropZoneNodes.Add(dz.transform.position + new Vector3(0, 50, 0));}
		}

		public class BMGJETCH47PathFinder : BasePathFinder
		{
			public CH47AIBrain brain;
			public bool customdrop = false;

			public override Vector3 GetRandomPatrolPoint()
			{
				Vector3 vector = plugin.DropZoneNodes[UnityEngine.Random.Range(0, plugin.DropZoneNodes.Count)];
				if (plugin.DropLocation.Count > 0) { vector = plugin.DropLocation[0]; if (!customdrop) { customdrop = true; DoDrop(); return vector; } }
				float num3 = Mathf.Max(TerrainMeta.WaterMap.GetHeight(vector), TerrainMeta.HeightMap.GetHeight(vector));
				float num4 = num3;
				RaycastHit raycastHit;
				if (Physics.SphereCast(vector + new Vector3(0f, 200f, 0f), 20f, Vector3.down, out raycastHit, 300f, 1218511105)) { num4 = Mathf.Max(raycastHit.point.y, num3); }
				vector.y = num4 + 30f;
				return vector;
			}

			void DoDrop()
			{
				if (brain != null && plugin.DropLocation != null && plugin.DropLocation.Count > 0)
				{
					if (Vector3.Distance(brain.transform.position, plugin.DropLocation[0]) < 100) { brain.GetEntity().DropCrate(); return; }
					brain.GetEntity().SetMoveTarget(plugin.DropLocation[0]);
					brain.mainInterestPoint = plugin.DropLocation[0];
					plugin.timer.Once(1f, DoDrop);
				}
			}
		}

		#endregion
	}
}