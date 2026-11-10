using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>Một thao tác hoàn tác được.</summary>
    public interface ICommand
    {
        void Do();
        void Undo();
    }

    /// <summary>Tạo vật mới. Lần đầu Do() có hiệu ứng nảy; redo thì không.</summary>
    public class CreateEntityCommand : ICommand
    {
        readonly string type, json;
        float popDelay;
        public int Id;

        public CreateEntityCommand(string type, string json, float popDelay = -1f, int existingId = 0)
        {
            this.type = type;
            this.json = json;
            this.popDelay = popDelay;
            Id = existingId;
        }

        public void Do()
        {
            var e = EntityManager.Spawn(type, json, Id, popDelay);
            Id = e.Id;
            popDelay = -1f;
        }

        public void Undo() => EntityManager.Destroy(Id);
    }

    public class DeleteEntityCommand : ICommand
    {
        readonly string type, json;
        readonly int id;

        public DeleteEntityCommand(Entity e)
        {
            type = e.TypeName;
            json = e.Capture();
            id = e.Id;
        }

        public void Do() => EntityManager.Destroy(id);
        public void Undo() => EntityManager.Spawn(type, json, id);
    }

    /// <summary>Đổi tham số của 1 vật (kéo tay nắm, di chuyển, xoay...).</summary>
    public class ModifyEntityCommand : ICommand
    {
        readonly int id;
        readonly string before, after;

        public ModifyEntityCommand(int id, string before, string after)
        {
            this.id = id;
            this.before = before;
            this.after = after;
        }

        public void Do() => EntityManager.Get(id)?.Restore(after);
        public void Undo() => EntityManager.Get(id)?.Restore(before);
    }

    /// <summary>Gộp nhiều lệnh thành 1 bước hoàn tác (ví dụ: 1 nét tường = tường + các tháp).</summary>
    public class CompositeCommand : ICommand
    {
        readonly List<ICommand> list = new();

        public void Add(ICommand c) => list.Add(c);
        public int Count => list.Count;

        public void Do()
        {
            foreach (var c in list) c.Do();
        }

        public void Undo()
        {
            for (int i = list.Count - 1; i >= 0; i--) list[i].Undo();
        }
    }

    /// <summary>1 lần nhấn–kéo–thả cọ địa hình. Chỉ lưu vùng bị sửa.</summary>
    public class TerrainCommand : ICommand
    {
        readonly RectInt rect;
        readonly float[] before, after;

        public TerrainCommand(RectInt rect, float[] before, float[] after)
        {
            this.rect = rect;
            this.before = before;
            this.after = after;
        }

        public void Do() => HeightField.I.SetRegion(rect, after);
        public void Undo() => HeightField.I.SetRegion(rect, before);
    }
}
