# Làng Gió — Prototype khối xám (M10)

Đặc tả cơ chế chơi chi tiết (con số, quy tắc, thứ tự làm): [Docs/CoChe-Gameplay.md](Docs/CoChe-Gameplay.md)

Mọi vật chỉ lưu *tham số*, hình luôn được sinh lại từ tham số → chọn, kéo tay nắm để sửa, di chuyển, xoay, nhân bản, hoàn tác được mọi thứ. Có lưu / tải, tự động lưu, 15 tổ hợp khám phá, thế giới sống (đèn đêm, khói, vịt...).

## Mở và chạy
1. **Unity Hub** → **Add** → **Add project from disk** → chọn thư mục `LangGio` → mở bằng Unity **6000.1.12f1**.
2. Bấm **Play** → menu chính: **Chơi mới** hoặc **Tiếp tục** (làng đã lưu).

## Công cụ
| Phím | Công cụ | Thao tác |
|---|---|---|
| **V** | Bàn tay | Click chọn · kéo vật = di chuyển · kéo **tay nắm** = chỉnh · **R** / **Shift + con lăn** xoay · **Ctrl+D** nhân bản · **Delete** xoá · **Esc** bỏ chọn |
| **B** | Nhà | Kéo vẽ nền (hướng kéo đầu = chiều dài) · click = nhà 4 × 6 m · **kéo từ trong nhà cũ ra ngoài = thêm cánh (nhà chữ L / T / U)** · cửa tự quay về đường gần nhất |
| **G** | Tường | Kéo vẽ · **1–4** kiểu tường · **[ / ]** chiều cao · tự nối tháp / thân tường · cắt ngang → ngã tư |
| **K** | Tháp | Click đất: tháp riêng · click tường: chèn tháp · click tháp: thêm tầng |
| **H** | Đường | Kéo vẽ · **1–3** Đất / Đá cuội / Ván gỗ · **[ / ]** bề rộng · đầu đường gần cửa nhà / đường khác → tự nối |
| **O** | Ao | Kéo vẽ vòng · **Shift+kéo** cọ thêm nước · **Alt+kéo** cọ bớt · **1/2** nông / sâu |
| **T** | Cây | Click 1 cây · kéo = cọ trồng · **Alt+kéo** = cọ xoá · **1–4** Tròn / Thông / Bụi / Hoa |
| **P** | Cọ màu | Click đổi màu / chất liệu (tầng nhà, mái, cửa, đoạn tường, mái tháp, cây, đường) · giữ và quét = áp cùng màu |
| **W** | Cửa sổ | Click tường nhà: đặt ô cửa (trùng ô cũ → đổi kiểu) · **Alt + click**: xoá · **1–5**: Vuông / Vòm / Tròn / Cửa ra vào / Cao |
| **A** | Dây leo | Giữ + kéo lên tường thành / nhà / khối để phủ dây leo · **Alt + kéo**: gỡ · **[ / ]**: mảng to / nhỏ · dời nhà thì dây leo đi theo |
| **D** | Đồ trang trí | Click đặt · kéo = rải nhiều món · **1–9, 0**: Đèn lồng / Ghế băng / Thùng gỗ / Thùng hàng / Chậu hoa / Đống rơm / Xe cút kít / Bù nhìn / Biển chỉ đường / Đống củi · **R** xoay 45° · đặt được lên mái bằng / mặt khối · đèn lồng sáng khi chiều tối |
| **L** | Cọ đất | Kéo để tô: **1** Cỏ (xoá) · **2** Hoa dại (cỏ hoa dày + bướm) · **3** Sỏi · **4** Cát · **5** Đất · **[ / ]** cỡ cọ |
| **N** | Khối | Xây tự do bằng mảnh ghép — xem mục "Xây bằng khối" bên dưới |
| **U** | Địa hình | **1** Nâng · **2** Hạ · **3** San phẳng · **4** Làm mượt · **[ / ]** cỡ cọ · Shift mạnh |
| **X** | Xóa | Click vật (tường: từng đoạn · nhà: từng cánh) · click chi tiết tự mọc = tắt nó · **giữ + kéo = cọ xoá cây và đường** · **[ / ]** cỡ cọ |

