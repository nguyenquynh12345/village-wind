# Làng Gió — Kế hoạch nâng cấp v0.5

> Tài liệu giao việc cho AI/dev khác làm. Đọc **toàn bộ Phần 0** trước khi viết dòng code nào.
> Đặc tả gốc: `Docs/CoChe-Gameplay.md` (v0.4). Engine: **Unity 6000.1.12f1**, URP, C#, namespace `LangGio`.

---

## Phần 0 — Luật bắt buộc (đọc trước)

### 0.1 Kiến trúc hiện có (không được phá)
| Thành phần | File | Ghi chú |
|---|---|---|
| Vật xây được | `Assets/Scripts/World/Entity.cs` | Kế thừa `Entity<TParams>`. Chỉ lưu **tham số** ([Serializable], đọc/ghi bằng `JsonUtility`). Hình luôn sinh lại trong `Rebuild()` qua `NewVisual()` |
| Đăng ký vật | `World/EntityManager.cs` | Thêm loại mới = thêm 1 dòng vào dict `types` |
| Hoàn tác | `World/Commands.cs`, `History.cs` | **Mọi** thay đổi thế giới đi qua `ICommand` + `History.Execute/Push`. Nhiều thay đổi cùng lúc → `CompositeCommand` |
| Lưu / tải | `SaveSystem.cs` | Lưu JSON. Thêm loại vật mới → thêm vào `LoadOrder` |
| Công cụ | `Tools/Tool.cs`, `Tools/ToolManager.cs` | Thêm công cụ = lớp kế thừa `Tool` + đăng ký trong `ToolManager.Awake` + phím tắt |
| Địa hình | `World/HeightField.cs` | Mọi độ cao lấy qua `HeightField.I.HeightAt(x, z)`. Đào đất không sửa đất gốc mà dùng lớp "carve" (xem `PondEntity.CarveAt`) |
| Tổ hợp | `World/Combos.cs` | `Combos.Count = 15`, mảng `Names/Hints/Descs`, `Found[]` |
| UI | `GameUI.cs`, `UiKit.cs` | IMGUI vẽ tay (`OnGUI`). Dùng lại `UiKit.Panel/BigButton/Chip/Label/Icon`, **không** thêm Canvas/UGUI |
| Âm thanh | `Sfx.cs` | Âm tự tổng hợp, `Sfx.Play(SfxId.X, pitch, vol)` |
| Số liệu | `Core/GameConfig.cs` | Mọi con số "cảm giác" để ở đây, không viết cứng |

### 0.2 Quy tắc code
1. **Không** sửa/xoá file `.meta`, `Library/`, `ProjectSettings/` trừ khi bước đó nói rõ. File `.cs` mới: để Unity tự sinh `.meta`.
2. **Không** thêm package, asset ngoài, model, texture. Mọi hình dựng bằng code (`Prim`, `MeshObj`, `MeshBuilder`) như các Entity hiện có.
3. Chữ hiển thị cho người chơi: **tiếng Việt có dấu**. Comment: tiếng Việt, ngắn, giống style hiện tại.
4. Mỗi lớp static có trạng thái phải có `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] static void ResetStatics()` (xem `History.cs`).
5. **File lưu cũ phải tải được.** Thêm field mới vào `SaveData`/`Params` luôn có giá trị mặc định hợp lý; tăng `version` khi đổi cấu trúc.
6. Mỗi bước xong phải: biên dịch không lỗi, không warning mới, chạy đúng "Tiêu chí xong" của bước đó.
7. Làm **từng bước một theo thứ tự**, xong bước nào báo bước đó (liệt kê file đã sửa + cách thử). Không gộp nhiều bước vào 1 lần.

### 0.3 Phím đang dùng (tránh trùng)
`V B G K H O T P U X N L D W A I J F F1 F2 Q E Z Y R Delete Esc [ ] 1–4 PageUp/PageDown`, Ctrl+S, Ctrl+D, Ctrl+Z, mũi tên.
Còn trống dùng được: `C M` (kiểm tra lại bằng grep `KeyCode.` trước khi dùng), `F3`, `F4`, Ctrl+C, Ctrl+V.

