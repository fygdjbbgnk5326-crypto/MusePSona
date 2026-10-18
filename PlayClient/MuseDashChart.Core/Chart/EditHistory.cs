namespace MuseDashChart;

/// <summary>
/// 文档快照撤销/重做。每次 Push 记下操作前的整份谱，新操作清空 redo。
/// 拖动长条等手势由调用方保证只 Push 一次。
/// </summary>
public sealed class EditHistory
{
    public const int MaxDepth = 30;

    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];

    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void Push(ChartDocument document)
    {
        _undo.Add(ProjectJson.Serialize(document));
        if (_undo.Count > MaxDepth)
            _undo.RemoveAt(0);
        _redo.Clear();
    }

    public bool TryUndo(ChartDocument current, out ChartDocument restored)
    {
        restored = null!;
        if (_undo.Count == 0) return false;
        _redo.Add(ProjectJson.Serialize(current));
        restored = ProjectJson.Deserialize(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
        return true;
    }

    public bool TryRedo(ChartDocument current, out ChartDocument restored)
    {
        restored = null!;
        if (_redo.Count == 0) return false;
        _undo.Add(ProjectJson.Serialize(current));
        if (_undo.Count > MaxDepth)
            _undo.RemoveAt(0);
        restored = ProjectJson.Deserialize(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
        return true;
    }
}