| Phím chung | |
|---|---|
| **Z / Y** | Hoàn tác / làm lại (200 bước) |
| **Ctrl+S** | Lưu làng |
| **J** | Sổ khám phá |
| **I** | Xem trong nhà đang chọn (bật / tắt) · **PageUp / PageDown** đổi tầng |
| **F** | Chế độ chụp ảnh: ẩn giao diện · **Space** chụp · **[ / ]** đổi giờ · **F / Esc** thoát |
| **Esc** | Về Bàn tay → bỏ chọn → menu tạm dừng |
| Chuột phải / Q, E | Xoay camera |
| Con lăn | Zoom |
| Chuột giữa / phím mũi tên | Di chuyển camera |

## Tay nắm (khi chọn bằng Bàn tay)
- 🔵 **Cầu xanh** (kéo trên mặt đất): cạnh / góc xoay nhà · đầu & giữa đoạn tường · đầu & giữa đường · bờ ao
- 🔼 **Mũi tên vàng** (kéo lên/xuống): số tầng nhà / tháp (tháp về 0 = bỏ tháp) · độ dốc mái · chiều cao tường · bề rộng đường · độ sâu ao · cỡ cây
- 🔶 **Hình thoi cam** (click): kiểu mái nhà / mái tháp · chất liệu tầng · vị trí cửa · kiểu tường · kiểu đường · loại cây

## Xem trong nhà (I)
Chọn 1 nhà → nhấn **I** (hoặc click 🔶 con mắt trên nóc nhà).
- Mái và các tầng phía trên biến mất; vách phía camera hạ còn 1 m để nhìn vào. Xoay camera thì vách tự đổi.
- Nhà chia phòng theo diện tích (< 20 m²: 1 · 20–40: 2 · > 40: 3), vách ngăn có ô cửa; nhà ≥ 2 tầng có cầu thang ở góc xa cửa nhất.
- Đồ đạc tự sinh: tầng 1 bàn + ghế, bếp lò (dưới ống khói nếu có), tủ, chậu cây · tầng trên giường, rương, tủ, đèn.
- **Sửa**: click đồ → kéo để xếp (snap 0.25 m, không ra khỏi tường) · **R** xoay 90° · **Delete** xoá · 🔶 trái đổi kiểu (3 kiểu) · 🔶 phải xoay.
- **Thêm đồ**: thanh dưới màn hình (Bàn · Ghế · Giường · Tủ · Đèn · Chậu cây · Rương).
- Lần đầu sửa, game chép bộ tự sinh vào nhà và từ đó nhớ mọi thay đổi (xoá rồi không tự mọc lại). Đổi kích thước nhà → đồ nằm ngoài bị đẩy vào trong.
- Ra ngoài: **I**, **Esc**, nút "Ra ngoài" hoặc click ra ngoài nhà.

## Xây bằng khối (N)
14 loại mảnh: Khối · Nửa khối · Tấm sàn · Vách ván · Cột · Dầm · Mái dốc · Mái thoải · Tấm mái · Bậc thang · Hàng rào · Vòm · Cửa sổ · Cửa ra vào — 9 chất liệu (đá, đá tối, gỗ, gỗ tối, gạch, vữa, ngói đỏ, ngói xám, rêu).

| Thao tác | Kết quả |
|---|---|
| Click | Đặt mảnh. Trỏ vào mảnh có sẵn → mảnh mới **tự hít** vào mặt đó (trên, bên cạnh); trỏ mặt đất → bám lưới 0.5 m |
| Giữ + kéo | Xếp **1 hàng** · **Shift**: cả mảng (sàn) · **Space**: chồng lên cao (cột) · **Shift + Space**: cả **bức tường** |
| **R** / **Shift+R** | Xoay 90° / 15° |
| **Alt** + click / kéo | Tháo mảnh |
| **Ctrl** + click / kéo | Thay mảnh bằng loại + chất liệu đang chọn |
| **C** | Lấy mẫu mảnh dưới chuột |
| **1–9, 0** / **[ ]** | Chọn loại mảnh · **M**: đổi chất liệu (hoặc bấm ô màu trên thanh) |

