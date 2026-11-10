# Làng Gió — Đặc tả cơ chế chơi v0.4

> **Mục tiêu bản này:** cơ chế phải **cuốn hút** và **mọi thứ đều sửa được sau khi đặt** — từ nền nhà, số tầng, mái nhà, tường thành, ao hồ đến cây cối.
> **Chưa cần đẹp.** Toàn bộ dùng khối xám / màu phẳng. Chỉ cần **hoạt động đúng** và **chơi thấy vui**.
> Đơn vị: mét, giây, độ. Mọi con số là giá trị khởi đầu, nằm trong `GameConfig` để chỉnh khi chơi thử.

---

## Phần A — Vì sao người chơi bị cuốn vào?

Game không có thắng/thua, nên sức hút phải đến từ **5 "móc câu"** dưới đây. Mỗi tính năng mới phải phục vụ ít nhất 1 móc.

| # | Móc câu | Người chơi cảm thấy | Cơ chế cụ thể |
|---|---|---|---|
| H1 | **Thấy ngay kết quả** | "Mình vẽ 1 nét mà ra cả bức tường!" | Mọi thao tác cho ra kết quả trong **< 0.5 s**, có hiệu ứng nảy |
| H2 | **Sửa không sợ hỏng** | "Thử đại xem sao, không thích thì kéo lại" | Mọi thứ có **tay nắm (handle)** để kéo sửa; hoàn tác vô hạn |
| H3 | **Bất ngờ nhỏ** | "Ồ, tự mọc cái cầu kìa!" | **Tổ hợp khám phá**: đặt 2 thứ gần nhau → mọc chi tiết mới (Phần E) |
| H4 | **Thế giới sống** | "Làng mình đang sống" | Khói ống khói, đèn cửa sổ ban đêm, vịt trên ao, cây lớn dần |
| H5 | **Tò mò sưu tầm** | "Còn 4 khám phá nữa, chắc là đặt nhà cạnh ao?" | **Sổ khám phá**: danh sách tổ hợp, cái chưa tìm ra chỉ hiện bóng mờ + gợi ý |

**Vòng lặp chính:** Đặt → Thấy nó mọc lên (H1) → Kéo chỉnh cho vừa ý (H2) → Phát hiện chi tiết bất ngờ (H3) → Ngắm làng sống động (H4) → Tò mò thử tổ hợp khác (H5) → Đặt tiếp…

---

## Phần B — Kiến trúc: "Mọi thứ đều là tham số"

Đây là quyết định kỹ thuật quan trọng nhất. Nó giúp **mọi thứ sửa được**.

```
 Dữ liệu (tham số)  ──▶  Bộ sinh (Generator)  ──▶  Mesh trên màn hình
   nền: 6×4 m             đọc tham số,              (vứt đi và sinh lại
   tầng: 2                tự tính cửa sổ,            mỗi khi tham số đổi)
   mái: hai dốc, 40°      cửa, ống khói...
   seed: 812
```

| Quy tắc | Nghĩa là |
|---|---|
| **B1** | Mỗi vật thể (nhà, đoạn tường, ao, cây…) chỉ lưu **tham số**, không lưu mesh |
| **B2** | **Generator** sinh mesh từ tham số. Cùng tham số + cùng seed → luôn ra cùng một kết quả |
| **B3** | Sửa = đổi tham số → sinh lại mesh của **riêng vật đó** (và vật bị ảnh hưởng bởi tổ hợp) |
| **B4** | Mọi thay đổi tham số đi qua **Command** (có Do/Undo) → hoàn tác được mọi thứ |
| **B5** | Lưu game = lưu danh sách tham số (JSON nhỏ gọn) |

### B.1 Các loại vật thể
| Vật thể | Tạo bằng | Tham số chính |
|---|---|---|
| **Nhà** | Công cụ Nhà | Nền (đa giác), số tầng, chất liệu từng tầng, kiểu mái, độ dốc mái, màu mái, seed |
| **Tường thành** | Công cụ Tường | Đồ thị Nút–Đoạn (Phần C.4), chiều cao, kiểu tường |
| **Tháp** | Tự mọc hoặc công cụ Tháp | Vị trí, số tầng tháp, kiểu mái tháp |
| **Ao / hồ** | Công cụ Nước | Đường viền (vòng kín các điểm), độ sâu |
| **Cây** | Công cụ Cây | Vị trí, loại, kích thước, tuổi |
| **Đường đi** | Công cụ Đường | Đường giữa (các điểm), bề rộng |
| **Địa hình** | Công cụ Địa hình | Lưới độ cao 1 m (lưu theo khối 16 × 16 m) |
| **Đồ nội thất** | Tự sinh / thêm tay khi xem trong nhà | Loại, biến thể, vị trí, góc xoay, thuộc nhà nào + tầng nào |

