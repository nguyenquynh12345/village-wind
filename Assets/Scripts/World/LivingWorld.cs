using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// "Thế giới sống" (móc câu H4): cửa sổ sáng đèn ban đêm, khói ống khói buổi tối, mặt nước gợn nhẹ.
    /// </summary>
    public class LivingWorld : MonoBehaviour
    {
        static readonly Color WindowNight = Palette.Hex("#FFD27A");
        static readonly Color SmokeColor = Palette.Hex("#D9D4CC");
        static readonly Color GlassDay = Palette.Hex("#3E4C5E");   // kính ban ngày: xám xanh như phản chiếu trời

        readonly List<Transform> chimneys = new(), waters = new();
        readonly List<Light> lamps = new();
        readonly List<Renderer> windows = new();
        MaterialPropertyBlock darkBlock;
        readonly Dictionary<Light, float> lampBase = new();
        bool night, applied;
        float puffTimer, refreshTimer;

        void Update()
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f) { refreshTimer = 5f; Refresh(); }   // quét cây con khá tốn: 5 s một lần là đủ

            bool n = DayCycle.IsNight;
            if (n != night || !applied) { night = n; applied = true; ApplyWindows(); }

            float bob = Mathf.Sin(Time.time * 1.3f) * 0.03f * (1f - Seasons.Snow);
            foreach (var w in waters)
                if (w) w.localPosition = new Vector3(0, bob, 0);

            // đèn lồng / đèn tường lập loè như ánh lửa
            float tt = Time.time;
            foreach (var l in lamps)
            {
                if (!l || !l.enabled) continue;
                if (!lampBase.TryGetValue(l, out var b0)) { b0 = l.intensity; lampBase[l] = b0; }
                float ph = l.GetInstanceID() * 0.37f;
                float f = 0.88f + 0.08f * Mathf.PerlinNoise(tt * 2.3f + ph, ph) + 0.06f * Mathf.PerlinNoise(tt * 9f, ph * 2f);
                l.intensity = b0 * f;
            }

            {
                // khói: thưa ban ngày, dày buổi tối / trời mưa, mùa đông nhiều và dày hơn hẳn (nhà đang sưởi)
                bool winter = Settings.Season == 3;
                bool cozy = DayCycle.IsEvening || winter || Seasons.Rain > 0.5f;
                puffTimer -= Time.deltaTime;
                if (puffTimer <= 0f)
                {
                    puffTimer = winter ? 0.18f : cozy ? 0.32f : 0.8f;
                    foreach (var c in chimneys)
                        if (c && c.gameObject.activeInHierarchy) SpawnPuff(c);
                }
            }
        }

        void Refresh()
        {
            chimneys.Clear();
            waters.Clear();
            foreach (var h in EntityManager.OfType<HouseEntity>())
                foreach (var t in h.GetComponentsInChildren<Transform>())
                    if (t.name == "Chimney") chimneys.Add(t);
            // cửa sổ: tìm lại mỗi lần làm mới, áp trạng thái sáng/tối riêng từng ô
            windows.Clear();
            var wm = Palette.WindowMat();
            foreach (var h in EntityManager.OfType<HouseEntity>())
                foreach (var r in h.GetComponentsInChildren<Renderer>())
                    if (r.sharedMaterial == wm) windows.Add(r);
            ApplyWindowVariety();

            lamps.Clear();
            var world = EntityManager.Root;
            if (world)
                foreach (var l in world.GetComponentsInChildren<Light>())
                    if (l.type == LightType.Point && l.name != "RoomLight") lamps.Add(l);
            if (Combos.I)
                foreach (var l in (Combos.Deco ? Combos.Deco : Combos.I.transform).GetComponentsInChildren<Light>())
                    if (l.type == LightType.Point && l.name != "RoomLight") lamps.Add(l);
            if (lampBase.Count > lamps.Count * 2 + 8)   // dọn đèn đã bị xoá
            {
                var keep = new HashSet<Light>(lamps);
                var dead = new List<Light>();
                foreach (var k in lampBase.Keys) if (!k || !keep.Contains(k)) dead.Add(k);
                foreach (var k in dead) lampBase.Remove(k);
            }
            foreach (var p in EntityManager.OfType<PondEntity>())
                foreach (var t in p.GetComponentsInChildren<Transform>())
                    if (t.name == "Water") waters.Add(t);
        }

        static void ApplyWindowsStatic(bool night)
        {
            var m = Palette.WindowMat();
            if (night) { m.color = WindowNight; Palette.Glow(m, Palette.Hex("#FFB050"), 1.3f); }
            else { m.color = GlassDay; Palette.NoGlow(m); }
        }

        void ApplyWindows()
        {
            ApplyWindowsStatic(night);
            ApplyWindowVariety();
        }

        /// <summary>Ban đêm ~1/4 số ô cửa tắt đèn (cố định theo vị trí, không nhấp nháy khi dựng lại).</summary>
        void ApplyWindowVariety()
        {
            if (darkBlock == null)
            {
                darkBlock = new MaterialPropertyBlock();
                darkBlock.SetColor("_BaseColor", GlassDay * 0.6f);
                darkBlock.SetColor("_Color", GlassDay * 0.6f);
                darkBlock.SetColor("_EmissionColor", Color.black);
            }
            foreach (var r in windows)
            {
                if (!r) continue;
                var p = r.transform.position;
                int hsh = Mathf.FloorToInt(p.x * 0.9f) * 73856093 ^ Mathf.FloorToInt(p.y * 0.6f) * 19349663 ^ Mathf.FloorToInt(p.z * 0.9f) * 83492791;
                bool off = night && ((hsh >> 3) & 3) == 0;
                if (off) r.SetPropertyBlock(darkBlock);
                else r.SetPropertyBlock(null);
            }
        }

        void SpawnPuff(Transform chimney)
        {
            var top = chimney.position + Vector3.up * (chimney.lossyScale.y * 0.5f + 0.2f);
            if (AmbientFx.Smoke(top, Settings.Season == 3 ? 1.3f : 1f)) return;   // khói dạng hạt (mềm, liền mạch)
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = g.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            g.name = "Smoke";
            g.transform.position = top;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = Palette.Mat(SmokeColor);
            r.shadowCastingMode = ShadowCastingMode.Off;
            g.AddComponent<Puff>();
        }
    }
}
