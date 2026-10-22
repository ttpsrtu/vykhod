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
            if (info?.IsWeapon == true)
            {
                var gp = AssetCatalog.GunModel(info.weapon);
                visual = gp != null ? AssetCatalog.Spawn(gp, transform, 0.8f, false, false, info.weapon.modelVariant) : AssetCatalog.GunPlaceholder(info.weapon, transform);
                if (gp != null) { AssetCatalog.OrientGun(visual); AssetCatalog.Recenter(visual); }
                visual.transform.localRotation = Quaternion.Euler(90, 0, 0) * visual.transform.localRotation; // лежит на земле
            }
            else
            {
                string hint = info?.IsMod == true ? info.mod.model : info?.model ?? "crate";
                visual = AssetCatalog.SpawnOrPlaceholder(hint, transform, 0.35f, new Color(0.8f, 0.6f, 0.2f), PrimitiveType.Cube, null, false);
            }
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