---

## Thứ tự làm

| Bước | Tính năng | Độ khó | Phụ thuộc |
|---|---|---|---|
| **S1** | Nhiều làng (ô lưu + ảnh thu nhỏ) | Dễ | — |
| **S2** | Chọn nhiều vật + sao chép / dán | Vừa | — |
| **S3** | Sông / suối | Khó | — |
| **S4** | Thuyền + cá | Vừa | S3 (cá/thuyền dùng được cả ao, nên làm được trước S3 nếu muốn) |
| **S5** | Chế độ dạo làng (đi bộ / ngồi thuyền) | Vừa | S4 cho phần ngồi thuyền |
| **S6** | Mùa tự trôi + lá rơi / tuyết đọng | Dễ | — |
| **S7** | Tổ hợp mới E16–E21 + Sổ khám phá 2 trang | Vừa | S3, S4 |
| **S8** | Hoàn thiện việc dở: mờ dần 0.3 s nội thất, M15 cho tường/tháp | Vừa | — |

---

## S1 — Nhiều làng (ô lưu)

**Mục tiêu:** người chơi có nhiều làng, mỗi làng 1 file, menu chính hiện danh sách kèm ảnh thu nhỏ.

**Sửa `SaveSystem.cs`:**
- Bỏ đường dẫn cố định `village.json`. Thêm `public static string CurrentSlot` (tên file không đuôi, ví dụ `lang-20261110-143200`).
- `FilePath => Path.Combine(Dir, CurrentSlot + ".json")`, ảnh thu nhỏ `CurrentSlot + ".png"` cùng thư mục.
- **File cũ:** `village.json` sẵn có tự thành 1 slot tên `village` (không đổi tên, không xoá) — `ListSlots()` chỉ cần quét mọi `*.json` trong thư mục.
- Thêm API:
  ```csharp
  public struct SlotInfo { public string slot, name, savedAt; public DateTime modified; public int entityCount; }
  public static List<SlotInfo> ListSlots();            // sắp theo modified giảm dần
  public static bool Load(string slot);
  public static void NewVillage(string name, bool withLandscape = true); // tạo slot mới, đặt CurrentSlot
  public static void Rename(string slot, string newName); // chỉ đổi field name trong JSON
  public static void Delete(string slot);              // chuyển vào thư mục villages/trash/, KHÔNG xoá hẳn
  public static void Duplicate(string slot);
  public static Texture2D LoadThumb(string slot);      // cache theo slot + thời gian sửa file
  ```
- `SaveInfo`/`HasSave` giữ lại cho tương thích (trỏ vào slot mới nhất).
- Lưu `PlayerPrefs "lastSlot"` để nút "Tiếp tục" mở đúng làng gần nhất.

**Ảnh thu nhỏ:** khi `Save()`, chụp `Camera.main` vào `RenderTexture` 320×180 (tắt UI — UI là IMGUI nên render camera thẳng là không dính UI), `ReadPixels` → `EncodeToPNG` → ghi file. Bọc try/catch, lỗi chụp ảnh không được làm hỏng việc lưu.

**Sửa `GameUI.DrawMain`:**
- Nút "Tiếp tục" (làng gần nhất) giữ nguyên.
- Thêm nút "Các làng" → màn hình mới (thêm `UiScreen.Villages`): lưới thẻ 3 cột, mỗi thẻ: ảnh, tên, "lưu HH:mm dd/MM", số công trình. Click thẻ = mở. Mỗi thẻ có chip nhỏ: Đổi tên · Nhân bản · Xoá (xoá phải hỏi lại "Chắc chưa?" bằng lần bấm thứ 2).
- "Làng mới" → tạo slot mới, tên mặc định "Làng số N".
- Đổi tên: ô nhập `GUI.TextField` trong UiKit style.

