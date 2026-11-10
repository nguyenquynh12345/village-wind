using System.Collections.Generic;
using UnityEngine;

namespace LangGio
{
    /// <summary>Lịch sử hoàn tác / làm lại (tối đa <see cref="GameConfig.HistoryLimit"/> bước).</summary>
    public static class History
    {
        static readonly List<ICommand> undo = new(), redo = new();

        /// <summary>Thế giới vừa đổi (thêm lệnh, hoàn tác, làm lại, tải làng) — để tổ hợp / tự động lưu cập nhật.</summary>
        public static event System.Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { undo.Clear(); redo.Clear(); Changed = null; }

        public static void Clear()
        {
            undo.Clear();
            redo.Clear();
            Changed?.Invoke();
        }

        public static void NotifyChanged() => Changed?.Invoke();

        public static bool CanUndo => undo.Count > 0;
        public static bool CanRedo => redo.Count > 0;

        /// <summary>Thực hiện lệnh rồi ghi vào lịch sử.</summary>
        public static void Execute(ICommand c)
        {
            c.Do();
            Push(c);
        }

        /// <summary>Ghi lệnh đã được áp dụng sẵn (ví dụ sau khi kéo tay nắm xong).</summary>
        public static void Push(ICommand c)
        {
            redo.Clear();
            undo.Add(c);
            if (undo.Count > GameConfig.HistoryLimit) undo.RemoveAt(0);
            if (c is DeleteEntityCommand) Sfx.Play(SfxId.Delete, 1f, 0.5f);
            else if (!(c is CreateEntityCommand)) Sfx.Play(SfxId.Place, Random.Range(0.9f, 1.1f), 0.45f);
            Changed?.Invoke();
        }

        public static void Undo()
        {
            if (undo.Count == 0) return;
            var c = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            c.Undo();
            redo.Add(c);
            Sfx.Play(SfxId.Undo, 1f, 0.5f);
            Changed?.Invoke();
        }

        public static void Redo()
        {
            if (redo.Count == 0) return;
            var c = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            c.Do();
            undo.Add(c);
            Sfx.Play(SfxId.Redo, 1f, 0.5f);
            Changed?.Invoke();
        }
    }
}