> Mọi Generator lấy độ cao mặt đất qua **một hàm duy nhất** `Terrain.HeightAt(x, z)`. Nhờ vậy địa hình có thể thêm từ sớm mà không phải viết lại các bộ phận khác.

---

## Phần C — Từng bộ phận: tạo thế nào, sửa thế nào

### Cách sửa chung (áp dụng cho mọi vật thể)

**Công cụ mặc định là "Bàn tay" (phím V hoặc Esc).**

| Thao tác | Kết quả |
|---|---|
| Rê chuột lên vật | Vật sáng viền (đang trỏ vào) |
| Click vào vật | **Chọn** → hiện các **tay nắm** của vật đó |
| Kéo tay nắm | Đổi tham số tương ứng, mesh sinh lại **ngay khi kéo** (xem trước), thả chuột = chốt (1 Command) |
| Kéo thân vật (không phải tay nắm) | **Di chuyển** cả vật |
| Phím **R** / con lăn + Shift | Xoay vật được chọn 15°/lần |
| Phím **Delete** | Xoá vật được chọn |
| Phím **Ctrl+D** | Nhân bản vật được chọn (đặt lệch 2 m) |
| Click ra chỗ trống / Esc | Bỏ chọn |

**Tay nắm** là các khối nhỏ màu sáng:
- 🔵 **Hình cầu**: kéo trên mặt đất (vị trí, góc, cạnh)
- 🔼 **Mũi tên đứng**: kéo lên/xuống (chiều cao, số tầng)
- 🔶 **Hình thoi**: click để **đổi kiểu** (vòng qua các lựa chọn)

---

### C.1 Nền nhà

**Tạo:** Công cụ Nhà → nhấn giữ chuột tại 1 góc → kéo tới góc đối diện → thả. Hướng kéo đầu tiên quyết định trục dài, xoay snap 15°.

| Tham số | Giá trị | Sửa bằng |
|---|---|---|
| Hình nền | Hình chữ nhật, rộng 3–10 m, dài 3–16 m | 🔵 4 tay nắm **giữa cạnh**: kéo ra/vào để đổi kích thước (snap 0.5 m) |
| Góc xoay | 0–360°, snap 15° | 🔵 1 tay nắm **xoay** ở ngoài góc |
| Mở rộng hình L/T/U | Thêm hình chữ nhật dính vào cạnh | Công cụ Nhà kéo **bắt đầu từ trong nền cũ** → nền mới **gộp** vào nhà đó (thành 1 nhà) |
| Bậc nền | Nền cao 0.3 m, có 1 bậc trước cửa | Tự động |

**Quy tắc tự động:**
- Nền chồng lên tường thành / nhà khác → preview đỏ, không đặt (trừ trường hợp gộp ở trên).
- Cây và đường nằm dưới nền → tự ẩn (vẫn lưu lại, xoá nhà thì hiện lại).

---

### C.2 Các tầng nhà

| Tham số | Giá trị | Sửa bằng |
|---|---|---|
| Số tầng | 1–4 | 🔼 **Mũi tên trên nóc nhà**: kéo lên → thêm tầng, kéo xuống → bớt tầng (mỗi 2.8 m = 1 tầng). Tầng mới **trồi lên**, mái được **nâng theo** |
| Chất liệu từng tầng | Vữa trắng / Gỗ / Đá / Gạch | 🔶 **Hình thoi ở giữa mỗi tầng**: click để đổi vòng |
| Tầng hẹp dần (tầng trên lùi vào) | Có / Không | 🔶 trên tầng 2 trở lên: bật thì tầng đó nhỏ hơn tầng dưới 0.3 m mỗi phía |
| Ban công | Có / Không mỗi tầng ≥ 2 | 🔶 ở mép tầng |