**`AutoSaver`:** không đổi logic, chỉ lưu vào `CurrentSlot`.

**Tiêu chí xong:**
- Có file `village.json` cũ → mở game vẫn thấy làng cũ trong danh sách.
- Tạo 3 làng, mỗi làng xây khác nhau, thoát game, mở lại → đủ 3 làng, ảnh đúng, mở từng làng đúng nội dung.
- Xoá 1 làng → file nằm trong `villages/trash/`.

---

## S2 — Chọn nhiều vật + sao chép / dán

**Mục tiêu:** chọn cả cụm (nhà + cây + đường…), di chuyển / xoay / xoá / nhân bản cùng lúc; Ctrl+C / Ctrl+V để dán cụm ở chỗ khác.

**Sửa `Tools/HandTool.cs`:**
- Trạng thái mới: `List<int> multiSel` (ngoài `selId` đơn hiện có). Khi `multiSel.Count > 0` thì không hiện tay nắm của từng vật.
- **Ctrl + kéo trên chỗ trống** → vẽ khung chữ nhật màn hình (vẽ bằng `UiKit.Outline`), thả → chọn mọi Entity có tâm `bounds` chiếu lên màn hình nằm trong khung. Bỏ qua các Entity "toàn cục": `WallGraph`, `Blocks`, `Ivy` (chúng là 1 entity chứa cả thế giới — chỉ chọn được bằng click như cũ).
- **Ctrl + click** vật → thêm/bớt khỏi `multiSel`.
- Tô sáng mọi vật trong `multiSel` bằng `SetTint`.
- Kéo thân 1 vật trong nhóm → `Translate` cả nhóm. Thả chuột → **1** `CompositeCommand` gồm `ModifyEntityCommand` cho từng vật.
- `R` → xoay cả nhóm quanh tâm nhóm: với mỗi vật gọi `Rotate(deg)` rồi `Translate` phần dời do xoay quanh tâm chung (vật nào `CanRotate == false` thì chỉ dời vị trí).
- `Delete` / `Ctrl+D` áp cho cả nhóm, 1 bước hoàn tác.

**Bộ nhớ tạm (file mới `World/Clipboard.cs`):**
- `Copy(IEnumerable<Entity>)`: lưu danh sách `(TypeName, Capture())` + tâm nhóm.
- `Ctrl+V`: hiện bóng mờ (preview) của nhóm theo chuột trên mặt đất; click để đặt → `CompositeCommand` gồm các `CreateEntityCommand` (JSON đã dịch vị trí). Cách dịch: Spawn tạm rồi gọi `Translate(delta)` rồi `Capture()` lại — **không** tự sửa JSON bằng tay.
- Esc / chuột phải = huỷ dán.
- Dán chồng lên nhà khác: cho phép, không chặn (giữ đơn giản), nhưng preview tô đỏ nếu tâm nhà mới chồng nhà cũ.

**Tiêu chí xong:** Ctrl+kéo chọn 2 nhà + 5 cây → kéo đi 10 m → Z một lần → về chỗ cũ. Ctrl+C, Ctrl+V ở chỗ khác → cụm mới xuất hiện có hiệu ứng nảy; Z một lần → cụm mới biến mất.

---

## S3 — Sông / suối

**Mục tiêu:** vẽ nét như đường đi → thành dòng nước chảy, lòng sông tự đào, bờ có đá và lau sậy; nối vào ao thì hòa vào ao.

