using UnityEngine;
using UnityEngine.Rendering;

namespace LangGio
{
    /// <summary>
    /// Không khí sống động quanh chỗ camera đang nhìn:
    /// ban ngày cánh hoa / lá bay theo gió, ban đêm đom đóm lập loè. Chạy hoàn toàn bằng ParticleSystem.
    /// </summary>
    public class AmbientFx : MonoBehaviour
    {
        static AmbientFx I;
        ParticleSystem petals, fireflies, dust, rain, smoke;
        CameraRig rig;
        Material fireflyMat;

        void Start()
        {
            I = this;
            rig = FindAnyObjectByType<CameraRig>();
            dust = MakeDust();
            rain = MakeRain();
            smoke = MakeSmoke();
            petals = MakePetals();
            fireflies = MakeFireflies();
        }

        void Update()
        {
            if (!Settings.Decor)
            {
                SetRate(petals, 0); SetRate(fireflies, 0);
                return;
            }
            Vector3 c = rig ? rig.target : Vector3.zero;
            if (petals) petals.transform.position = c + Vector3.up * 7f;
            if (fireflies) fireflies.transform.position = c + Vector3.up * 1.2f;
            if (rain) rain.transform.position = c + Vector3.up * 14f;
            float rainK = Settings.Weather == 2 ? Seasons.Rain : 0f;   // tạnh là ngừng rơi ngay (đất vẫn ướt một lúc)

            bool night = DayCycle.IsNight, evening = DayCycle.IsEvening;
            int season = Settings.Season;
            if (season != shownSeason) ApplySeason(season);
            SetRate(rain, season == 3 ? 0f : rainK * 900f);
            if (season == 3) SetRate(petals, 70f + rainK * 260f);           // tuyết rơi (dày hơn khi trời "mưa")
            else if (rainK > 0.3f) SetRate(petals, 0f);
            else SetRate(petals, night ? 0f : evening ? 2f : season == 2 ? 9f : 5f);
            SetRate(fireflies, season == 3 || rainK > 0.3f ? 0f : night ? 14f : evening ? 4f : 0f);

            if (fireflyMat)   // đom đóm nhấp nháy đồng loạt nhẹ
                Palette.Glow(fireflyMat, Palette.Hex("#D9FF7A"), 2.2f + Mathf.Sin(Time.time * 3.1f) * 0.6f);
        }

        static Mesh sphere;
        static void UseSphere(ParticleSystem ps)
        {
            if (!sphere)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere = g.GetComponent<MeshFilter>().sharedMesh;
                Destroy(g);
            }
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = sphere;
        }

        int shownSeason = -1;

        static Gradient Grad(params string[] hex)
        {
            var g = new Gradient();
            var keys = new GradientColorKey[hex.Length];
            for (int k = 0; k < hex.Length; k++) keys[k] = new GradientColorKey(Palette.Hex(hex[k]), hex.Length == 1 ? 0f : k / (float)(hex.Length - 1));
            g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        /// <summary>Cánh hoa (xuân/hạ) · lá vàng đỏ (thu) · bông tuyết (đông).</summary>
        void ApplySeason(int season)
        {
            shownSeason = season;
            if (!petals) return;
            var main = petals.main;
            var vel = petals.velocityOverLifetime;
            Gradient g = season switch
            {
                2 => Grad("#F2A23A", "#D9572B", "#E8C14F", "#B5462A"),
                3 => Grad("#FFFFFF", "#EEF4FF"),
                _ => Grad("#F6C9D4", "#FFF4E0", "#F4D06F", "#9CC26A"),
            };
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            bool snowing = season == 3;
            main.startSize = snowing ? new ParticleSystem.MinMaxCurve(0.04f, 0.09f) : new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            main.maxParticles = snowing ? 1200 : 300;
            vel.x = snowing ? new ParticleSystem.MinMaxCurve(0.1f, 0.4f) : new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            vel.y = snowing ? new ParticleSystem.MinMaxCurve(-0.9f, -0.5f) : new ParticleSystem.MinMaxCurve(-0.25f, -0.1f);
        }

        static void SetRate(ParticleSystem ps, float r)
        {
            if (!ps) return;
            var e = ps.emission;
            e.rateOverTime = Settings.Lite ? r * 0.4f : r;
        }

        ParticleSystem Base(string name, Material mat, out ParticleSystem.MainModule main)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.maxParticles = 300;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        ParticleSystem MakePetals()
        {
            var petalMat = new Material(Palette.VertexColorMat()) { name = "Petal" };
            Palette.Glow(petalMat, Palette.Hex("#FFF1E6"), 0.35f);   // không bị đen khi ngược sáng trên nền trời
            var ps = Base("Petals", petalMat, out var main);
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.015f;

            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(Palette.Hex("#F6C9D4"), 0f), new GradientColorKey(Palette.Hex("#FFF4E0"), 0.35f),
                    new GradientColorKey(Palette.Hex("#F4D06F"), 0.6f), new GradientColorKey(Palette.Hex("#9CC26A"), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(46f, 6f, 46f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.25f, -0.1f);
            vel.z = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.3f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));

            ps.Play();
            return ps;
        }