**Chi tiết tự sinh trên mỗi tầng** (không cần người chơi làm gì):
| Chi tiết | Quy tắc |
|---|---|
| Cửa ra vào | 1 cửa ở tầng 1, trên cạnh dài, quay về **đường đi gần nhất**; không có đường → quay về phía camera lúc đặt. Click vào cửa (khi nhà đang chọn) → dời cửa sang vị trí khả dĩ kế tiếp |
| Cửa sổ | Mỗi 2.2 m trên mỗi cạnh, chừa 1 m ở góc, không trùng cửa |
| Đường viền tầng | Dải gờ ngang giữa 2 tầng |
| Cầu thang | Không hiển thị (nhà xem từ ngoài) |

---

### C.3 Mái nhà

| Tham số | Giá trị | Sửa bằng |
|---|---|---|
| Kiểu mái | **Hai dốc** · **Bốn dốc** · **Mái bằng** (có lan can) · **Nón** (chỉ nhà vuông ≤ 5 m) | 🔶 **Hình thoi trên nóc**: click đổi vòng |
| Độ dốc | 20°–60° | 🔼 **Mũi tên trên đỉnh mái**: kéo lên/xuống |
| Hướng nóc | Theo trục dài / trục ngắn | 🔶 click lần 2 vào hình thoi khi đang giữ Shift |
| Màu mái | Ngói đỏ / Ngói xám / Rơm vàng / Rêu xanh | Công cụ **Cọ màu** (Phần C.7) |
| Chìa mái | 0.3 m | Cố định |

**Quy tắc tự động:**
- Nhà hình L/T/U → mái tự **ghép** theo từng hình chữ nhật, giao nhau thành khe mái.
- Ống khói: nhà ≥ 30 m² có 1 ống khói (vị trí theo seed). Ban đêm có khói (H4).
- Mái bằng → tự có lan can và lối lên (nắp hầm nhỏ).

---

### C.4 Tường thành và tháp

Tường lưu dạng **đồ thị**: **Nút** (tháp hoặc chỗ nối) nối với nhau bằng **Đoạn tường**.

**Tạo:** Công cụ Tường → kéo chuột vẽ nét (giữ Shift = thẳng). Thả chuột → game tự làm mượt, tìm góc, mọc tháp.

| Tham số | Giá trị | Sửa bằng |
|---|---|---|
| Đường đi của đoạn | Các điểm điều khiển | 🔵 Tay nắm ở **mỗi nút** (kéo → 2 đoạn nối vào tự uốn theo) + 🔵 tay nắm **giữa đoạn** (kéo → uốn cong đoạn đó) |
| Chiều cao | 1.6 / 2.6 / 3.6 / 4.6 m | 🔼 Mũi tên giữa đoạn |
| Kiểu tường | Đá có lỗ châu mai / Đá trơn / Hàng rào gỗ / Bờ rào thấp | 🔶 Hình thoi giữa đoạn |
| Tháp: số tầng | 0 (không tháp) – 3 | 🔼 Mũi tên trên tháp. Kéo về 0 = bỏ tháp, tường liền mạch |
| Tháp: kiểu mái | Nón / Bằng có răng cưa / Chòi gỗ | 🔶 trên tháp |

**Quy tắc tự động của đồ thị:**
| Quy tắc | Điều kiện | Kết quả |
|---|---|---|
| R1 | Nút ở đầu tường (1 đoạn) | Có tháp |
| R2 | Nút có góc gấp > 40° | Có tháp |
| R3 | Nút có góc ≤ 40° | Không tháp, tường liền |
| R4 | Ngã ba/ngã tư (≥ 3 đoạn) | Có tháp |
| R5 | Đoạn dài > 24 m | Chèn tháp nhỏ ở giữa |
| R6 | 2 tháp cách nhau < 4 m | Giữ 1 |
| R7 | Nút không còn đoạn nào (và do game tự tạo) | Xoá |
| R8 | Nút người chơi đã chỉnh tay | Game không tự đổi nút đó nữa |

**Snap khi vẽ:** đầu/cuối nét hút vào nút có sẵn (1.8 m) → hút vào thân tường (1.2 m, tách đoạn) → khép vòng về điểm đầu (1.6 m). Vẽ cắt ngang tường cũ → tự tạo ngã tư có tháp.

---

### C.5 Ao hồ

