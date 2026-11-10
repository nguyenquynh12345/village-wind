using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Thuyền gỗ + cá nhảy (S4 — thế giới sống, không phải Entity, không lưu):
    /// - Mỗi ao ≥ 120 m² có 1 thuyền trôi chậm trong lòng ao, tránh bờ.
    /// - Mỗi sông dài ≥ 40 m có 1 thuyền trôi xuôi dòng, tới cuối thu nhỏ rồi xuất hiện lại ở đầu.
    /// - Lắc bập bềnh ±3° theo sin; nếu có bến (E4) thì mỗi ~60 s ghé đậu 10 s.
    /// - Cá nhảy khỏi mặt nước mỗi 6–15 s (nhiều hơn lúc hoàng hôn), tạo vòng gợn + tiếng "tõm".
    /// </summary>
    public class Boat : MonoBehaviour
    {
        public static readonly List<Boat> All = new();

        static readonly Color HullDark = Palette.Hex("#6B4F3E");
        static readonly Color HullWood = Palette.Hex("#9A7352");
        static readonly Color SeatWood = Palette.Hex("#B98A5E");
        static readonly Color HatStraw = Palette.Hex("#E2C46B");
        static readonly Color ShirtCol = Palette.Hex("#5B7FA3");
        static readonly Color FishCol = Palette.Hex("#E89A54");
        static readonly Color RippleCol = Palette.Hex("#DCEEF5");

        public PondEntity pond;
        public RiverEntity river;

        Transform visual, oarL, oarR;
        Renderer[] rends;
        float phase;

        // Trạng thái trên ao
        Vector2 pondPos, pondTarget, pondDir = Vector2.up;
        float waitTimer;

        // Trạng thái trên sông
        float riverDist;

        // Bến đậu (E4)
        bool hasDock;
        Vector2 dockPos, dockDir;
        float dockRiverDist;
        float dockCooldown;
        float dockStay;

        // Cá nhảy dùng chung
        static float fishTimer = 5f;
        static readonly List<FishJump> activeFish = new();

        struct FishJump
        {
            public Transform fish;
            public Transform ripple1, ripple2;
            public Vector3 p0, p1;
            public float waterY;
            public float t, duration;
            public bool splashedEnd;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            activeFish.Clear();
            fishTimer = 5f;
        }

        public Vector3 SeatWorldPos => transform.position + Vector3.up * 0.35f;

        void OnDestroy() => All.Remove(this);

        // ================= Quản lý tự sinh thuyền & cá =================
        public static void RefreshAll(Transform parent)
        {
            if (!parent)
            {
                for (int i = All.Count - 1; i >= 0; i--)
                    if (All[i]) Destroy(All[i].gameObject);
                All.Clear();
                return;
            }

            var houses = new List<HouseEntity>(EntityManager.OfType<HouseEntity>());
            var pondBoats = new HashSet<PondEntity>();
            var riverBoats = new HashSet<RiverEntity>();

            // Giữ thuyền cũ nếu ao/sông vẫn còn và đủ điều kiện; xoá thuyền không còn hợp lệ
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var b = All[i];
                if (!b) { All.RemoveAt(i); continue; }

                bool validPond = b.pond && EntityManager.Get(b.pond.Id) == b.pond && b.pond.Mask != null && b.pond.Mask.Area >= 120f;
                bool validRiver = b.river && EntityManager.Get(b.river.Id) == b.river && b.river.P.pts != null && b.river.P.pts.Count >= 2 && b.river.Length >= 40f;

                if (validPond && !pondBoats.Contains(b.pond))
                {
                    pondBoats.Add(b.pond);
                    b.FindPondDock(houses);
                    if (!SafeInPond(b.pond.Mask, b.pondPos, 0.8f))
                    {
                        if (FindSafePondSpot(b.pond.Mask, out var sp))
                        {
                            b.pondPos = sp;
                            b.pondTarget = sp;
                        }
                        b.PickNextPondTarget();
                    }
                    else if (!SafeInPond(b.pond.Mask, b.pondTarget, 0.8f))
                    {
                        b.PickNextPondTarget();
                    }
                }
                else if (validRiver && !riverBoats.Contains(b.river))
                {
                    riverBoats.Add(b.river);
                    b.FindRiverDock(houses);
                    b.riverDist = Mathf.Clamp(b.riverDist, 0f, b.river.Length);
                }
                else
                {
                    Destroy(b.gameObject);
                    All.RemoveAt(i);
                }
            }

            // Tạo thuyền mới cho ao ≥ 120 m² chưa có thuyền
            foreach (var p in EntityManager.OfType<PondEntity>())
            {
                if (p.Mask == null || p.Mask.Area < 120f || pondBoats.Contains(p)) continue;
                SpawnForPond(parent, p, houses);
            }

            // Tạo thuyền mới cho sông dài ≥ 40 m chưa có thuyền
            foreach (var rv in EntityManager.OfType<RiverEntity>())
            {
                if (rv.P.pts == null || rv.P.pts.Count < 2 || rv.Length < 40f || riverBoats.Contains(rv)) continue;
                SpawnForRiver(parent, rv, houses);
            }
        }

        static void SpawnForPond(Transform parent, PondEntity p, List<HouseEntity> houses)
        {
            if (!FindSafePondSpot(p.Mask, out var start)) return;
            var go = new GameObject("Boat_Pond");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Boat>();
            b.pond = p;
            b.phase = (p.Id * 1.73f) % 6.28f;
            b.pondPos = start;
            b.pondTarget = start;
            b.dockCooldown = Random.Range(45f, 65f);
            b.FindPondDock(houses);
            b.BuildModel();
            b.PickNextPondTarget();
            go.transform.position = new Vector3(start.x, p.WaterLevel, start.y);
            All.Add(b);
        }

        static void SpawnForRiver(Transform parent, RiverEntity rv, List<HouseEntity> houses)
        {
            var go = new GameObject("Boat_River");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<Boat>();
            b.river = rv;
            b.phase = (rv.Id * 2.17f) % 6.28f;
            b.riverDist = Mathf.Min(4f, rv.Length * 0.1f);
            b.dockCooldown = Random.Range(45f, 65f);
            b.FindRiverDock(houses);
            b.BuildModel();
            var p0 = Geo.PointAt(rv.Points, b.riverDist);
            go.transform.position = new Vector3(p0.x, rv.WaterLevelAt(p0), p0.y);
            All.Add(b);
        }

        void BuildModel()
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            // Thân thuyền gỗ (4 khối: đáy, mũi, lái, ghế ngang)
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.04f, 0), new Vector3(0.66f, 0.12f, 1.65f), HullDark, Quaternion.identity);
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.09f, 0.92f), new Vector3(0.44f, 0.13f, 0.52f), HullWood, Quaternion.Euler(-14f, 0, 0));
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.09f, -0.88f), new Vector3(0.50f, 0.13f, 0.44f), HullWood, Quaternion.Euler(12f, 0, 0));
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.14f, -0.1f), new Vector3(0.60f, 0.05f, 0.24f), SeatWood, Quaternion.identity);

            // Người chèo nhỏ + nón lá
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.30f, -0.25f), new Vector3(0.22f, 0.28f, 0.18f), ShirtCol, Quaternion.identity);
            Part(visual, PrimitiveType.Sphere, new Vector3(0, 0.50f, -0.25f), Vector3.one * 0.16f, Palette.Cream, Quaternion.identity);
            Part(visual, PrimitiveType.Cylinder, new Vector3(0, 0.58f, -0.25f), new Vector3(0.34f, 0.03f, 0.34f), HatStraw, Quaternion.identity);

            // Hai mái chèo hai bên mạn
            oarL = MakeOar(visual, -1f);
            oarR = MakeOar(visual, 1f);

            rends = GetComponentsInChildren<Renderer>();
        }

        static Transform MakeOar(Transform parent, float side)
        {
            var pivot = new GameObject("Oar").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(side * 0.32f, 0.18f, -0.05f);
            Part(pivot, PrimitiveType.Cube, new Vector3(side * 0.32f, -0.06f, 0), new Vector3(0.72f, 0.035f, 0.05f), SeatWood, Quaternion.Euler(0, 0, -side * 16f));
            return pivot;
        }

        static void Part(Transform parent, PrimitiveType t, Vector3 lp, Vector3 s, Color c, Quaternion rot)
        {
            var g = GameObject.CreatePrimitive(t);
            var col = g.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localRotation = rot;
            g.transform.localScale = s;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = Palette.Mat(c);
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        // ================= Tìm bến E4 =================
        void FindPondDock(List<HouseEntity> houses)
        {
            hasDock = false;
            if (!pond || pond.Mask == null) return;
            foreach (var h in houses)
            {
                var fp = h.Footprint;
                if (!pond.WorldRect.Overlaps(new Rect(fp.c.x - 15f, fp.c.y - 15f, 30f, 30f))) continue;
                float best = 3f;
                Vector2 bestP = default;
                foreach (var cell in pond.Mask.Contour())
                {
                    var p = new Vector2(PondMask.Center(cell.x), PondMask.Center(cell.y));
                    float d = h.DistanceTo(p);
                    if (d < best) { best = d; bestP = p; }
                }
                if (best < 3f)
                {
                    var intoWater = (pond.Mask.Centroid() - bestP).normalized;
                    var candidate = bestP + intoWater * 3.1f;
                    if (SafeInPond(pond.Mask, candidate, 0.8f))
                    {
                        hasDock = true;
                        dockPos = candidate;
                        dockDir = new Vector2(-intoWater.y, intoWater.x);
                        return;
                    }
                }
            }
        }

        void FindRiverDock(List<HouseEntity> houses)
        {
            hasDock = false;
            if (!river || river.Points == null || river.Points.Count < 2) return;
            float hw = river.Width * 0.5f;
            foreach (var h in houses)
            {
                var fp = h.Footprint;
                if (!river.Area.Overlaps(new Rect(fp.c.x - 15f, fp.c.y - 15f, 30f, 30f))) continue;
                Geo.Closest(river.Points, fp.c, out _, out var q, out float centerDist);
                if (centerDist - hw <= 3f)
                {
                    float acc = 0f, bestD = float.MaxValue, bestArc = 0f;
                    var pts = river.Points;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        if (i > 0) acc += Vector2.Distance(pts[i - 1], pts[i]);
                        float d = Vector2.Distance(pts[i], q);
                        if (d < bestD) { bestD = d; bestArc = acc; }
                    }
                    if (bestArc > 4f && bestArc < river.Length - 4f)
                    {
                        hasDock = true;
                        dockRiverDist = bestArc;
                        return;
                    }
                }
            }
        }

        // ================= Cập nhật chuyển động thuyền =================
        void Update()
        {
            if (!HeightField.I) return;
            if (rends == null) rends = GetComponentsInChildren<Renderer>();

            // Mùa đông mặt nước đóng băng: ẩn thuyền
            bool show = Seasons.Snow < 0.5f && ((pond && pond.Mask != null) || (river && river.Points != null && river.Points.Count >= 2));
            if (rends.Length > 0 && rends[0].enabled != show)
                foreach (var r in rends) if (r) r.enabled = show;
            if (!show) return;

            float dt = Time.deltaTime;
            float t = Time.time;

            if (pond) UpdatePondBoat(dt, t);
            else if (river) UpdateRiverBoat(dt, t);

            // Lắc bập bềnh ±3° theo sin, nhịp theo mặt nước
            float pitch = Mathf.Sin(t * 2.1f + phase) * 3f;
            float roll = Mathf.Cos(t * 1.6f + phase * 1.3f) * 3f;
            if (visual) visual.localRotation = Quaternion.Euler(pitch, 0f, roll);

            // Nhịp mái chèo khi đang di chuyển
            bool rowing = dockStay <= 0f && waitTimer <= 0f;
            float oarAng = rowing ? Mathf.Sin(t * 3.4f + phase) * 28f : 0f;
            if (oarL) oarL.localRotation = Quaternion.Euler(0f, oarAng, 0f);
            if (oarR) oarR.localRotation = Quaternion.Euler(0f, -oarAng, 0f);
        }

        void UpdatePondBoat(float dt, float t)
        {
            var m = pond.Mask;
            if (m == null) return;

            // Đang đậu ở bến
            if (dockStay > 0f)
            {
                dockStay -= dt;
                if (dockStay <= 0f)
                {
                    dockCooldown = Random.Range(55f, 65f);
                    PickNextPondTarget();
                }
                ApplyPondTransform(t);
                return;
            }

            if (hasDock) dockCooldown -= dt;

            if (waitTimer > 0f)
            {
                waitTimer -= dt;
                ApplyPondTransform(t);
                return;
            }

            // Đến giờ ghé bến nếu đường từ vị trí hiện tại tới bến không vướng bờ
            bool goingToDock = false;
            Vector2 goal = pondTarget;
            if (hasDock && dockCooldown <= 0f && SegmentSafeInPond(m, pondPos, dockPos, 0.8f))
            {
                goal = dockPos;
                goingToDock = true;
            }

            var toGoal = goal - pondPos;
            float dist = toGoal.magnitude;
            if (dist < 0.35f)
            {
                if (goingToDock)
                {
                    dockStay = 10f;
                    if (dockDir.sqrMagnitude > 0.01f) pondDir = dockDir;
                }
                else
                {
                    waitTimer = Random.Range(0.5f, 1.8f);
                    PickNextPondTarget();
                }
                ApplyPondTransform(t);
                return;
            }

            var desiredDir = toGoal / dist;
            pondDir = Vector2.Lerp(pondDir, desiredDir, Mathf.Clamp01(dt * 2.2f)).normalized;
            if (pondDir.sqrMagnitude < 1e-4f) pondDir = desiredDir;

            float speed = 0.72f;
            var nextPos = pondPos + pondDir * (speed * dt);

            // Kiểm tra tránh bờ tuyệt đối bằng PondMask.ContainsWorld
            float reqMargin = goingToDock ? 0.75f : 1.15f;
            if (!SafeInPond(m, nextPos, reqMargin))
            {
                // Quẹo ngay về phía tâm ao hoặc điểm an toàn mới
                if (goingToDock) dockCooldown = Random.Range(25f, 40f);
                PickNextPondTarget();
                var safeDir = (pondTarget - pondPos);
                if (safeDir.sqrMagnitude > 1e-4f)
                {
                    pondDir = safeDir.normalized;
                    var tryPos = pondPos + pondDir * (speed * dt);
                    if (SafeInPond(m, tryPos, 0.75f)) pondPos = tryPos;
                }
            }
            else
            {
                pondPos = nextPos;
            }

            ApplyPondTransform(t);
        }

        void ApplyPondTransform(float t)
        {
            float y = pond.WaterLevel + Mathf.Sin(t * 2.4f + phase) * 0.02f;
            var dir3 = new Vector3(pondDir.x, 0f, pondDir.y);
            if (dir3.sqrMagnitude < 1e-4f) dir3 = Vector3.forward;
            transform.SetPositionAndRotation(new Vector3(pondPos.x, y, pondPos.y), Quaternion.LookRotation(dir3));
            transform.localScale = Vector3.one;
        }

        void PickNextPondTarget()
        {
            var m = pond ? pond.Mask : null;
            if (m == null) return;
            for (int k = 0; k < 24; k++)
            {
                float rx = PondMask.Center(m.x0 + Random.Range(2, Mathf.Max(3, m.w - 2)));
                float rz = PondMask.Center(m.z0 + Random.Range(2, Mathf.Max(3, m.h - 2)));
                var cand = new Vector2(rx, rz);
                if (Vector2.Distance(cand, pondPos) < 2f) continue;
                if (SafeInPond(m, cand, 1.3f) && SegmentSafeInPond(m, pondPos, cand, 1.0f))
                {
                    pondTarget = cand;
                    return;
                }
            }
            if (FindSafePondSpot(m, out var fallback)) pondTarget = fallback;
        }

        static bool FindSafePondSpot(PondMask m, out Vector2 spot)
        {
            var c = m.Centroid();
            if (SafeInPond(m, c, 1.3f)) { spot = c; return true; }
            for (int k = 0; k < 40; k++)
            {
                float rx = PondMask.Center(m.x0 + Random.Range(1, Mathf.Max(2, m.w - 1)));
                float rz = PondMask.Center(m.z0 + Random.Range(1, Mathf.Max(2, m.h - 1)));
                var cand = new Vector2(rx, rz);
                if (SafeInPond(m, cand, 1.1f)) { spot = cand; return true; }
            }
            spot = c;
            return m.ContainsWorld(c.x, c.y);
        }

        static bool SafeInPond(PondMask m, Vector2 p, float margin)
        {
            if (m == null || !m.ContainsWorld(p.x, p.y)) return false;
            const int Dirs = 8;
            for (int i = 0; i < Dirs; i++)
            {
                float a = i * (Mathf.PI * 2f / Dirs);
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (!m.ContainsWorld(p.x + d.x * margin, p.y + d.y * margin)) return false;
                if (!m.ContainsWorld(p.x + d.x * (margin * 0.5f), p.y + d.y * (margin * 0.5f))) return false;
            }
            return true;
        }

        static bool SegmentSafeInPond(PondMask m, Vector2 a, Vector2 b, float margin)
        {
            float dist = Vector2.Distance(a, b);
            int steps = Mathf.Max(2, Mathf.CeilToInt(dist / 0.5f));
            for (int i = 1; i <= steps; i++)
            {
                var q = Vector2.Lerp(a, b, (float)i / steps);
                if (!SafeInPond(m, q, margin)) return false;
            }
            return true;
        }

        void UpdateRiverBoat(float dt, float t)
        {
            var pts = river.Points;
            float total = river.Length;
            if (pts == null || pts.Count < 2 || total < 5f) return;

            if (dockStay > 0f)
            {
                dockStay -= dt;
                if (dockStay <= 0f) dockCooldown = Random.Range(55f, 65f);
            }
            else
            {
                if (hasDock) dockCooldown -= dt;
                float prev = riverDist;
                riverDist += 1.25f * dt;
                if (hasDock && dockCooldown <= 0f && prev < dockRiverDist && riverDist >= dockRiverDist)
                {
                    riverDist = dockRiverDist;
                    dockStay = 10f;
                }
                if (riverDist >= total) riverDist = 0f;
            }

            var p = Geo.PointAt(pts, riverDist);
            var pAhead = Geo.PointAt(pts, Mathf.Min(total, riverDist + 0.8f));
            var pBehind = Geo.PointAt(pts, Mathf.Max(0f, riverDist - 0.8f));
            var dir2 = (pAhead - pBehind).normalized;
            if (dir2.sqrMagnitude < 1e-4f) dir2 = Vector2.up;

            // Mờ dần / thu nhỏ ở đầu và cuối dòng chảy rồi xuất hiện lại ở đầu
            float fade = Mathf.Min(Mathf.Clamp01(riverDist / 3f), Mathf.Clamp01((total - riverDist) / 3f));
            float y = river.WaterLevelAt(p) + Mathf.Sin(t * 2.4f + phase) * 0.02f - (1f - fade) * 0.18f;

            transform.SetPositionAndRotation(new Vector3(p.x, y, p.y), Quaternion.LookRotation(new Vector3(dir2.x, 0f, dir2.y)));
            transform.localScale = Vector3.one * Mathf.Max(0.01f, fade);
        }

        // ================= Cá nhảy + vòng gợn nước =================
        public static void TickFish(Transform parent)
        {
            float dt = Time.deltaTime;

            // Cập nhật các con cá đang nhảy
            for (int i = activeFish.Count - 1; i >= 0; i--)
            {
                var f = activeFish[i];
                f.t += dt;
                float u = f.t / f.duration;
                if (u >= 1f)
                {
                    if (f.fish) Destroy(f.fish.gameObject);
                    if (f.ripple1) Destroy(f.ripple1.gameObject);
                    if (f.ripple2) Destroy(f.ripple2.gameObject);
                    activeFish.RemoveAt(i);
                    continue;
                }

                // Quỹ đạo vòng cung trong 0..0.65 thời gian đầu, phần còn lại chỉ còn vòng gợn lan ra
                float jumpU = Mathf.Clamp01(u / 0.65f);
                if (f.fish)
                {
                    if (jumpU < 1f)
                    {
                        var pos = Vector3.Lerp(f.p0, f.p1, jumpU);
                        pos.y = f.waterY + Mathf.Sin(jumpU * Mathf.PI) * 0.55f;
                        var nextU = Mathf.Min(1f, jumpU + 0.05f);
                        var nextPos = Vector3.Lerp(f.p0, f.p1, nextU);
                        nextPos.y = f.waterY + Mathf.Sin(nextU * Mathf.PI) * 0.55f;
                        var vel = nextPos - pos;
                        if (vel.sqrMagnitude > 1e-6f) f.fish.rotation = Quaternion.LookRotation(vel);
                        f.fish.position = pos;
                    }
                    else
                    {
                        f.fish.gameObject.SetActive(false);
                        if (!f.splashedEnd)
                        {
                            f.splashedEnd = true;
                            PlaySplashAt(f.p1, 0.22f);
                        }
                    }
                }

                // Vòng gợn lúc nhảy lên
                if (f.ripple1)
                {
                    float r1 = Mathf.Lerp(0.12f, 0.85f, u);
                    float h1 = Mathf.Max(0.002f, 0.018f * (1f - u));
                    f.ripple1.localScale = new Vector3(r1, h1, r1);
                }

                // Vòng gợn lúc rơi xuống nước
                if (f.ripple2)
                {
                    if (u >= 0.62f)
                    {
                        f.ripple2.gameObject.SetActive(true);
                        float u2 = Mathf.Clamp01((u - 0.62f) / 0.38f);
                        float r2 = Mathf.Lerp(0.12f, 0.9f, u2);
                        float h2 = Mathf.Max(0.002f, 0.02f * (1f - u2));
                        f.ripple2.localScale = new Vector3(r2, h2, r2);
                    }
                }

                activeFish[i] = f;
            }

            // Mùa đông không có cá nhảy
            if (Seasons.Snow > 0.5f || !parent || GameUI.Screen != UiScreen.Play) return;

            fishTimer -= dt;
            if (fishTimer > 0f) return;

            // Hoàng hôn (DayCycle.time ~ 0.72..0.90) cá nhảy nhiều hơn
            bool dusk = DayCycle.I && DayCycle.I.time >= 0.72f && DayCycle.I.time <= 0.90f;
            fishTimer = dusk ? Random.Range(3.2f, 7.5f) : Random.Range(6f, 15f);

            TrySpawnFish(parent);
        }

        static void TrySpawnFish(Transform parent)
        {
            var ponds = new List<PondEntity>();
            foreach (var p in EntityManager.OfType<PondEntity>())
                if (p.Mask != null && p.Mask.Area >= 12f) ponds.Add(p);

            var rivers = new List<RiverEntity>();
            foreach (var rv in EntityManager.OfType<RiverEntity>())
                if (rv.Points != null && rv.Points.Count >= 2 && rv.Length >= 6f) rivers.Add(rv);

            int total = ponds.Count + rivers.Count;
            if (total == 0) return;

            Vector3 p0 = default, p1 = default;
            float waterY = 0f;
            bool found = false;

            int pick = Random.Range(0, total);
            if (pick < ponds.Count)
            {
                var pond = ponds[pick];
                var m = pond.Mask;
                for (int k = 0; k < 20; k++)
                {
                    float rx = PondMask.Center(m.x0 + Random.Range(1, Mathf.Max(2, m.w - 1)));
                    float rz = PondMask.Center(m.z0 + Random.Range(1, Mathf.Max(2, m.h - 1)));
                    var a = new Vector2(rx, rz);
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    var b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.95f;
                    if (SafeInPond(m, a, 0.6f) && SafeInPond(m, b, 0.6f))
                    {
                        waterY = pond.WaterLevel;
                        p0 = new Vector3(a.x, waterY, a.y);
                        p1 = new Vector3(b.x, waterY, b.y);
                        found = true;
                        break;
                    }
                }
            }
            else
            {
                var rv = rivers[pick - ponds.Count];
                float len = rv.Length;
                float d0 = Random.Range(1.5f, Mathf.Max(2f, len - 2.5f));
                float d1 = Mathf.Clamp(d0 + Random.Range(-0.9f, 0.9f), 1f, len - 1f);
                var a = Geo.PointAt(rv.Points, d0);
                var b = Geo.PointAt(rv.Points, d1);
                if (Vector2.Distance(a, b) < 0.4f) b = a + new Vector2(0.6f, 0.3f);
                waterY = rv.WaterLevelAt(a);
                p0 = new Vector3(a.x, waterY, a.y);
                p1 = new Vector3(b.x, waterY, b.y);
                found = true;
            }

            if (!found) return;

            var fishRoot = new GameObject("JumpingFish").transform;
            fishRoot.SetParent(parent, false);
            fishRoot.position = p0;
            Part(fishRoot, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.08f, 0.11f, 0.28f), FishCol, Quaternion.identity);
            Part(fishRoot, PrimitiveType.Cube, new Vector3(0, 0, -0.15f), new Vector3(0.02f, 0.12f, 0.10f), Palette.Cream, Quaternion.Euler(45f, 0, 0));

            var r1 = MakeRipple(parent, p0 + Vector3.up * 0.02f);
            var r2 = MakeRipple(parent, p1 + Vector3.up * 0.02f);
            r2.gameObject.SetActive(false);

            PlaySplashAt(p0, 0.28f);

            activeFish.Add(new FishJump
            {
                fish = fishRoot,
                ripple1 = r1,
                ripple2 = r2,
                p0 = p0,
                p1 = p1,
                waterY = waterY,
                t = 0f,
                duration = 0.95f
            });
        }

        static Transform MakeRipple(Transform parent, Vector3 pos)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var col = g.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            g.name = "FishRipple";
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = new Vector3(0.12f, 0.015f, 0.12f);
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = Palette.Mat(RippleCol);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return g.transform;
        }

        static void PlaySplashAt(Vector3 pos, float baseVol)
        {
            var cam = Camera.main;
            if (!cam) { Sfx.Play(SfxId.Splash, Random.Range(0.95f, 1.25f), baseVol); return; }
            float dist = Vector3.Distance(cam.transform.position, pos);
            if (dist > 85f) return;
            float atten = Mathf.Clamp01(1f - dist / 85f);
            var vp = cam.WorldToViewportPoint(pos);
            float pan = Mathf.Clamp((vp.x - 0.5f) * 1.6f, -0.85f, 0.85f);
            Sfx.Play(SfxId.Splash, Random.Range(0.95f, 1.25f), baseVol * atten, pan);
        }
    }
}