**Entity mới `World/RiverEntity.cs`** (`TypeName = "River"`):
```csharp
[Serializable] public class RiverParams {
    public List<Vector2> pts = new();   // đường giữa đã làm mượt (dùng lại PathUtil / Geo như PathEntity)
    public float width = 3f;            // 1.5 .. 8 m
    public bool deep;                   // nông 0.4 / sâu 1.0
    public int seed;
}
```
- **Đào lòng sông:** làm giống `PondEntity` — thêm hàm `CarveAt(x, z)` (sâu ở giữa, thoải ra bờ 1 m) và đăng ký vào chỗ `HeightField.RecomputeCarve` đang gọi `PondEntity.CarveAt` (tìm trong `HeightField.cs`, cộng dồn lấy **max** độ đào của ao và sông).
- **Mặt nước dốc theo địa hình:** mỗi điểm dọc sông có mực nước = `min(bờ trái, bờ phải) − 0.1`, rồi ép **không tăng** theo chiều chảy (từ `pts[0]` tới cuối). Chiều chảy = từ đầu cao hơn tới đầu thấp hơn (tự đảo `pts` nếu cần khi `Rebuild`).
- **Mesh mặt nước:** dải tam giác dọc đường giữa, UV.y chạy theo chiều dài để shader cho nước "chảy". Dùng `Shaders/Water.shader` hiện có, thêm property `_FlowSpeed` (mặc định 0 = ao đứng yên, sông = 0.6) cuộn UV theo thời gian. Không phá ao.
- **Chi tiết tự sinh:** đá bờ mỗi ~2 m, lau sậy 30% đoạn bờ (theo seed) — chép cách làm của `PondEntity`.
- **Đầu sông chạm ao** (≤ 1 m) → bỏ nắp đầu sông, mực nước khớp ao.
- **Tay nắm:** giống `PathEntity` (2 đầu + giữa đoạn), 🔼 bề rộng, 🔶 nông/sâu.

**Công cụ:** **không** thêm phím mới. Trong `Tools/PondTool.cs` thêm chế độ: phím `1` = Ao (như cũ), `2` = Sông. Chế độ sông vẽ nét giống `PathTool` (có thể dùng chung code lấy mẫu nét).

**Tương tác có sẵn cần cập nhật:**
- Cây, nhà không mọc/đặt trên mặt sông: tìm nơi kiểm tra "có nằm trên ao không" (grep `PondEntity` trong `TreeEntity.cs`, `HouseTool.cs`, `Fauna.cs`) và thêm kiểm tra sông.
- E2 (cầu gỗ) và E3 (cầu đá) trong `Combos.cs`: hiện kiểm tra đường/tường cắt ao → mở rộng cho cả sông.
- `EraseTool`: click sông = xoá cả sông.
- `SaveSystem.LoadOrder`: thêm `"River"` **sau** `"Pond"`.

**Tiêu chí xong:** vẽ sông từ đồi xuống hồ → lòng sông lõm, nước thấy chảy xuống dốc, đổ vào hồ không có mép hở. Vẽ đường cắt ngang sông → cầu gỗ mọc. Z → sông biến mất, đất trở lại như cũ.

---

## S4 — Thuyền + cá

**Thuyền** (file mới `World/Boat.cs`, giống `Duck.cs`/`Fauna.cs` — **không** phải Entity, không lưu):
- Tự sinh: mỗi ao ≥ 120 m² có 1 thuyền; mỗi sông dài ≥ 40 m có 1 thuyền.
- Hình: thân thuyền gỗ dựng bằng 3–4 khối, 1 người chèo khối nhỏ (hoặc chỉ mái chèo).
- Ao: trôi chậm theo vòng ngẫu nhiên trong mặt nạ ao, tránh bờ (dùng `PondMask.ContainsWorld`). Sông: trôi xuôi dòng, tới cuối thì mờ dần và xuất hiện lại ở đầu.
- Bập bềnh: lắc ±3° theo sin, nhịp theo mặt nước.
- Nếu có bến (E4) thì thuyền thỉnh thoảng (mỗi ~60 s) ghé đậu ở bến 10 s.