Bàn tay chọn được **từng mảnh**: kéo = di chuyển (bước 0.25 m) · 🔼 nâng / hạ · 🔶 đổi loại · đổi chất liệu · xoay 90° · **Delete** xoá. Công cụ Xóa (X) click tháo từng mảnh. Mọi thứ hoàn tác được và lưu cùng làng.

## Nhà nâng cao
- **Cánh nhà (L / T / U)**: công cụ Nhà, nhấn trong nền nhà cũ rồi kéo ra ngoài 1 cạnh → cánh mới dính vào cạnh đó (tối đa 4 cánh). Mái cánh tự ghép vào mái chính. Click cánh (Bàn tay) để chọn riêng: kéo = trượt dọc cạnh · 🔵 kéo đầu / 2 bên = đổi độ sâu / bề ngang · 🔼 số tầng · **Delete** = bỏ cánh.
- **Tầng hẹp dần**: 🔶 bên trái mỗi tầng 2+ → tầng đó lùi vào 0.3 m mỗi phía (cộng dồn).
- **Ban công**: 🔶 thấp ở giữa mỗi tầng 2+ → ban công gỗ + cửa kính phía cửa ra vào.
- **Hướng nóc**: **Shift + click** 🔶 kiểu mái → nóc chạy theo trục ngắn / dài.
- **Cửa sổ mái**: 🔶 cạnh nóc (mái hai dốc / bốn dốc) → 0 / 1 / 2 cửa sổ mái mỗi bên.
- **Ống khói**: 🔶 cạnh nóc bên kia → tự động (nhà ≥ 30 m²) / luôn có / không.
- **Cổng hàng rào**: đường đi cắt qua hàng rào gỗ / bờ rào thấp → cổng gỗ mở hé.
- **Ao**: kéo / nắn bờ ao chạm ao khác → tự gộp thành 1 (hoàn tác 1 bước).

## Tổ hợp khám phá (J)
Đặt các thứ gần nhau → chi tiết mới tự mọc + thông báo "Khám phá mới":

| | Tổ hợp | Điều kiện |
|---|---|---|
| E1 | Cổng thành | Đường cắt qua tường đá |
| E2 | Cầu gỗ | Đường đi qua ao |
| E3 | Cầu đá vòm | Tường vắt qua ao |
| E4 | Bến nhỏ | Nhà cách bờ ao ≤ 3 m |
| E5 | Liễu rủ | Cây tròn cách bờ ao ≤ 4 m |
| E6 | Nhà dựa tường | Cạnh nhà sát tường (không mở cửa sổ phía đó) |
| E7 | Giếng làng | ≥ 3 nhà trong 15 m, ≥ 2 nhà có đường tới cửa |
| E8 | Thành trì | Vòng tường kín có nhà bên trong → cờ trên tháp |
| E9 | Đèn đường | Ban đêm (sau 18:30), đường trong 6 m quanh nhà |
| E10 | Đàn vịt | Ao ≥ 60 m² |
| E11 | Hoa súng | Ao nông + ≥ 3 cây quanh bờ |
| E12 | Chuông tháp | Tháp 3 tầng |
| E13 | Vườn rau | Sau nhà trống 4 × 3 m |
| E14 | Rừng nhỏ | ≥ 12 cây trong 10 m |
| E15 | Lâu đài | Nhà ≥ 3 tầng trong vòng thành |

