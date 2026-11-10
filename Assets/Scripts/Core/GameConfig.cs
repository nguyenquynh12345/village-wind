namespace LangGio
{
    /// <summary>
    /// Mọi con số "cảm giác" của game ở một chỗ (theo Docs/CoChe-Gameplay.md).
    /// Chỉnh ở đây khi chơi thử, không viết cứng trong code khác.
    /// </summary>
    public static class GameConfig
    {
        // ---- Thế giới ----
        public const float WorldHalf = 96f;        // mặt đất từ -96 → +96 m
        public const float BuildRadius = 90f;      // vùng được xây
        public const float CellSize = 1f;          // ô lưới độ cao
        public const int ChunkCells = 16;          // 1 khối mesh địa hình = 16 x 16 ô
        public const float MinHeight = -3f;
        public const float MaxHeight = 12f;

        // ---- Cọ địa hình ----
        public const float BrushMin = 2f;
        public const float BrushMax = 15f;
        public const float BrushDefault = 5f;
        public const float BrushRate = 2f;         // m/giây ở tâm cọ
        public const float RockSlope = 40f;        // dốc hơn → màu đá

        // ---- Tường ----
        public static readonly float[] WallHeights = { 1.6f, 2.6f, 3.6f, 4.6f };
        public const int WallHeightDefault = 1;
        public const float WallThickness = 0.9f;
        public const float WallStep = 0.8f;        // khoảng cách điểm trên tường
        public const float SampleDist = 0.35f;     // lấy mẫu nét vẽ
        public const float SimplifyEps = 0.6f;     // RDP tìm góc gấp
        public const float CornerAngle = 40f;      // góc gấp > số này → mọc tháp
        public const float MinWallLength = 1.2f;
        public const float CloseLoopDist = 1.6f;
        public const float CloseLoopMinLength = 8f;
        public const float MerlonSpacing = 1.2f;
        public const float MerlonTowerGap = 1.7f;

        public const float WallMaxSegment = 24f;      // R5: đoạn dài hơn → chèn tháp giữa
        public const float SegmentSnap = 1.2f;        // hút vào thân tường
        public const float IntersectEndGap = 1.0f;    // không tạo ngã tư quá sát đầu đoạn
        public static readonly float[] WallStyleThickness = { 0.9f, 0.9f, 0.35f, 0.6f };
        public const float LowWallHeight = 1.0f;      // kiểu "Bờ rào thấp"

        // ---- Tháp ----
        public const float TowerSnap = 1.8f;       // hút nét vẽ / click vào tháp
        public const float TowerMinSpacing = 4f;
        public static readonly float[] TowerRadius = { 1.0f, 1.15f, 1.4f, 1.7f };  // theo số tầng (index 1..3)
        public static readonly float[] TowerExtra = { 0f, 2.4f, 4.4f, 6.4f };      // cao hơn tường chuẩn
        public static readonly float[] TowerRoofHeight = { 0f, 2.3f, 2.8f, 3.4f };

        // ---- Nhà ----
        public const float HouseMinSize = 3f;
        public const float HouseMaxWidth = 10f;
        public const float HouseMaxLength = 16f;
        public const float HouseSnap = 0.5f;          // kích thước nền làm tròn 0.5 m
        public const float HouseAngleSnap = 15f;
        public const float FloorHeight = 2.8f;
        public const int MaxFloors = 4;
        public const float HouseMaxSlope = 3f;        // chênh đất dưới nền tối đa khi đặt
        public const float FoundationHeight = 0.3f;
        public const float WindowSpacing = 2.2f;
        public const float RoofOverhang = 0.3f;
        public const float PitchMin = 20f, PitchMax = 60f, PitchDefault = 40f;
        public const float ChimneyMinArea = 30f;
        public const float ConeRoofMaxSize = 5f;
        public const float FloorDragStep = 2.8f;      // kéo mũi tên 2.8 m = 1 tầng
        public const float PitchPerMeter = 12f;       // kéo mũi tên 1 m = 12°

        // ---- Ao hồ ----
        public const float PondCell = 0.5f;           // ô mặt nạ ao
        public const float PondShallow = 0.4f, PondDeep = 1.2f;
        public const float PondMinArea = 4f;          // m²
        public const float PondBank = 1.0f;           // bờ dốc thoải ~1 m
        public const float PondBrushDefault = 2.5f;
        public const float PondHandleFalloff = 2.5f;  // kéo tay nắm bờ: ảnh hưởng ~2.5 m quanh đó

        // ---- Cây ----
        public const float TreeSpacing = 2.2f;
        public const float TreeDensity = 6f;          // 1 cây / 6 m²
        public const float TreeBrushDefault = 3f;
        public const float TreeMaxSlope = 35f;
        public const float TreeGrowTime = 60f;
        public const float TreeMinSize = 0.6f, TreeMaxSize = 1.6f;

        // ---- Thao tác ----
        public const float DragThresholdPx = 5f;
        public const float TowerLevelDragStep = 2f;   // kéo mũi tên 2 m = 1 tầng
        public const float WallHeightDragStep = 1f;
        public const int HistoryLimit = 200;

        // ---- Dạo làng (S5) ----
        public const float WalkSpeed = 3.2f;
        public const float RunSpeed = 6f;
        public const float EyeHeight = 1.6f;
        public const float WalkMaxSlope = 50f;
    }
}