**Cá:** cá nhảy khỏi mặt nước mỗi 6–15 s ở ao/sông bất kỳ, nhiều hơn lúc hoàng hôn (`DayCycle`). Một khối nhỏ bay vòng cung + vòng gợn nước + `Sfx` tiếng "tõm" nhỏ (thêm `SfxId.Splash` tổng hợp trong `Sfx.cs`). Mùa đông (`Seasons.Snow > 0.5`) không có cá.

**Tiêu chí xong:** ao 150 m² có thuyền trôi không bao giờ lên bờ; sông có thuyền xuôi dòng; ngồi xem 1 phút thấy cá nhảy vài lần.

---

## S5 — Chế độ dạo làng

**Mục tiêu:** phần thưởng ngắm làng mình xây từ góc người đi bộ.

**File mới `WalkMode.cs`** (MonoBehaviour, gắn cạnh `CameraRig` trong `Bootstrap.cs`):
- Bật/tắt: phím **F3** (khi đang chơi, không có công cụ Busy) hoặc nút trên thanh dưới. Tắt bằng F3 / Esc.
- Khi bật: tắt `CameraRig` và `ToolManager` (làm giống cách `GameUI.PhotoMode` đang chặn công cụ), ẩn UI trừ 1 dòng hướng dẫn mờ.
- **Đi bộ:** WASD + chuột xoay đầu (khóa con trỏ), Shift chạy. Mắt cao 1.6 m trên `HeightField.HeightAt`. Va chạm: không đi xuyên nhà, tường, tháp (raycast/capsule với collider đã có của Entity), không xuống nước sâu hơn 0.3 m. Giới hạn trong `GameConfig.BuildRadius`.
- **Điểm bắt đầu:** trước cửa ngôi nhà gần tâm màn hình nhất; không có nhà → giữa màn hình.
- **Ngồi thuyền (nếu S4 xong):** đứng cách thuyền ≤ 3 m nhấn `E` → camera gắn lên thuyền, chuột vẫn nhìn quanh, `E` lần nữa để lên bờ gần nhất.
- Tiếng bước chân tổng hợp (`SfxId.Step`), đổi âm theo mặt đất (đường / cỏ / tuyết).
- Thêm số vào `GameConfig`: `WalkSpeed = 3.2f`, `RunSpeed = 6f`, `EyeHeight = 1.6f`.

**Tiêu chí xong:** F3 → đang đứng trước cửa nhà; đi quanh làng không xuyên tường; Esc → camera về đúng vị trí cũ.

---

## S6 — Mùa tự trôi

**Sửa `Settings.cs` + `World/Seasons.cs`:**
- Thêm `Settings.SeasonAuto` (bool) và `Settings.SeasonMinutes` (mặc định 8 phút/mùa). Bật thì sau mỗi `SeasonMinutes` mùa tự sang mùa kế (dùng `Time.unscaledDeltaTime`, chỉ đếm khi `GameUI.Screen == Play`).
- Thời tiết tự đổi ngẫu nhiên mỗi 2–5 phút khi bật auto, xác suất theo mùa (hạ: nắng nhiều; thu: mây; đông: không mưa mà tuyết — tuyết đã có qua `_LG_Snow`).
- Toast nhỏ khi đổi mùa: "🍂 Sang thu".
- Lưu `seasonAuto` theo làng trong `SaveData` (giống `season`, `weather`).

**Hiệu ứng thêm** (trong `World/AmbientFx.cs`, dùng hạt có sẵn):
- Thu: lá vàng/cam rơi lác đác quanh cây (chỉ cây trong 40 m quanh camera).
- Đông: khói ống khói nhiều hơn, mặt ao đóng băng (đổi màu nước sang trắng xanh qua biến shader `_LG_Snow` trong `Water.shader`), vịt/thuyền/cá ẩn.

**Tiêu chí xong:** bật auto với 1 phút/mùa → trong 4 phút thấy đủ 4 mùa chuyển mượt; mùa đông ao đóng băng, thuyền biến mất; lưu và tải lại giữ đúng chế độ auto.

---

## S7 — Tổ hợp mới E16–E21