**Tắt 1 chi tiết**: click chi tiết (cổng, cầu, giếng, cờ, đèn...) bằng Bàn tay rồi nhấn **Delete**, hoặc click bằng công cụ Xóa → chi tiết đó không mọc lại ở chỗ đó (được lưu; **Z** để hiện lại). Sổ khám phá có nút "Hiện lại N chi tiết đã tắt".

**Thế giới sống**: gà và cừu đi lại quanh nhà · đàn chim bay vòng trên làng · bướm trên bãi hoa dại · hộp hoa dưới cửa sổ · cửa sổ sáng đèn sau 18:30 · khói ống khói sau 17:00 · mặt nước gợn · cây lớn dần, đung đưa · vịt bơi. Bật **Giờ tự chạy** trong menu tạm dừng để ngày đêm tự trôi.

## Âm thanh, hình ảnh, cài đặt (M9)
- **Âm thanh tự tổng hợp** (không cần file): "pốp" khi vật mọc lên, tiếng đặt / xoá, hoàn tác / làm lại, chuông khi khám phá tổ hợp, click nút, chụp ảnh. **Âm nền**: gió, chim hót ban ngày, dế kêu buổi tối.
- **Cỏ và hoa dại** rải khắp mặt đất, tự tránh nhà, ao, đường, tường, tháp và chỗ dốc; cập nhật sau mỗi thay đổi.
- Cửa sổ có khung trắng + bậu gỗ · viền tối nhẹ quanh màn hình · đồ đạc trong nhà nảy lên khi vào nhà / đổi tầng.
- **Menu tạm dừng → Cài đặt**: âm lượng hiệu ứng, âm nền, bật/tắt cỏ hoa, bóng đổ, viền tối (lưu tự động).
- Ảnh chụp: `...\LangGio\photos\` cạnh thư mục lưu làng.

## Lưu
- File: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\LangGio\villages\village.json` (đường dẫn `Application.persistentDataPath`).
- Tự động lưu mỗi 2 phút nếu có thay đổi, khi mở menu tạm dừng, khi về menu chính và khi thoát.

## Cấu trúc code
| File | Việc |
|---|---|
| `Core/GameConfig.cs` | **Mọi con số chỉnh "cảm giác"** |
| `World/HeightField.cs` | Địa hình lưới 1 m + lớp đào ao |
| `World/Entity.cs`, `EntityManager.cs`, `Commands.cs`, `History.cs` | Khung tham số → hình, hoàn tác |
| `World/WallGraph.cs` | Tường thành dạng đồ thị Nút–Đoạn, quy tắc tháp R1–R8 |
| `World/HouseEntity.cs` | Nhà (kèm phần nội thất: chia phòng, cầu thang, đồ tự sinh, cắt lớp) |
| `World/Interior.cs`, `Furniture.cs` | Chế độ xem trong nhà · danh mục đồ đạc |
| `World/PathEntity.cs` | Đường đi |
| `World/PondMask.cs`, `PondEntity.cs` | Ao |
| `World/TreeEntity.cs` | Cây |
| `World/BlockCatalog.cs`, `BlockWorld.cs`, `Tools/BlockTool.cs` | Xây tự do bằng mảnh ghép (gộp hình theo cụm 8 m) |
| `World/Combos.cs` | 15 tổ hợp khám phá (chi tiết phái sinh, không lưu) |
| `World/LivingWorld.cs`, `Sway`, `Grow`, `Puff`, `Duck` | Thế giới sống |
| `SaveSystem.cs`, `AutoSaver.cs` | Lưu / tải / tự động lưu |
| `Sfx.cs`, `Settings.cs` | Âm thanh tự tổng hợp + âm nền · cài đặt người chơi |
| `World/GroundDecor.cs` | Cỏ và hoa trang trí |
| `Tools/*` | 10 công cụ + tay nắm |
| `GameUI.cs` | Menu chính, tạm dừng, sổ khám phá, thanh công cụ (IMGUI tạm) |