**Tạo:** Công cụ Nước, 2 cách:
- **Kéo vẽ vòng** (như vẽ lasso): thả chuột → tự khép thành ao theo hình vừa vẽ.
- **Cọ nước**: giữ **Shift** + kéo → quét như cọ vẽ, bán kính 2.5 m, các vệt chạm nhau tự gộp thành 1 ao.

| Tham số | Giá trị | Sửa bằng |
|---|---|---|
| Đường viền | Vòng kín, làm mượt, chia đều mỗi 1.5 m | 🔵 Tay nắm trên viền (mỗi ~4 m một cái): kéo để nắn bờ |
| Mở rộng / thu hẹp | | Cọ nước (Shift+kéo) để thêm; **Alt+kéo** để bớt |
| Độ sâu | Nông (0.4 m) / Sâu (1.2 m) | 🔼 Mũi tên giữa ao |
| Diện tích | 4–2000 m² | Ao nhỏ hơn 4 m² tự huỷ |

**Chi tiết tự sinh:**
| Chi tiết | Quy tắc |
|---|---|
| Lòng ao | Mặt đất hạ xuống theo độ sâu, bờ dốc thoải 1 m |
| Mặt nước | Mặt phẳng ở y = −0.1 m |
| Đá bờ | Rải đá nhỏ dọc bờ, mỗi ~2 m |
| Lau sậy | Mọc ở 30% đoạn bờ (theo seed) |
| Hai ao chạm nhau | Tự **gộp** thành 1 ao |

---

### C.6 Cây cối

**Tạo:** Công cụ Cây — click = 1 cây; giữ + kéo = **cọ cây** bán kính 3 m (mật độ 1 cây/6 m², cách nhau ≥ 2.2 m). **Alt + kéo** = cọ xoá cây.

| Tham số | Giá trị | Sửa bằng |
|---|---|---|
| Loại | Cây tròn / Cây thông / Bụi / Cây hoa | 🔶 trên cây được chọn; hoặc phím **1–4** khi đang cầm công cụ Cây để chọn loại sẽ trồng |
| Kích thước | 0.6–1.6 | 🔼 Mũi tên trên ngọn |
| Vị trí | | Kéo thân cây |
| Tuổi (H4) | Cây mới trồng nhỏ 60%, **lớn dần** tới kích thước đủ trong 60 s | Tự động |

**Quy tắc tự động:** không mọc trên nền nhà, tường, đường, mặt nước. Xây đè lên cây → cây ẩn, xoá công trình → cây hiện lại.

---

### C.7 Cọ màu / chất liệu

Một công cụ riêng (phím **P**) để đổi "da" mà không đổi hình:
| Click vào | Đổi vòng qua |
|---|---|
| Tầng nhà | Vữa trắng → Gỗ → Đá → Gạch |
| Mái nhà / mái tháp | Ngói đỏ → Ngói xám → Rơm → Rêu |
| Đoạn tường | Đá sáng → Đá tối → Gỗ |
| Cửa ra vào | Gỗ nâu → Xanh lá → Xanh dương → Đỏ |

Giữ chuột và **quét** qua nhiều vật → áp cùng một chất liệu cho tất cả (nhanh và đã tay).

---

### C.8 Đường đi

**Tạo:** kéo vẽ như tường. Rộng 2 m, nằm sát đất.
**Sửa:** tay nắm như đoạn tường (nút + giữa đoạn), 🔼 bề rộng 1–4 m, 🔶 kiểu (đất / đá cuội / ván gỗ).
**Tự động:** đầu đường trong 2 m quanh cửa nhà → hút vào cửa; đường gặp đường → tự nối.

---

### C.9 Địa hình: nâng / hạ đồi

Mặt đất là một **lưới độ cao** (heightmap) ô 1 m, phủ vùng 180 × 180 m. Độ cao cho phép từ **−3 m** đến **+12 m**.

**Công cụ Địa hình (phím U)**: 4 chế độ, đổi bằng phím 1–4 khi đang cầm công cụ:
| Chế độ | Giữ chuột trái + kéo |
|---|---|
| 1. **Nâng** | Đất nhô lên dần dưới cọ (tốc độ 2 m/s ở tâm cọ, giảm mềm ra mép) |
| 2. **Hạ** | Đất lún xuống dần |
| 3. **San phẳng** | Kéo mọi điểm dưới cọ về đúng độ cao **tại chỗ bắt đầu nhấn chuột** → làm sân, làm nền |
| 4. **Làm mượt** | Xoá gồ ghề, bo tròn sườn đồi |

