using System;
using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>Sổ đăng ký mọi Entity theo Id. Tạo / xoá / tìm, và báo cho vật biết khi địa hình đổi.</summary>
    public static class EntityManager
    {
        static readonly Dictionary<int, Entity> map = new();
        static readonly Dictionary<string, Type> types = new()
        {
            { "WallGraph", typeof(WallGraph) },
            { "House", typeof(HouseEntity) },
            { "Pond", typeof(PondEntity) },
            { "Tree", typeof(TreeEntity) },
            { "Path", typeof(PathEntity) },
            { "Blocks", typeof(BlockWorld) },
            { "Prop", typeof(PropEntity) },
            { "Ivy", typeof(IvyLayer) },
        };
        static int nextId = 1;
        static Transform root;
        public static Transform Root => root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            map.Clear();
            nextId = 1;
            root = null;
        }

        public static IEnumerable<Entity> All => map.Values;

        public static Entity Get(int id) => id != 0 && map.TryGetValue(id, out var e) && e ? e : null;

        public static IEnumerable<T> OfType<T>() where T : Entity
        {
            foreach (var e in map.Values)
                if (e is T t && t) yield return t;
        }

        /// <summary>Tạo vật từ tham số JSON. id = 0 → cấp id mới. popDelay &lt; 0 → không chạy hiệu ứng nảy.</summary>
        public static Entity Spawn(string type, string json, int id = 0, float popDelay = -1f)
        {
            if (!root) root = new GameObject("World").transform;
            var go = new GameObject(type);
            go.transform.SetParent(root, false);
            var e = (Entity)go.AddComponent(types[type]);
            e.Id = id > 0 ? id : nextId++;
            nextId = Mathf.Max(nextId, e.Id + 1);
            map[e.Id] = e;
            e.Restore(json);
            if (popDelay >= 0f)
            {
                e.PlayPop(popDelay);
                if (popDelay < 0.3f) AmbientFx.DustAround(go);   // bụi bung ra khi đặt
            }
            return e;
        }

        /// <summary>Xoá sạch mọi vật (làng mới / trước khi tải). Không gọi OnRemoved.</summary>
        public static void Clear()
        {
            foreach (var e in map.Values)
            {
                if (!e) continue;
                e.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(e.gameObject);
            }
            map.Clear();
            nextId = 1;
        }

        public static void Destroy(int id)
        {
            if (!map.TryGetValue(id, out var e)) return;
            map.Remove(id);
            if (!e) return;
            e.gameObject.SetActive(false);
            e.OnRemoved();
            UnityEngine.Object.Destroy(e.gameObject);
        }

        /// <summary>Địa hình đổi trong vùng r. light = đang kéo cọ (chỉ cập nhật phần nhẹ như tháp).</summary>
        public static void TerrainChanged(Rect r, bool light, Entity except = null)
        {
            foreach (var e in new List<Entity>(map.Values))
                if (e && e != except && e.AffectedBy(r)) e.TerrainChangedIn(r, light);
        }

        /// <summary>Có công trình mới / bị xoá trong vùng r → cây ở đó kiểm tra lại có bị che không.</summary>
        public static void NotifyTrees(Rect r)
        {
            foreach (var t in new List<TreeEntity>(OfType<TreeEntity>()))
                if (t.AffectedBy(r)) t.Rebuild();
        }
    }
}
