using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Предмет, лежащий на земле (сетевой).</summary>
    public class WorldItem : NetworkBehaviour
    {
        public NetworkVariable<FixedString4096Bytes> Json = new NetworkVariable<FixedString4096Bytes>();
        public ItemInstance Item;
        GameObject visual;

        public override void OnNetworkSpawn()
        {
            Refresh();
            Json.OnValueChanged += (a, b) => Refresh();
        }

        void Refresh()
        {
            Item = ItemInstance.FromJsonString(Json.Value.ToString());
            if (visual != null) Destroy(visual);
            if (Item == null) return;
            var info = Item.Info;
            string hint = info?.IsWeapon == true ? info.weapon.model : info?.IsMod == true ? info.mod.model : info?.model ?? "crate";
            visual = AssetCatalog.SpawnOrPlaceholder(hint, transform, info?.IsWeapon == true ? 0.8f : 0.35f, info?.IsWeapon == true ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.8f, 0.6f, 0.2f), PrimitiveType.Cube, info?.IsWeapon == true ? "weapon|guns" : null, false);
            visual.transform.localPosition = Vector3.up * 0.15f;
            var col = GetComponent<Collider>(); if (col == null) { var sc = gameObject.AddComponent<SphereCollider>(); sc.radius = 0.5f; sc.isTrigger = true; }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void PickupRpc(RpcParams p = default)
        {
            if (!IsSpawned) return;
            Game.Manager.Net.Session.GiveItemRpc(Json.Value.ToString(), RpcTarget.Single(p.Receive.SenderClientId, RpcTargetUse.Temp));
            NetworkObject.Despawn(true);
        }
    }
}
