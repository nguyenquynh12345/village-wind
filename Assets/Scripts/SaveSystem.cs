using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LangGio
{
    /// <summary>
    /// Lưu / tải làng (JSON). Chỉ lưu dữ liệu gốc: độ cao đất, tham số mọi vật, khám phá, giờ, camera.
    /// Mesh luôn được sinh lại khi tải.
    /// </summary>
    public static class SaveSystem
    {
        [Serializable]
        class SaveEntity
        {
            public string type;
            public int id;
            public string json;
        }

        [Serializable]
        class SaveData
        {
            public int version = 1;
            public string name = "Làng của bạn";
            public string savedAt;
            public float time = 0.72f;
            public bool autoRun;
            public int season = 1, weather;   // mùa + thời tiết của riêng làng này
            public string terrain;
            public string groundPaint;
            public List<SaveEntity> entities = new();
            public List<bool> found = new();
            public List<string> hiddenDetails = new();
            public float yaw = 35f, pitch = 42f, dist = 38f;
            public Vector3 target;
        }

        static readonly string[] LoadOrder = { "WallGraph", "Blocks", "Pond", "House", "Path", "Tree", "Prop", "Ivy" };
        static string cachedInfo;

        static string Dir => Path.Combine(Application.persistentDataPath, "villages");
        public static string FilePath => Path.Combine(Dir, "village.json");
        public static bool HasSave => File.Exists(FilePath);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cachedInfo = null;

        /// <summary>Ví dụ: "Làng của bạn · lưu 14:32 09/11".</summary>
        public static string SaveInfo
        {
            get
            {
                if (cachedInfo != null) return cachedInfo;
                if (!HasSave) return cachedInfo = "";
                try
                {
                    var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                    cachedInfo = $"{d.name} · lưu {d.savedAt}";
                }
                catch { cachedInfo = ""; }
                return cachedInfo;
            }
        }

        public static void Save()
        {
            var d = new SaveData { savedAt = DateTime.Now.ToString("HH:mm dd/MM") };
            var hf = HeightField.I;
            var heights = hf.BaseHeights();
            var bytes = new byte[heights.Length * 4];
            Buffer.BlockCopy(heights, 0, bytes, 0, bytes.Length);
            d.terrain = Convert.ToBase64String(bytes);
            d.groundPaint = Convert.ToBase64String(hf.PaintData());

            foreach (var e in EntityManager.All)
                if (e) d.entities.Add(new SaveEntity { type = e.TypeName, id = e.Id, json = e.Capture() });

            if (Combos.I) { d.found.AddRange(Combos.I.Found); d.hiddenDetails.AddRange(Combos.I.Suppressed); }
            if (DayCycle.I) { d.time = DayCycle.I.time; d.autoRun = DayCycle.I.autoRun; }
            d.season = Settings.Season; d.weather = Settings.Weather;
            var rig = Camera.main ? Camera.main.GetComponent<CameraRig>() : null;
            if (rig) rig.GetState(out d.yaw, out d.pitch, out d.dist, out d.target);

            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(d));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
            cachedInfo = null;
        }

        public static bool Load()
        {
            if (!HasSave) return false;
            SaveData d;
            try { d = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)); }
            catch (Exception ex) { Debug.LogWarning("Không đọc được file lưu: " + ex.Message); return false; }

            ClearWorld();
            float[] heights = null;
            if (!string.IsNullOrEmpty(d.terrain))
            {
                var bytes = Convert.FromBase64String(d.terrain);
                heights = new float[bytes.Length / 4];
                Buffer.BlockCopy(bytes, 0, heights, 0, bytes.Length);
            }
            HeightField.I.SetBase(heights);
            HeightField.I.SetPaint(string.IsNullOrEmpty(d.groundPaint) ? null : Convert.FromBase64String(d.groundPaint));

            bool hasGraph = false, hasBlocks = false, hasIvy = false;
            foreach (var type in LoadOrder)
                foreach (var e in d.entities)
                    if (e.type == type)
                    {
                        EntityManager.Spawn(e.type, e.json, e.id);
                        if (type == "WallGraph") hasGraph = true;
                        if (type == "Blocks") hasBlocks = true;
                        if (type == "Ivy") hasIvy = true;
                    }
            if (!hasGraph) EntityManager.Spawn("WallGraph", "{}");
            if (!hasBlocks) EntityManager.Spawn("Blocks", "{}");
            if (!hasIvy) EntityManager.Spawn("Ivy", "{}");

            if (Combos.I) { Combos.I.SetFound(d.found); Combos.I.SetSuppressedList(d.hiddenDetails); }
            Settings.Season = d.season; Settings.Weather = d.weather;
            if (DayCycle.I) { DayCycle.I.autoRun = d.autoRun; DayCycle.I.SetTime(d.time); }
            var rig = Camera.main ? Camera.main.GetComponent<CameraRig>() : null;
            if (rig) rig.SetState(d.yaw, d.pitch, d.dist, d.target);
            History.Clear();
            return true;
        }

        public static void NewVillage(bool withLandscape = true)
        {
            ClearWorld();
            HeightField.I.Regenerate();
            EntityManager.Spawn("WallGraph", "{}");
            EntityManager.Spawn("Blocks", "{}");
            EntityManager.Spawn("Ivy", "{}");
            if (Combos.I) { Combos.I.SetFound(null); Combos.I.SetSuppressedList(null); }
            if (withLandscape) StartLandscape.Generate(UnityEngine.Random.Range(1, 100000));   // đồi, hồ, rừng có sẵn — chỉ việc xây nhà
            Settings.Season = 1; Settings.Weather = 0;
            if (DayCycle.I) { DayCycle.I.autoRun = false; DayCycle.I.SetTime(0.72f); }
            var rig = Camera.main ? Camera.main.GetComponent<CameraRig>() : null;
            if (rig) rig.SetState(35f, 42f, 38f, Vector3.zero);
            History.Clear();
        }

        static void ClearWorld()
        {
            if (ToolManager.I) ToolManager.I.ResetTools();
            EntityManager.Clear();
        }
    }
}