| Tham số cọ | Giá trị | Đổi bằng |
|---|---|---|
| Bán kính | 2–15 m (mặc định 5 m) | Phím **[** / **]** |
| Độ mạnh | Nhẹ / Vừa / Mạnh | Giữ **Shift** = mạnh gấp đôi |
| Hiển thị | Vòng tròn cọ bám theo mặt đất | Luôn hiện |

**Mọi thứ khác tự thích nghi với địa hình** (không cần người chơi làm gì):
| Vật | Phản ứng khi đất thay đổi |
|---|---|
| **Nền nhà** | Nhà đặt ở độ cao **cao nhất** dưới nền; phần hở bên dưới tự đắp **chân móng đá** xuống tới đất. Dốc chênh > 3 m dưới nền → preview đỏ |
| **Tường thành** | Chân tường bám theo mặt đất từng điểm; đỉnh tường = mặt đất + chiều cao, làm mượt để không gãy khúc; lỗ châu mai bậc thang theo dốc |
| **Tháp** | Đứng ở độ cao mặt đất tại tâm |
| **Ao hồ** | Mặt nước = điểm **thấp nhất trên bờ** − 0.1 m (nước không bao giờ tràn bờ). Nâng đất cắt ngang ao → ao tự thu nhỏ |
| **Cây** | Bám mặt đất; dốc > 35° → cây không mọc |
| **Đường đi** | Bám mặt đất; đoạn dốc > 25° → tự thành **bậc thang** đá |
| **Mặt đất** | Dốc > 40° hiện màu đá (vách núi), còn lại màu cỏ |

**Hiệu năng & hoàn tác:**
- Lưới chia thành **khối 16 × 16 m**, chỉ khối bị cọ chạm mới sinh lại mesh.
- 1 lần nhấn–kéo–thả = 1 Command, lưu **phần chênh lệch** của vùng bị sửa (không lưu cả bản đồ).
- Vật bị ảnh hưởng (nhà, tường, cây…) sinh lại sau khi thả chuột, trong lúc kéo chỉ cập nhật cây và tháp cho nhẹ.

---

### C.10 Xem trong nhà (nội thất)

> Tính năng "có thì tốt" — làm **sau** khi ngoại thất đã chạy ổn (mốc M8).

**Bật:** chọn 1 nhà → click 🔶 **biểu tượng con mắt** trên nhà (hoặc phím **I**).

| Khi đang xem trong | Hành vi |
|---|---|
| Cắt lớp | Mái và các tầng **phía trên** tầng đang xem biến mất (mờ dần 0.3 s); tường phía camera hạ thấp còn 1 m để nhìn vào |
| Đổi tầng | Phím **PageUp / PageDown** hoặc mũi tên 🔼 ở cạnh nhà |
| Thoát | Phím **I** lần nữa, Esc, hoặc click ra ngoài nhà |
| Camera | Tự zoom vào nhà, vẫn xoay được |

**Nội thất tự sinh** (theo tham số nhà, có seed):
| Chi tiết | Quy tắc |
|---|---|
| Chia phòng | Sàn < 20 m² → 1 phòng; 20–40 m² → 2 phòng; > 40 m² → 3 phòng. Vách ngăn vuông góc trục dài, có ô cửa |
| Cầu thang | Nhà ≥ 2 tầng: cầu thang ở góc **xa cửa ra vào nhất**, xuyên các tầng, giữ cùng vị trí |
| Đồ đạc tầng 1 | Bàn + ghế, bếp lò (nằm dưới ống khói nếu có), tủ |
| Đồ đạc tầng 2+ | Giường, rương, tủ |
| Ánh sáng | Ban đêm: đèn trong phòng bật → cửa sổ nhìn từ ngoài sáng theo (H4) |

**Sửa nội thất** (công cụ Bàn tay vẫn dùng được bên trong):
| Thao tác | Kết quả |
|---|---|
| Click đồ đạc | Chọn → 🔶 đổi kiểu (3 biến thể mỗi loại) |
| Kéo đồ đạc | Di chuyển trong phòng, va tường thì dừng, snap 0.25 m |
| R | Xoay 90° |
| Delete | Xoá (game nhớ, không tự sinh lại) |
| Thêm đồ | Thanh nhỏ hiện ở dưới khi đang xem trong: Bàn · Ghế · Giường · Tủ · Đèn · Chậu cây |

