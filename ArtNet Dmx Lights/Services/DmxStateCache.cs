namespace ArtNet_Dmx_Lights.Services;

public sealed class DmxStateCache
{
    private readonly object _lock = new();
    private readonly int[][] _current =
    [
        new int[512],
        new int[512]
    ];

    public int[][] GetSnapshot()
    {
        lock (_lock)
        {
            return _current.Select(values => values.ToArray()).ToArray();
        }
    }

    public void SetState(int[][] state)
    {
        lock (_lock)
        {
            for (var universe = 0; universe < _current.Length; universe++)
            {
                Array.Copy(state[universe], _current[universe], _current[universe].Length);
            }
        }
    }
}
