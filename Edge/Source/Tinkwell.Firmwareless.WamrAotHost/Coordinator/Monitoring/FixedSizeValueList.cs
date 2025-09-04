namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator.Monitoring;

sealed class FixedSizeValueList
{
    public FixedSizeValueList(int capacity)
        => _values = new List<double>(capacity);

    public int Count
        => _values.Count;

    public double? Current
        => _values.Count == 0 ? null : _values[_values.Count - 1];

    public void Clear()
        => _values.Clear();

    public void Push(double value)
    {
        if (_values.Count == _values.Capacity)
            _values.RemoveAt(0);

        _values.Add(value);
    }

    public double? ExponentialMovingAverage(double? alpha = default)
    {
        if (_values.Count == 0)
            return null;

        double a = alpha is null ? 2.0 / (_values.Count + 1) : alpha.Value;
        double result = _values[0];
        for (int i=1; i < _values.Count; ++i)
            result = (1 - a) * result + a * _values[i];

        return result;
    }

    private readonly List<double> _values;
}