Đổi kích thước nhà / số tầng khi đã chỉnh nội thất: đồ đạc người chơi đặt **giữ nguyên** nếu còn nằm trong nhà; đồ nằm ngoài bị đẩy vào trong.

---

## Phần D — Xoá, hoàn tác, an toàn

| Thao tác | Kết quả |
|---|---|
| Công cụ Xóa (phím X) + click | Xoá **phần nhỏ nhất có nghĩa**: 1 đoạn tường giữa 2 nút (không phải cả bức), 1 tháp, cả 1 nhà (bớt tầng thì dùng mũi tên), 1 cây, 1 đoạn đường, 1 ao |
| Công cụ Xóa + kéo | Cọ xoá cây và đường (không xoá nhà/tường để tránh nhầm) |
| Z / Y | Hoàn tác / làm lại, 200 bước |
| Mọi thao tác kéo tay nắm | Thả chuột = 1 Command. Đang kéo mà nhấn chuột phải / Esc → trả về như cũ |

---

## Phần E — Tổ hợp khám phá (móc câu H3 + H5)

Điều kiện được kiểm tra **mỗi khi có thay đổi** (chỉ quanh vật vừa đổi, bán kính 20 m). Khi một tổ hợp xuất hiện lần đầu: chi tiết mọc lên + thông báo nhỏ "✨ Khám phá mới: Cây cầu đá" + ghi vào **Sổ khám phá**.

| # | Tổ hợp | Điều kiện chính xác | Kết quả |
|---|---|---|---|
| E1 | Cổng thành | Đường đi cắt qua đoạn tường đá | Cổng vòm rộng 2.4 m tại chỗ giao |
| E2 | Cầu gỗ | Đường đi cắt qua ao | Đoạn đường trên nước thành cầu ván gỗ |
| E3 | Cầu đá vòm | Tường thành cắt qua ao | Đoạn tường trên nước thành cầu có vòm |
| E4 | Bến nhỏ | Nhà cách bờ ao ≤ 3 m | Cầu tàu gỗ nhỏ từ phía nhà ra ao |
| E5 | Liễu rủ | Cây tròn cách bờ ao ≤ 4 m | Cây đổi dáng thành liễu rủ |
| E6 | Nhà dựa tường | Một cạnh nhà nằm sát tường thành (≤ 0.5 m) | Cạnh đó không có cửa sổ; mái nghiêng tựa vào tường |
| E7 | Giếng làng | ≥ 3 nhà trong vòng 15 m **và** cùng nối với 1 mạng đường | Giếng nước mọc ở chỗ trống gần tâm nhóm nhà |
| E8 | Thành trì | Vòng tường kín có ≥ 1 nhà bên trong | Cờ trên tháp cao nhất + cờ đuôi nheo trên các tháp |
| E9 | Đèn đường | Ban đêm (sau 18:30) + đường đi trong 6 m quanh nhà | Cột đèn lồng mỗi 8 m dọc đường |
| E10 | Đàn vịt | Ao ≥ 60 m² | 3 con vịt (khối nhỏ) bơi chậm theo vòng |
| E11 | Hoa súng | Ao nông + có ≥ 3 cây trong 6 m | Lá súng trên mặt nước |
| E12 | Chuông tháp | Tháp 3 tầng | Chòi chuông trên đỉnh, ban đêm sáng đèn |
| E13 | Vườn rau | Nhà có khoảng trống ≥ 4×3 m phía sau, không cây | Luống rau nhỏ sau nhà |
| E14 | Rừng nhỏ | ≥ 12 cây trong bán kính 10 m | Nấm, hoa dại mọc dưới tán |
| E15 | Lâu đài | Nhà ≥ 3 tầng nằm trong vòng tường kín | Nhà có thêm tháp góc nhỏ trên mái |

**Sổ khám phá** (phím J): lưới 15 ô. Ô đã khám phá: tên + mô tả. Ô chưa khám phá: bóng mờ + gợi ý một dòng (ví dụ E2: *"Đường đi gặp nước thì sao nhỉ?"*).

> Các tổ hợp **không bao giờ** chặn hay phá điều người chơi đã làm — chỉ **thêm** chi tiết. Người chơi tắt từng chi tiết được bằng cách chọn nó và nhấn Delete (game nhớ, không mọc lại chỗ đó).

