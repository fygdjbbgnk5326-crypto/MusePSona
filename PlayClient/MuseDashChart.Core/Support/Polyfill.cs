using System.Text;

namespace MuseDashChart;

internal sealed class RefEq<T> : IEqualityComparer<T> where T : class
{
    public static readonly RefEq<T> Instance = new();
    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}

internal static class Latin1
{
    public static Encoding Encoding =>
#if NETSTANDARD
        System.Text.Encoding.GetEncoding(28591);
#else
        System.Text.Encoding.Latin1;
#endif
}

