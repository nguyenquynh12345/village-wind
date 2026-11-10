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
            public bool seasonAuto;
            public int season = 1, weather;   // mùa + thời tiết của riêng làng này
            public string terrain;
            public string groundPaint;
            public List<SaveEntity> entities = new();
            public List<bool> found = new();
            public List<string> hiddenDetails = new();
            public float yaw = 35f, pitch = 42f, dist = 38f;
            public Vector3 target;
        }

        static readonly string[] LoadOrder = { "WallGraph", "Blocks", "Pond", "River", "House", "Path", "Tree", "Prop", "Ivy" };
        static string cachedInfo;
        static string currentName = "Làng của bạn";
        static readonly List<SlotInfo> cachedSlots = new();
        static bool slotsDirty = true;
        static bool worldOrphaned;
        static readonly Dictionary<string, Texture2D> thumbCache = new();

        public struct SlotInfo
        {
            public string slot, name, savedAt;
            public DateTime modified;
            public int entityCount;
        }

        public static string CurrentSlot;

        static string Dir => Path.Combine(Application.persistentDataPath, "villages");
        static string TrashDir => Path.Combine(Dir, "trash");
        static string SlotJsonPath(string slot) => Path.Combine(Dir, slot + ".json");
        static string SlotPngPath(string slot) => Path.Combine(Dir, slot + ".png");

        public static string FilePath => SlotJsonPath(string.IsNullOrEmpty(CurrentSlot) ? (LatestSlot() ?? "village") : CurrentSlot);
        public static bool HasSave => ListSlots().Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cachedInfo = null;
            CurrentSlot = null;
            currentName = "Làng của bạn";
            cachedSlots.Clear();
            slotsDirty = true;
            worldOrphaned = false;
            foreach (var kv in thumbCache) if (kv.Value) UnityEngine.Object.Destroy(kv.Value);
            thumbCache.Clear();
        }

        public static void RefreshSlots()
        {
            slotsDirty = true;
            cachedInfo = null;
        }

        static string LatestSlot()
        {
            var list = ListSlots();
            if (list.Count == 0) return null;
            string last = PlayerPrefs.GetString("lastSlot", "");
            if (!string.IsNullOrEmpty(last))
                for (int i = 0; i < list.Count; i++)
                    if (list[i].slot == last) return last;
            return list[0].slot;
        }

        static string NewSlotId()
        {
            string baseId = $"lang-{DateTime.Now:yyyyMMdd-HHmmss}";
            string slot = baseId;
            int k = 2;
            while (File.Exists(SlotJsonPath(slot))) slot = $"{baseId}-{k++}";
            return slot;
        }

        static int CountEntities(List<SaveEntity> list)
        {
            if (list == null) return 0;
            int n = 0;
            foreach (var e in list)
                if (e != null && e.type != "WallGraph" && e.type != "Blocks" && e.type != "Ivy") n++;
            return n;
        }

        /// <summary>Danh sách các làng đã lưu, sắp theo thời gian sửa giảm dần.</summary>
        public static List<SlotInfo> ListSlots()
        {
            if (!slotsDirty) return cachedSlots;
            slotsDirty = false;
            cachedSlots.Clear();
            if (!Directory.Exists(Dir)) return cachedSlots;
            foreach (var f in Directory.GetFiles(Dir, "*.json"))
            {
                string slot = Path.GetFileNameWithoutExtension(f);
                if (string.IsNullOrEmpty(slot)) continue;
                try
                {
                    DateTime mod = File.GetLastWriteTime(f);
                    var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(f));
                    if (d == null) continue;
                    cachedSlots.Add(new SlotInfo
                    {
                        slot = slot,
                        name = string.IsNullOrEmpty(d.name) ? slot : d.name,
                        savedAt = string.IsNullOrEmpty(d.savedAt) ? mod.ToString("HH:mm dd/MM") : d.savedAt,
                        modified = mod,
                        entityCount = CountEntities(d.entities)
                    });
                }
                catch { }
            }
            cachedSlots.Sort((a, b) => b.modified.CompareTo(a.modified));
            return cachedSlots;
        }

        /// <summary>Ví dụ: "Làng của bạn · lưu 14:32 09/11".</summary>
        public static string SaveInfo
        {
            get
            {
                if (cachedInfo != null) return cachedInfo;
                string slot = LatestSlot();
                if (string.IsNullOrEmpty(slot)) return cachedInfo = "";
                var list = ListSlots();
                for (int i = 0; i < list.Count; i++)
                    if (list[i].slot == slot)
                        return cachedInfo = $"{list[i].name} · lưu {list[i].savedAt}";
                return cachedInfo = "";
            }
        }

        public static void Save(bool captureThumb = true)
        {
            if (worldOrphaned) return;
            if (string.IsNullOrEmpty(CurrentSlot)) CurrentSlot = NewSlotId();
            var d = new SaveData
            {
                name = string.IsNullOrEmpty(currentName) ? "Làng của bạn" : currentName,
                savedAt = DateTime.Now.ToString("HH:mm dd/MM")
            };
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
            d.season = Settings.Season; d.weather = Settings.Weather; d.seasonAuto = Settings.SeasonAuto;
            var rig = Camera.main ? Camera.main.GetComponent<CameraRig>() : null;
            if (rig) rig.GetState(out d.yaw, out d.pitch, out d.dist, out d.target);

            Directory.CreateDirectory(Dir);
            string path = SlotJsonPath(CurrentSlot);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(d));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            PlayerPrefs.SetString("lastSlot", CurrentSlot);
            PlayerPrefs.Save();
            if (captureThumb) CaptureThumb(CurrentSlot);
            RefreshSlots();
        }

        static void CaptureThumb(string slot)
        {
            try
            {
                var cam = Camera.main;
                if (!cam) return;
                var rt = RenderTexture.GetTemporary(320, 180, 24, RenderTextureFormat.ARGB32);
                var prev = cam.targetTexture;
                var active = RenderTexture.active;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prev;
                RenderTexture.active = rt;
                var tex = new Texture2D(320, 180, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes(SlotPngPath(slot), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
                if (thumbCache.TryGetValue(slot, out var old) && old) UnityEngine.Object.Destroy(old);
                thumbCache.Remove(slot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Không chụp được ảnh thu nhỏ: " + ex.Message);
            }
        }

        public static Texture2D LoadThumb(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return null;
            if (thumbCache.TryGetValue(slot, out var cached)) return cached;
            string p = SlotPngPath(slot);
            if (!File.Exists(p))
            {
                thumbCache[slot] = null;
                return null;
            }
            try
            {
                var bytes = File.ReadAllBytes(p);
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                if (!tex.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(tex);
                    thumbCache[slot] = null;
                    return null;
                }
                thumbCache[slot] = tex;
                return tex;
            }
            catch
            {
                thumbCache[slot] = null;
                return null;
            }
        }

        public static bool Load() => Load(LatestSlot());

        public static bool Load(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return false;
            string path = SlotJsonPath(slot);
            if (!File.Exists(path)) return false;
            SaveData d;
            try { d = JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
            catch (Exception ex) { Debug.LogWarning("Không đọc được file lưu: " + ex.Message); return false; }

            worldOrphaned = false;
            CurrentSlot = slot;
            currentName = string.IsNullOrEmpty(d.name) ? "Làng của bạn" : d.name;
            PlayerPrefs.SetString("lastSlot", slot);
            PlayerPrefs.Save();
            cachedInfo = null;

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
            Settings.Season = d.season; Settings.Weather = d.weather; Settings.SeasonAuto = d.seasonAuto;
            if (DayCycle.I) { DayCycle.I.autoRun = d.autoRun; DayCycle.I.SetTime(d.time); }
            var rig = Camera.main ? Camera.main.GetComponent<CameraRig>() : null;
            if (rig) rig.SetState(d.yaw, d.pitch, d.dist, d.target);
            History.Clear();
            return true;
        }

        public static void NewVillage(bool withLandscape = true) => NewVillage(null, withLandscape);

        public static void NewVillage(string name, bool withLandscape = true)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                var slots = ListSlots();
                int idx = slots.Count + 1;
                while (slots.Exists(s => s.name == $"Làng số {idx}")) idx++;
                name = $"Làng số {idx}";
            }
            worldOrphaned = false;
            CurrentSlot = NewSlotId();
            currentName = name.Trim();
            cachedInfo = null;

            ClearWorld();
            HeightField.I.Regenerate();
            EntityManager.Spawn("WallGraph", "{}");
            EntityManager.Spawn("Blocks", "{}");
            EntityManager.Spawn("Ivy", "{}");
            if (Combos.I) { Combos.I.SetFound(null); Combos.I.SetSuppressedList(null); }
            if (withLandscape) StartLandscape.Generate(UnityEngine.Random.Range(1, 100000));   // đồi, hồ, rừng có sẵn — chỉ việc xây nhà
            Settings.Season = 1; Settings.Weather = 0; Settings.SeasonAuto = false;
            if (DayCycle.I) { DayCycle.I.autoRun = false; DayCycle.I.SetTime(0.72f); }
            var rig = Camera.main ? Camera.main.GetComponent<CameraRig>() : null;
            if (rig) rig.SetState(35f, 42f, 38f, Vector3.zero);
            History.Clear();
            if (!AutoSaver.Disabled) Save(false);
            RefreshSlots();
        }

        public static void Rename(string slot, string newName)
        {
            if (string.IsNullOrEmpty(slot) || string.IsNullOrWhiteSpace(newName)) return;
            string path = SlotJsonPath(slot);
            if (!File.Exists(path)) return;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (d == null) return;
                d.name = newName.Trim();
                File.WriteAllText(path, JsonUtility.ToJson(d));
                if (CurrentSlot == slot) currentName = d.name;
                RefreshSlots();
            }
            catch (Exception ex) { Debug.LogWarning("Đổi tên làng thất bại: " + ex.Message); }
        }

        public static void Delete(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return;
            string json = SlotJsonPath(slot);
            if (!File.Exists(json)) return;
            try
            {
                Directory.CreateDirectory(TrashDir);
                string dstJson = Path.Combine(TrashDir, slot + ".json");
                if (File.Exists(dstJson)) File.Delete(dstJson);
                File.Move(json, dstJson);

                string png = SlotPngPath(slot);
                if (File.Exists(png))
                {
                    string dstPng = Path.Combine(TrashDir, slot + ".png");
                    if (File.Exists(dstPng)) File.Delete(dstPng);
                    File.Move(png, dstPng);
                }

                if (thumbCache.TryGetValue(slot, out var t) && t) UnityEngine.Object.Destroy(t);
                thumbCache.Remove(slot);
                if (CurrentSlot == slot)
                {
                    CurrentSlot = null;
                    worldOrphaned = true;
                    if (AutoSaver.I) AutoSaver.I.ClearDirty();
                }
                if (PlayerPrefs.GetString("lastSlot", "") == slot) { PlayerPrefs.DeleteKey("lastSlot"); PlayerPrefs.Save(); }
                RefreshSlots();
            }
            catch (Exception ex) { Debug.LogWarning("Xoá làng thất bại: " + ex.Message); }
        }

        public static void Duplicate(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return;
            string srcJson = SlotJsonPath(slot);
            if (!File.Exists(srcJson)) return;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(srcJson));
                if (d == null) return;
                d.name = (string.IsNullOrEmpty(d.name) ? slot : d.name) + " (bản sao)";
                d.savedAt = DateTime.Now.ToString("HH:mm dd/MM");
                string newSlot = NewSlotId();
                File.WriteAllText(SlotJsonPath(newSlot), JsonUtility.ToJson(d));

                string srcPng = SlotPngPath(slot);
                if (File.Exists(srcPng)) File.Copy(srcPng, SlotPngPath(newSlot), true);
                RefreshSlots();
            }
            catch (Exception ex) { Debug.LogWarning("Nhân bản làng thất bại: " + ex.Message); }
        }

        static void ClearWorld()
        {
            if (ToolManager.I) ToolManager.I.ResetTools();
            EntityManager.Clear();
        }
    }
}