**Sửa `World/Combos.cs`:** nâng `Count` lên 21, thêm vào `Names/Hints/Descs` và `GameUI.JournalIcons/JournalTints`.

**Tương thích file lưu:** `SetFound(list)` phải chấp nhận list ngắn hơn `Count` (phần thiếu = false).

| # | Tên | Điều kiện | Kết quả | Gợi ý trong sổ |
|---|---|---|---|---|
| E16 | Cối xay nước | Nhà cách sông ≤ 2 m | Bánh xe nước quay ở cạnh nhà phía sông | "Nhà bên dòng nước chảy…" |
| E17 | Cối xay gió | Nhà 1–2 tầng trên điểm cao hơn trung bình làng ≥ 5 m | Cánh quạt gió trên mái, quay | "Trên đỉnh đồi lộng gió…" |
| E18 | Chợ phiên | ≥ 5 nhà trong 20 m, có chỗ trống ≥ 8×8 m nối đường | 3–4 sạp hàng có mái vải màu (ban ngày), dọn đi ban đêm | "Đông nhà quá, cần chỗ họp chợ" |
| E19 | Đền đỉnh đồi | Tháp/nhà nằm trên đỉnh đồi cao nhất làng, cao ≥ 8 m | Cổng nhỏ + bậc đá dẫn lên | "Nơi cao nhất làng thì nên có gì?" |
| E20 | Bến thuyền | E4 (bến nhỏ) ở ao ≥ 120 m² hoặc ở sông | Thuyền đậu cạnh bến (S4) | "Bến có rồi, thiếu gì nhỉ?" |
| E21 | Thác nước | Sông chảy qua chỗ dốc > 35° | Mặt nước thành dải thác + bọt trắng ở chân | "Dòng nước gặp vách đá…" |

**Sổ khám phá (`GameUI.DrawJournal`):** lưới hiện tại 5×3. Chuyển sang 2 trang (15 + 6), có nút ‹ › và phím ←/→ khi sổ đang mở.

**Tiêu chí xong:** mỗi tổ hợp kích hoạt đúng điều kiện, thông báo "✨ Khám phá mới", ghi sổ, và tắt được bằng Delete như các tổ hợp cũ. File lưu cũ (15 ô) vẫn tải đúng.

---

## S8 — Hoàn thiện việc dở

1. **Mờ dần 0.3 s khi xem trong nhà** (`World/Interior.cs`): mái và tầng trên mờ dần thay vì biến mất ngay. Dùng `MaterialPropertyBlock` đổi alpha nếu shader hỗ trợ; nếu `Stylized.shader` không có chế độ trong suốt thì thu `localScale.y` về 0 trong 0.3 s (đơn giản, không cần đổi shader).
2. **M15 cho tường thành và tháp** (`WallMesh.cs`, `World/WallGraph.cs`): đá xếp từng viên so le dọc tường đá (giống móng nhà trong `World/HouseDetail.cs` — tái dùng hàm), đá góc ở tháp, ván gỗ cho kiểu "Hàng rào gỗ". **Giữ số tam giác hợp lý**: gộp mesh mỗi đoạn tường thành 1 mesh, không tạo GameObject cho từng viên.
3. Cập nhật bảng Phần H trong `Docs/CoChe-Gameplay.md`: đánh dấu M8/M15 đã xong.

**Tiêu chí xong:** vào trong nhà thấy mái mờ/thu dần mượt; tường đá có viên đá rõ ràng, FPS không tụt quá 10% với làng 200 m tường.

---

## Phần cuối — Cách báo cáo mỗi bước

Sau mỗi bước, báo theo mẫu:
```
Bước: S?
File mới: ...
File sửa: ...
Cách thử: (các thao tác cụ thể trong game)
Chưa làm / còn lỗi: ...
```
Nếu một điều kiện trong tài liệu này không khớp code thật (tên hàm khác, file không tồn tại), **dừng lại hỏi** thay vì tự bịa API.
