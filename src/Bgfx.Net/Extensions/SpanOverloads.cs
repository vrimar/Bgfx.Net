namespace Bgfx.Net;

// Span<T>/ReadOnlySpan<T> overloads for bgfx APIs that take a (void* data, length) pair.
//
// Only methods where bgfx copies the data internally (e.g. Copy, DbgTextImage) are
// exposed via Span overloads. Methods like MakeRef/MakeRefRelease intentionally
// have no Span variant because they retain a pointer past the call — a stack-only
// Span cannot safely satisfy that lifetime contract; callers must pin manually.

public static partial class Bgfx
{
    /// <summary>
    /// Allocates and copies <paramref name="data"/> into a bgfx-owned <see cref="Memory"/> block.
    /// </summary>
    public static unsafe Memory* Copy<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        fixed (T* p = data)
        {
            return Copy(p, checked((uint)(data.Length * sizeof(T))));
        }
    }

    /// <summary>
    /// Draws raw image data into the debug text buffer.
    /// </summary>
    public static unsafe void DbgTextImage(
        ushort x, ushort y, ushort width, ushort height, ReadOnlySpan<byte> data, ushort pitch)
    {
        fixed (byte* p = data)
        {
            DbgTextImage(x, y, width, height, p, pitch);
        }
    }
}