---

## Phần F — Thế giới sống (móc câu H4)

| Chi tiết | Khi nào | Mức MVP (khối xám) |
|---|---|---|
| Cửa sổ sáng đèn | Sau 18:30 | Ô cửa sổ đổi sang màu vàng phát sáng |
| Khói ống khói | Sau 17:00 | Vài khối cầu nhỏ bay lên rồi mờ dần |
| Cây lớn dần | 60 s sau khi trồng | Scale 0.6 → 1.0 |
| Cây đung đưa | Luôn luôn | Xoay nhẹ ±2° theo sóng sin |
| Mặt nước | Luôn luôn | Gợn lên xuống ±3 cm |
| Vịt (E10) | Ao ≥ 60 m² | Khối nhỏ di chuyển theo vòng |

---

## Phần G — Điều khiển tổng hợp

| Phím / chuột | Chức năng |
|---|---|
| **V** / Esc | Bàn tay (chọn & sửa) — công cụ mặc định |
| **B** | Nhà |
| **G** | Tường thành |
| **K** | Tháp (đặt tay) |
| **T** | Cây |
| **O** | Nước (ao) |
| **H** | Đường đi |
| **P** | Cọ màu |
| **U** | Địa hình (1 Nâng · 2 Hạ · 3 San phẳng · 4 Làm mượt) |
| **I** | Xem trong nhà (khi đang chọn 1 nhà) |
| **[ / ]** | Đổi kích thước cọ (cây, nước, địa hình) hoặc chiều cao khi vẽ tường |
| **X** | Xóa |
| **J** | Sổ khám phá |
| **Z / Y** | Hoàn tác / làm lại |
| Chuột phải giữ | Xoay camera |
| Con lăn | Zoom |
| Chuột giữa giữ / phím mũi tên | Di chuyển camera |

> Lưu ý: prototype M0 đang dùng WASD cho camera. Từ M1, camera chuyển sang **phím mũi tên** để các chữ cái làm phím tắt công cụ (tránh nhầm khi đang xây).

---

## Phần H — Thứ tự làm (ưu tiên "chạy được trước, đẹp sau")

