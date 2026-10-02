using System;
using System.Collections.Generic;
using InvisibilityPotion.Goggles;
using UnityEngine;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Hover, pick and regrowth of the hidden plants (spec plan 5 §3.3); own component instead of vanilla Pickable (Pickable needs
    /// its own ZNetView, Pickable.cs:80, has two states only, and pick-all mods would find it). Uses the ZNetView in its parents:
    /// the ground plant's own root view, or the tree's view for Huldra's Hair on a trunk (vanilla child pattern of
    /// MaterialVariation/SpawnPrefab, research §2.3). State: two ZDO keys (base stage, time of the last change) written by the ZDO
    /// owner only; no keys = ripe. Every client derives the stage with <see cref="HarvestStage.At"/> every 5 s.
    /// </summary>
    public sealed class VeilHarvest : MonoBehaviour, Hoverable, Interactable
    {
        public const string RpcHarvest = "IP_Harvest";
        public const string RpcHarvested = "IP_Harvested";
        public static readonly int LichenStageHash = "IP_LichenStage".GetStableHashCode();
        public static readonly int LichenTimeHash = "IP_LichenTime".GetStableHashCode();
        public static readonly int PlantStageHash = "IP_PlantStage".GetStableHashCode();
        public static readonly int PlantTimeHash = "IP_PlantTime".GetStableHashCode();
        public static readonly int PlantScaleHash = "IP_PlantScale".GetStableHashCode();
        public const string VariantSalt = "IP_PlantVariant";
        private const float StageInterval = 5f;
        private const float LocalPickHold = 6f;

        /// <summary>Every live harvest component (ip_plants).</summary>
        public static readonly HashSet<VeilHarvest> All = new HashSet<VeilHarvest>();

        /// <summary>Plant tier 1..3: goggle level needed, yield ([Plants] YieldTN) and item VeilIngredient_TN.</summary>
        public int Tier = 1;
        /// <summary>Ripe stage: 3 for Huldra's Hair (S1..S3), 2 for the ground plants (1 = picked, 2 = ripe).</summary>
        public int MaxStage = 3;
        /// <summary>Keys IP_LichenStage/IP_LichenTime on the parent tree's ZDO; else IP_PlantStage/IP_PlantTime on the own ZDO.</summary>
        public bool OnTree;
        /// <summary>Localization token of the plant name.</summary>
        public string NameToken = "";
        /// <summary>Drop direction for the pick: up for ground plants, outward (local +Z) for a trunk patch.</summary>
        public bool DropOutward;
        /// <summary>
        /// Mixed prefabs (wild spawns): one model variant per instance, picked in Awake from a hash of the spawn position (same on
        /// every client, LichenRoll.Value with salt IP_PlantVariant); the others are deactivated. Empty = fixed variant.
        /// </summary>
        public GameObject[] Variants = new GameObject[0];

        private ZNetView _nview;
        private VeilSight _sight;
        private bool _registered;
        private float _pickedLocallyUntil;
        private static readonly HashSet<string> LoggedErrors = new HashSet<string>();

        public int Stage { get; private set; }
        /// <summary>Uniform size of a ground plant (IP_PlantScale), 1 for the lichen and plants without a size range.</summary>
        public float PlantScale { get; private set; } = 1f;
        public int VariantIndex { get; private set; } = -1;
        public int BaseStage { get; private set; }
        public long BaseTicks { get; private set; }
        public ZNetView View => _nview;
        public string ItemName => $"VeilIngredient_T{Tier}";
        public int StageHash => OnTree ? LichenStageHash : PlantStageHash;
        public int TimeHash => OnTree ? LichenTimeHash : PlantTimeHash;
        public double StageMinutes => Tier == 1 ? Config.PluginConfig.LichenStageMinutes : Config.PluginConfig.GroundRegrowMinutes;

        private void Awake()
        {
            _sight = GetComponent<VeilSight>();
            Stage = MaxStage;
            All.Add(this);
            if (Variants.Length > 1)
            {
                var p = transform.position;
                VariantIndex = Mathf.Min(Variants.Length - 1, (int)(LichenRoll.Value(0, p.x, p.z, VariantSalt) * Variants.Length));
                for (var i = 0; i < Variants.Length; i++)
                    if (Variants[i] != null) Variants[i].SetActive(i == VariantIndex);
            }
        }

        private void Start()
        {
            _nview = GetComponentInParent<ZNetView>();
            if (_nview == null || !_nview.IsValid())
            {
                LogOnce("noview", $"plants: {name} has no valid ZNetView in its parents; harvest disabled");
                enabled = false;
                return;
            }
            try
            {
                // ZNetView.Register uses Dictionary.Add (ZNetView.cs:275): one VeilHarvest per view by construction.
                _nview.Register(RpcHarvest, RPC_Harvest);
                _nview.Register(RpcHarvested, RPC_Harvested);
                _registered = true;
            }
            catch (ArgumentException e)
            {
                LogOnce("register", $"plants: {name}: RPC already registered on {_nview.name} ({e.Message}); harvest disabled");
                enabled = false;
                return;
            }
            ApplyScale();
            UpdateStage();
            InvokeRepeating(nameof(UpdateStage), UnityEngine.Random.Range(0.5f, StageInterval), StageInterval);
        }

        /// <summary>
        /// Ground plants with a size range (Baldr's Tear 0.8-1.3, Hel's Ember Fern 0.7-1.6): the ZDO owner stores IP_PlantScale once,
        /// taking the scale a wild spawn already got from VegetationConfig.ScaleMin/Max (PlaceVegetation -> ZNetView.SetLocalScale,
        /// ZoneSystem.cs:1553) or rolling one (ip_spawn); every client applies it to the root. ZNetView.m_syncInitialScale is on for
        /// these prefabs, so the vanilla "scale" key (ZNetView.SyncScale, ZNetView.cs:123) also restores it on load (ZNetView.cs:67-80).
        /// </summary>
        private void ApplyScale()
        {
            if (OnTree || Tier < 1 || Tier > 3) return;
            var (min, max) = PlantYield.ScaleRanges[Tier];
            if (!(max > min)) return;
            var zdo = _nview.GetZDO();
            var s = zdo.GetFloat(PlantScaleHash, 0f);
            if (s <= 0f)
            {
                if (!_nview.IsOwner()) return;   // the owner writes it; UpdateStage applies it when it arrives
                var current = transform.localScale.x;
                s = Mathf.Abs(current - 1f) > 0.001f ? current : PlantYield.RollScale(Tier, UnityEngine.Random.value);
                zdo.Set(PlantScaleHash, s);
            }
            PlantScale = s;
            if (Mathf.Abs(transform.localScale.x - s) > 0.001f) _nview.SetLocalScale(Vector3.one * s);   // non-owner: transform only
        }

        private void OnDestroy()
        {
            All.Remove(this);
            if (_registered && _nview != null)
            {
                _nview.Unregister(RpcHarvest);
                _nview.Unregister(RpcHarvested);
            }
        }

        /// <summary>Stage from the ZDO only (no local pick hold).</summary>
        private int ZdoStage()
        {
            var zdo = _nview.GetZDO();
            BaseStage = zdo.GetInt(StageHash, 0);
            BaseTicks = zdo.GetLong(TimeHash, 0L);
            var now = ZNet.instance != null ? ZNet.instance.GetTime().Ticks : BaseTicks;
            return HarvestStage.At(BaseStage, HarvestStage.ElapsedMinutes(now, BaseTicks), StageMinutes, MaxStage);
        }

        public void UpdateStage()
        {
            try { UpdateStageUnsafe(); }
            catch (Exception e) { LogOnce("stage" + e.GetType().Name, $"plants: {name}: stage update failed: {e}"); }
        }

        private void UpdateStageUnsafe()
        {
            if (_nview == null || !_nview.IsValid()) return;
            if (!OnTree && Mathf.Approximately(PlantScale, 1f)) ApplyScale();
            var stage = ZdoStage();
            // The pick broadcast can arrive before the ZDO data: show the picked stage meanwhile.
            if (stage >= MaxStage && Time.time < _pickedLocallyUntil) stage = HarvestStage.Picked;
            var changed = stage != Stage;
            Stage = stage;
            if (_sight == null) return;
            _sight.ActiveStage = stage - 1;
            if (changed) _sight.RefreshNow();
        }

        public bool IsRipe => Stage >= MaxStage;

        public string GetHoverText()
        {
            if (_sight != null && !_sight.Revealed) return "";
            var text = IsRipe ? NameToken + "\n[<color=yellow><b>$KEY_Use</b></color>] $inventory_pickup" : NameToken + " ($ip_plant_growing)";
            return Localization.instance.Localize(text);
        }

        public string GetHoverName() => Localization.instance.Localize(NameToken);

        public float GetHoverOffset() => 0f;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || _nview == null || !_nview.IsValid() || user != Player.m_localPlayer) return false;
            if (!GoggleLevel.Reveals(GogglesLevel.Local, Tier)) return false;   // colliders are off anyway; client-side gate
            UpdateStage();
            if (!IsRipe) return false;
            _nview.InvokeRPC(RpcHarvest);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        /// <summary>Runs on the ZDO owner (InvokeRPC routes to the owner, ZNetView.cs:331), like Pickable.RPC_Pick (Pickable.cs:235).</summary>
        private void RPC_Harvest(long sender)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (ZdoStage() < MaxStage) return;   // serialises double picks
            var zdo = _nview.GetZDO();
            zdo.Set(StageHash, HarvestStage.Picked);
            zdo.Set(TimeHash, ZNet.instance.GetTime().Ticks);
            var dropped = Drop();
            try { PlantPrefabs.PickEffects?.Create(transform.position, Quaternion.identity); }
            catch (Exception e) { LogOnce("fx", $"plants: pick effects failed: {e.Message}"); }
            Plugin.Log.LogInfo($"plants: harvested {NameToken} T{Tier} at {transform.position:F1} (scale {PlantScale:F2}) for peer {sender}: {dropped} x {ItemName}");
            _nview.InvokeRPC(ZNetView.Everybody, RpcHarvested);
        }

        private void RPC_Harvested(long sender)
        {
            _pickedLocallyUntil = Time.time + LocalPickHold;
            UpdateStage();
        }

        /// <summary>Item stacks like Pickable.Drop (Pickable.cs:331): Instantiate, SetStack, ItemDrop.OnCreateNew, upward impulse.</summary>
        private int Drop()
        {
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(ItemName) : null;
            if (prefab == null && ZNetScene.instance != null) prefab = ZNetScene.instance.GetPrefab(ItemName);
            if (prefab == null) { LogOnce("item" + ItemName, $"plants: item {ItemName} not registered; nothing dropped"); return 0; }
            var (yMin, yMax) = Config.PluginConfig.Yield(Tier);
            var roll = PlantYield.Roll(yMin, yMax, UnityEngine.Random.value);
            var (sMin, sMax) = PlantYield.ScaleRanges[Mathf.Clamp(Tier, 1, 3)];
            var left = PlantYield.Scaled(roll, PlantYield.NormalizedScale(PlantScale, sMin, sMax));
            var total = left;
            var maxStack = Mathf.Max(1, prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_maxStackSize ?? 1);
            var i = 0;
            while (left > 0)
            {
                var n = Mathf.Min(left, maxStack);
                left -= n;
                var jitter = UnityEngine.Random.insideUnitCircle * 0.2f;
                var basePos = DropOutward ? transform.position + transform.forward * 0.4f : transform.position + Vector3.up * 0.5f;
                var go = Instantiate(prefab, basePos + new Vector3(jitter.x, 0.3f * i, jitter.y), Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f));
                var drop = go.GetComponent<ItemDrop>();
                if (drop != null)
                {
                    drop.SetStack(n);
                    ItemDrop.OnCreateNew(drop);
                }
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = DropOutward ? transform.forward * 1.5f + Vector3.up * 2f : Vector3.up * 4f;
                i++;
            }
            return total;
        }

#if DEBUG
        /// <summary>Debug (ip_grow): take ownership, write base stage and time = now, re-evaluate.</summary>
        public void DevSetStage(int stage)
        {
            if (_nview == null || !_nview.IsValid()) return;
            if (!_nview.IsOwner()) _nview.ClaimOwnership();
            var zdo = _nview.GetZDO();
            zdo.Set(StageHash, Mathf.Clamp(stage, 1, MaxStage));
            zdo.Set(TimeHash, ZNet.instance.GetTime().Ticks);
            _pickedLocallyUntil = 0f;
            UpdateStage();
        }
#endif

        private static void LogOnce(string key, string message)
        {
            if (LoggedErrors.Add(key)) Plugin.Log.LogWarning(message);
        }
    }
}