        /// <summary>Bụi mịn bung ra dọc chân vật vừa đặt (gọi khi người chơi xây).</summary>
        public static void DustAround(GameObject go)
        {
            if (!I || !I.dust || !go || !Settings.Decor) return;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float ground = b.min.y;
            float per = Mathf.Clamp(b.size.x + b.size.z, 1f, 24f);
            int n = Mathf.RoundToInt(per * 2.5f);
            var ep = new ParticleSystem.EmitParams();
            for (int k = 0; k < n; k++)
            {
                // điểm ngẫu nhiên trên chu vi hộp bao, sát mặt đất
                float u = Random.value * 4f;
                float fx = u < 1f ? u : u < 2f ? 1f : u < 3f ? 3f - u : 0f;
                float fz = u < 1f ? 0f : u < 2f ? u - 1f : u < 3f ? 1f : 4f - u;
                var p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, fx), ground + 0.1f, Mathf.Lerp(b.min.z, b.max.z, fz));
                var outDir = new Vector3(p.x - b.center.x, 0, p.z - b.center.z).normalized;
                ep.position = p;
                ep.velocity = outDir * Random.Range(0.6f, 1.6f) + Vector3.up * Random.Range(0.3f, 0.9f);
                ep.startSize = Random.Range(0.25f, 0.55f);
                ep.startLifetime = Random.Range(0.6f, 1.1f);
                I.dust.Emit(ep, 1);
            }
        }

        /// <summary>Một cụm khói ống khói: bốc lên, nở to, bị gió đẩy nghiêng rồi tan.</summary>
        public static bool Smoke(Vector3 pos, float scale = 1f)
        {
            if (!I || !I.smoke) return false;
            var ep = new ParticleSystem.EmitParams
            {
                position = pos + new Vector3(Random.Range(-0.05f, 0.05f), 0, Random.Range(-0.05f, 0.05f)),
                velocity = new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.7f, 0.95f), Random.Range(-0.08f, 0.08f)),
                startSize = Random.Range(0.35f, 0.5f) * scale,
                startLifetime = Random.Range(4.5f, 6f),
                rotation3D = new Vector3(Random.value, Random.value, Random.value) * 360f,
            };
            I.smoke.Emit(ep, 1);
            return true;
        }

        ParticleSystem MakeSmoke()
        {
            var m = new Material(Palette.Mat(Palette.Hex("#E4E0DA"))) { name = "Smoke" };
            Palette.Glow(m, Palette.Hex("#B8B4AE"), 0.3f);
            var ps = Base("Smoke", m, out var main);
            UseSphere(ps);
            main.maxParticles = 600;
            main.startSpeed = 0f;
            var e = ps.emission;
            e.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.6f, new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));   // gió đẩy dần sang ngang
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(0.25f, new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.6f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.5f, 1.6f), new Keyframe(0.85f, 1.9f), new Keyframe(1f, 0f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.9f, 0.92f, 0.96f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            col.color = g;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeRain()
        {
            var m = new Material(Palette.Mat(Palette.Hex("#C9D6E2"))) { name = "Rain" };
            Palette.Glow(m, Palette.Hex("#9FB0C0"), 0.35f);
            var ps = Base("Rain", m, out var main);
            main.maxParticles = 4000;
            main.startLifetime = 1.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.03f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 1f, 50f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(1.2f, 1.6f);
            vel.y = new ParticleSystem.MinMaxCurve(-12f, -10f);
            vel.z = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f;
            r.lengthScale = 1f;
            var e = ps.emission;
            e.rateOverTime = 0f;
            ps.Play();
            return ps;
        }

        ParticleSystem MakeDust()
        {
            var m = new Material(Palette.Mat(Palette.Hex("#E9E1D2"))) { name = "Dust" };
            Palette.Glow(m, Palette.Hex("#F3ECDF"), 0.25f);
            var ps = Base("Dust", m, out var main);
            UseSphere(ps);
            main.startSpeed = 0f;
            main.gravityModifier = -0.02f;
            var e = ps.emission;
            e.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            ps.Play();
            return ps;
        }

        ParticleSystem MakeFireflies()
        {
            fireflyMat = new Material(Palette.LanternMat()) { name = "Firefly" };
            fireflyMat.color = Palette.Hex("#E8FF9A");
            var ps = Base("Fireflies", fireflyMat, out var main);
            UseSphere(ps);
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 2.2f, 36f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.9f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.4f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.35f, 0.3f), new Keyframe(0.55f, 1f),
                new Keyframe(0.75f, 0.4f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0f)));

            ps.Play();
            return ps;
        }
    }
}
