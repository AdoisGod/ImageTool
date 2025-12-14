using VisionVerificationToolkit.Objects;

namespace VisionVerificationToolkit.Core;

/// <summary>
/// 結果管理器 - 管理檢測物件與量測結果
/// </summary>
public class ResultManager
{
    private readonly List<IGeometryObject> _objects = new();

    public event EventHandler? ObjectsChanged;
    public event EventHandler<IGeometryObject>? ObjectAdded;
    public event EventHandler<IGeometryObject>? ObjectRemoved;
    public event EventHandler<IGeometryObject>? ObjectSelected;

    public IReadOnlyList<IGeometryObject> Objects => _objects.AsReadOnly();
    public IGeometryObject? SelectedObject { get; private set; }

    public IEnumerable<PointObject> Points => _objects.OfType<PointObject>();
    public IEnumerable<LineObject> Lines => _objects.OfType<LineObject>();
    public IEnumerable<CircleObject> Circles => _objects.OfType<CircleObject>();
    public IEnumerable<ContourObject> Contours => _objects.OfType<ContourObject>();
    public IEnumerable<MeasurementResult> Measurements => _objects.OfType<MeasurementResult>();

    public void Add(IGeometryObject obj)
    {
        _objects.Add(obj);
        ObjectAdded?.Invoke(this, obj);
        ObjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(IGeometryObject obj)
    {
        if (_objects.Remove(obj))
        {
            if (SelectedObject == obj)
                SelectedObject = null;
            ObjectRemoved?.Invoke(this, obj);
            ObjectsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear()
    {
        _objects.Clear();
        SelectedObject = null;
        ObjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Select(IGeometryObject? obj)
    {
        if (SelectedObject != null)
            SelectedObject.IsSelected = false;

        SelectedObject = obj;

        if (obj != null)
        {
            obj.IsSelected = true;
            ObjectSelected?.Invoke(this, obj);
        }
    }

    public IGeometryObject? HitTest(System.Drawing.PointF point, float tolerance = 5f)
    {
        // 優先選擇點（最小的物件）
        foreach (var obj in _objects.OfType<PointObject>().Where(o => o.IsVisible))
        {
            if (obj.HitTest(point, tolerance))
                return obj;
        }

        // 其次是線
        foreach (var obj in _objects.OfType<LineObject>().Where(o => o.IsVisible))
        {
            if (obj.HitTest(point, tolerance))
                return obj;
        }

        // 然後是圓
        foreach (var obj in _objects.OfType<CircleObject>().Where(o => o.IsVisible))
        {
            if (obj.HitTest(point, tolerance))
                return obj;
        }

        // 最後是輪廓和量測
        foreach (var obj in _objects.Where(o => o.IsVisible && !(o is PointObject || o is LineObject || o is CircleObject)))
        {
            if (obj.HitTest(point, tolerance))
                return obj;
        }

        return null;
    }

    public IGeometryObject? GetById(string id)
    {
        return _objects.FirstOrDefault(o => o.Id == id);
    }

    public IGeometryObject? GetByName(string name)
    {
        return _objects.FirstOrDefault(o => o.Name == name);
    }

    public IEnumerable<T> GetObjects<T>() where T : IGeometryObject
    {
        return _objects.OfType<T>();
    }

    public void SetVisibility(IGeometryObject obj, bool visible)
    {
        obj.IsVisible = visible;
        ObjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleVisibility(IGeometryObject obj)
    {
        obj.IsVisible = !obj.IsVisible;
        ObjectsChanged?.Invoke(this, EventArgs.Empty);
    }
}