## Hình ảnh (M14 · URP)
- Project dùng **URP 17.1** (có sẵn trong Unity 6.1). Lần đầu mở, script `Assets/Editor/RenderSetup.cs` tự tạo `Assets/Settings/LangGio_URP.asset` (bóng mềm 4 tầng, MSAA 4×, HDR, **SSAO**) và gán cho Graphics + mọi mức Quality. Console sẽ báo `[Làng Gió] Đã tạo URP asset`.
- Mọi vật dùng shader **LangGio/Stylized**: vân nhiễu, sáng tối mềm, viền sáng, nhận SSAO + bóng mềm + đèn phụ. Bầu trời: **LangGio/SkyGradient** (chuyển màu theo giờ, quầng nắng, mây trôi).
- Hậu kỳ (`RenderLook.cs`): tô màu ấm, bloom cho đèn, viền tối, **mô hình thu nhỏ** (mờ xa / gần, luôn lấy nét chỗ camera nhìn), SMAA. Bật / tắt trong menu tạm dừng.

## Nhà thủ công (M15)
Nhà tự sinh chi tiết theo chất liệu từng tầng: **vữa** → đá góc + khung gỗ ở tầng trên · **gỗ** → ván ốp đứng · **đá / gạch** → đá góc + viên đá / gạch lộ. Móng xếp đá (có viên phủ rêu), mái lợp ngói từng hàng so le, nóc ngói, ván diềm, song cửa, cửa chớp (cùng màu cửa chính), mái hiên trên cửa chính, ống khói đá có nắp. Mọi chi tiết gộp thành vài lưới để chạy nhẹ.

## Giao diện (theo Docs/UI/mockup.html)
- **Thanh biểu tượng** ở dưới: Bàn tay · **Xây** (nhà, cửa sổ, tường, tháp, đường, khối) · **Thiên nhiên** (cây, ao, cọ đất, địa hình) · **Trang trí** (đồ trang trí, dây leo, cọ màu) · Xóa · Hoàn tác / Làm lại. Rê chuột để xem tên + phím tắt.
- Bấm nhóm → **khay công cụ**; chọn công cụ → **hàng tuỳ chọn** (kiểu tường + chiều cao, chế độ địa hình, loại cây, kiểu đường + bề rộng, màu tô đất, loại khối + chất liệu, kiểu ô cửa, loại đồ trang trí).
- Chọn vật → **chip nổi phía trên vật**: tên, thông số, nút Xoay · Nhân bản · Xem trong nhà · Xoá.
- Góc trên: Menu · Sổ khám phá (số đã tìm) · đồng hồ kéo đổi giờ · Chụp ảnh (F) · Ẩn giao diện (**F1**).
- Biểu tượng: `Assets/Resources/UI/icons.png` (vẽ từ SVG trong bản mẫu). Chữ: Segoe UI của Windows.

## Cảnh khởi đầu (M17)
- Mỗi lần bấm **Làng mới**, game sinh ngẫu nhiên một thung lũng có sẵn: giữa là khoảng đất bằng (bán kính khoảng 24 m) để xây nhà, xung quanh là đồi thoải dâng lên ở rìa, một hồ lớn và một ao nhỏ có bãi cát ven bờ, 4 cánh rừng (thông, cây tròn, pha trộn) có bụi ở bìa, liễu ven hồ, cây hoa, cây lẻ, đồng hoa dại, và một đường đất uốn lượn từ bìa thung lũng vào sân sỏi ở giữa.
- Cây, hồ, đường đều là vật bình thường: chọn, sửa, xoá hay hoàn tác được như vật bạn tự đặt.
- Code: `Assets/Scripts/World/StartLandscape.cs` (`SaveSystem.NewVillage(withLandscape)`).

