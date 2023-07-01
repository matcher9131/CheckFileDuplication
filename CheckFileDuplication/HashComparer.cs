using System.Diagnostics.CodeAnalysis;

namespace CheckFileDuplication
{
    /// <summary>
    /// hash（実際にはbyte[]）が等しいかどうかを比べるためのクラス
    /// </summary>
    public class HashComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            return (x ?? Array.Empty<byte>()).SequenceEqual(y ?? Array.Empty<byte>());
        }

        public int GetHashCode([DisallowNull] byte[] obj)
        {
            return obj.Aggregate(0, (x, y) => x ^ y);
        }
    }
}