| Mốc | Nội dung | Kết quả chơi được |
|---|---|---|
| **M0** ✅ | Prototype hiện tại: vẽ tường, tháp tự mọc, xoá, undo | Đã có |
| **M1** ✅ | **Khung sườn**: hệ Tham số → Generator → Mesh, Command undo/redo, công cụ **Bàn tay** (chọn, tay nắm, di chuyển, xoay, xoá, nhân bản) + **địa hình lưới độ cao** với công cụ Nâng / Hạ / San phẳng / Làm mượt | Chọn được vật và kéo tay nắm; đắp được đồi |
| **M2** ✅ | **Nhà**: nền chữ nhật + kéo cạnh, số tầng (mũi tên), 4 kiểu mái + độ dốc, cửa/cửa sổ tự sinh, **chân móng tự đắp trên dốc** | Xây và sửa nhà hoàn chỉnh, cả trên đồi |
| **M3** ✅ | **Tường thành dạng đồ thị**: kéo nút, uốn đoạn, chiều cao, kiểu tường, tháp 0–3 tầng, **chân tường bám dốc** | Sửa tường sau khi vẽ |
| **M4** ✅ | **Ao hồ** (lasso + cọ, nắn bờ, mực nước theo bờ thấp nhất) + **Cây** (click, cọ, chọn loại, kích thước) | Có nước và cây |
| **M5** 🟡 | *(Đã code đủ: đường, cọ màu, cánh nhà L/T/U + mái ghép, tầng hẹp dần, ban công, đổi hướng nóc, ao gộp khi nắn bờ)* **Đường đi** (bậc thang tự sinh trên dốc) + **Cọ màu** + nền nhà gộp L/T/U | Đủ bộ công cụ |
| **M6** 🟡 | *(Đã code cả E1–E15)* **Tổ hợp E1–E8** + Sổ khám phá + thế giới sống (Phần F) | Có bất ngờ, có lý do chơi tiếp |
| **M7** 🟡 | *(Đã code: lưu/tải, tự lưu, menu chính, tạm dừng, sổ khám phá)* Lưu/Tải, tổ hợp E9–E15, menu theo Figma | Bản chơi thử hoàn chỉnh (vẫn khối xám) |
| **M8** ✅ | *(Đã xong)* **Xem trong nhà**: cắt lớp mượt 0.3 s, chia phòng, cầu thang, đồ đạc tự sinh, sửa đồ đạc | Nhìn và trang trí được bên trong |
| **M15** ✅ | *(Đã xong cả nhà, tường thành và tháp)* **Nhà & tường thủ công**: móng và tường đá xếp từng viên so le, đá góc tường nhà và tháp, khung gỗ tầng trên (nhà vữa), ván ốp (nhà gỗ / hàng rào gỗ), đá / gạch lộ (nhà đá / gạch), mái lợp từng hàng ngói so le + nóc ngói + ván diềm, song cửa, cửa chớp, mái hiên cửa chính, ống khói đá có nắp | Nhà và tường có chất liệu, có "tay người làm" |
| **M14** 🟡 | *(Đã code, chưa chạy thử)* **Hình ảnh đẹp — bước 1**: URP + SSAO + bóng mềm, shader vẽ tay, bầu trời chuyển màu, hậu kỳ (tô màu, bloom, viền tối, mô hình thu nhỏ) | Cùng khối nhưng có chiều sâu, ánh sáng ấm |
| **M12** 🟡 | *(Đã code)* **Chi tiết nhà & dây leo**: công cụ Cửa sổ (5 kiểu ô cửa, đặt / đổi / xoá), cửa sổ mái, bật/tắt ống khói, cọ dây leo bám theo vật, cổng hàng rào | Mỗi ngôi nhà có cá tính riêng |
| **M11** 🟡 | *(Đã code)* **Làng ấm cúng**: cọ tô đất (hoa dại / sỏi / cát / đất), 10 đồ trang trí đặt tay, gà · cừu · chim · bướm, hộp hoa dưới cửa sổ | Trang trí từng góc làng, làng có sự sống |
| **M10** 🟡 | *(Đã code)* **Xây tự do bằng khối**: 14 loại mảnh, 9 chất liệu, tự hít mặt, kéo xếp hàng / mảng / tường, xoay 90° / 15°, tháo, thay, lấy mẫu, sửa từng mảnh bằng Bàn tay | Tự ghép cầu, tháp, căn cứ tùy ý |
| **M9** 🟡 | *(Đã code bản procedural: âm thanh + âm nền tự tổng hợp, cỏ hoa, khung cửa sổ, viền tối, chụp ảnh F, cài đặt; chưa có model / nhạc thật)* Làm đẹp: model, âm thanh, ánh sáng | — |

### Tiêu chí "đã chạy được" cho từng mốc
- **M1**: chọn tháp → kéo mũi tên → tháp cao lên; Z → về như cũ. Đắp 1 ngọn đồi 6 m → tháp đứng trên đồi tự nâng theo; Z → đồi biến mất, tháp hạ xuống.
- **M2**: kéo nhà 6×4 → kéo cạnh thành 6×8 → kéo mũi tên lên 3 tầng → click hình thoi đổi mái 4 dốc → kéo độ dốc 55° → Z bốn lần về nhà ban đầu.
- **M3**: vẽ tường chữ L → kéo nút góc đi 5 m → hai đoạn uốn theo, tháp đi theo.
- **M4**: vẽ vòng ao → kéo tay nắm nắn bờ → quét cọ cây quanh ao → cây không mọc trên nước.
- **M6**: vẽ đường cắt ngang tường → cổng xuất hiện + "✨ Khám phá mới" + Sổ khám phá 1/15.

---

## Phần I — Quyết định đã chốt

| Câu hỏi | Quyết định | Ảnh hưởng |
|---|---|---|
| Địa hình nâng/hạ đồi | **Có** | Thêm C.9; lưới độ cao làm ngay từ M1 vì mọi thứ đứng trên nó |
| Xem trong nhà | **Có thì tốt** | Thêm C.10; làm ở M8, sau khi ngoại thất ổn |
| Dân làng tí hon | **Chưa cần** | Không làm trong bản này; "thế giới sống" dựa vào đèn, khói, cây, vịt |
| Số tổ hợp khám phá | **15 là tạm đủ** | Giữ E1–E15 |
| Mùa + thời tiết + cầu vồng (M16) | **Có** | Toàn bộ qua biến toàn cục của shader, không cần dựng lại vật; lưu theo làng |