## Mùa, thời tiết, không khí (M16)
- **Mùa** (nút dưới đồng hồ, góc phải trên): Xuân → Hạ → Thu → Đông. Lá và cỏ đổi màu ngay trong shader (thu: tán vàng cam, cỏ úa; đông: tuyết phủ mặt hướng lên, ao đóng băng, tuyết rơi). Chuyển mượt khoảng 2 giây.
- **Thời tiết** (nút cạnh mùa): Nắng / Nhiều mây / Mưa. Khi mưa: trời xám, mây dày, sương gần hơn, mặt đất sẫm và loáng nước, có vũng, vòng gợn trên ao, hạt mưa, tiếng mưa. Tạnh mưa ban ngày thì hiện **cầu vồng** phía đối diện mặt trời.
- Mùa và thời tiết được lưu theo từng làng.
- **Ngắm cảnh** (F2 hoặc nút ▶): camera tự lượn quanh làng và giao diện ẩn đi. Bấm phím hoặc chuột bất kỳ để thoát.
- Không khí: cánh hoa bay theo gió ban ngày, đom đóm ban đêm, bóng mây trôi trên mặt đất, cỏ lay theo gió, bụi bung ra khi đặt công trình, khói ống khói cả ngày (dày hơn khi tối, mùa đông, trời mưa).
- Trời: đĩa mặt trời, sương sáng ở chân trời, mây tích có khối sáng tối, mây ti, sao ban đêm. Vành đất bao quanh vùng xây trải ra tận chân trời.
- Cổng thành có hai cánh gỗ vòm, nẹp sắt và tay nắm đồng.
- **Chế độ chụp ảnh** có thêm bộ lọc màu: Gốc / Ấm / Mát / Cổ điển / Đen trắng.
- Ban đêm đèn lồng lập loè như ánh lửa và khoảng 1/4 số ô cửa tắt đèn. Ban ngày kính cửa sổ màu xám xanh.
- Đạo cụ đẹp hơn: đống rơm nhiều lớp, vườn rau có bắp cải xanh/tím và cà rốt, cột đèn đường có đế đá, lồng kính và mái chóp, khói ống khói dạng mây bồng bềnh.
- **Đồ hoạ nhẹ** (Tạm dừng → Hình ảnh): tắt SSAO và làm mờ, dựng hình ở 80% độ phân giải, khử răng cưa nhẹ hơn, giảm số hạt. Trên GT 730 ở 720p: khoảng 45 FPS ở chế độ thường, 60 FPS ở đồ hoạ nhẹ.
- Tay nắm chỉnh sửa có kiểu mới: lõi màu pastel, vòng trắng, thở nhẹ.
- Bộ chụp tự động đo FPS (ghi vào `done.txt`) và vẫn chạy khi cửa sổ game không được chọn.
- Bản build giữ lại biến thể sương (fog) nên chân trời hoà vào trời mờ; SSAO dịu hơn ở các khe hẹp.
- Ảnh chụp tự động giờ vẽ camera vào RenderTexture, nên vẫn chụp được khi Remote Desktop ngắt (riêng ảnh giao diện `ui_*` vẫn cần có màn hình).

## Chụp ảnh tự động (kiểm tra hình ảnh)- Menu **Làng Gió → Build bản chụp ảnh tự động** (hoặc dòng lệnh `Unity.exe -batchmode -quit -projectPath LangGio -executeMethod LangGio.EditorTools.TourBuild.Build`) → `Builds/Tour/LangGio.exe`.
- Chạy `LangGio.exe -langgio-tour -tour-out <thư mục>`: tự dựng làng mẫu, chụp 12 ảnh (6 góc × trưa / hoàng hôn / đêm), ghi `done.txt` rồi thoát. Không ghi vào làng đã lưu.

## Nếu gặp lỗi- **Mọi thứ màu hồng / tím**: chọn menu **Làng Gió → Thiết lập lại URP**, rồi chạy lại. Vẫn hồng → gửi ảnh Console.
- **"You are trying to read Input using the UnityEngine.Input class…"**: *Edit → Project Settings → Player → Active Input Handling* → **Both**, khởi động lại Unity.
- **Màu hồng / tím**: shader chưa nạp hoặc đang dùng URP — gửi ảnh Console.
