namespace Bgfx.Net;

// Companion file for handle structs (emitted partial + readonly by the generator).
//
// The generator emits each handle as `public readonly partial struct XHandle`
// with a public readonly `idx` field, a `Valid` property, and a public
// `XHandle(ushort idx)` constructor. This file adds value-equality.
//
// IDisposable is intentionally NOT implemented here for v1: bgfx's destroy_*
// functions are distinct per handle type and the matching is brittle to
// automate. The convention for v1 is to call the corresponding Bgfx.Destroy*
// function explicitly. A follow-up can add Dispose() per handle once the API
// surface is stable.

public readonly partial struct ProgramHandle : IEquatable<ProgramHandle>
{
    public bool Equals(ProgramHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is ProgramHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(ProgramHandle left, ProgramHandle right) => left.Equals(right);
    public static bool operator !=(ProgramHandle left, ProgramHandle right) => !left.Equals(right);
}

public readonly partial struct ShaderHandle : IEquatable<ShaderHandle>
{
    public bool Equals(ShaderHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is ShaderHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(ShaderHandle left, ShaderHandle right) => left.Equals(right);
    public static bool operator !=(ShaderHandle left, ShaderHandle right) => !left.Equals(right);
}

public readonly partial struct TextureHandle : IEquatable<TextureHandle>
{
    public bool Equals(TextureHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is TextureHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(TextureHandle left, TextureHandle right) => left.Equals(right);
    public static bool operator !=(TextureHandle left, TextureHandle right) => !left.Equals(right);
}

public readonly partial struct VertexBufferHandle : IEquatable<VertexBufferHandle>
{
    public bool Equals(VertexBufferHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is VertexBufferHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(VertexBufferHandle left, VertexBufferHandle right) => left.Equals(right);
    public static bool operator !=(VertexBufferHandle left, VertexBufferHandle right) => !left.Equals(right);
}

public readonly partial struct IndexBufferHandle : IEquatable<IndexBufferHandle>
{
    public bool Equals(IndexBufferHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is IndexBufferHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(IndexBufferHandle left, IndexBufferHandle right) => left.Equals(right);
    public static bool operator !=(IndexBufferHandle left, IndexBufferHandle right) => !left.Equals(right);
}

public readonly partial struct UniformHandle : IEquatable<UniformHandle>
{
    public bool Equals(UniformHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is UniformHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(UniformHandle left, UniformHandle right) => left.Equals(right);
    public static bool operator !=(UniformHandle left, UniformHandle right) => !left.Equals(right);
}

public readonly partial struct FrameBufferHandle : IEquatable<FrameBufferHandle>
{
    public bool Equals(FrameBufferHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is FrameBufferHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(FrameBufferHandle left, FrameBufferHandle right) => left.Equals(right);
    public static bool operator !=(FrameBufferHandle left, FrameBufferHandle right) => !left.Equals(right);
}

public readonly partial struct OcclusionQueryHandle : IEquatable<OcclusionQueryHandle>
{
    public bool Equals(OcclusionQueryHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is OcclusionQueryHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(OcclusionQueryHandle left, OcclusionQueryHandle right) => left.Equals(right);
    public static bool operator !=(OcclusionQueryHandle left, OcclusionQueryHandle right) => !left.Equals(right);
}

public readonly partial struct IndirectBufferHandle : IEquatable<IndirectBufferHandle>
{
    public bool Equals(IndirectBufferHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is IndirectBufferHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(IndirectBufferHandle left, IndirectBufferHandle right) => left.Equals(right);
    public static bool operator !=(IndirectBufferHandle left, IndirectBufferHandle right) => !left.Equals(right);
}

public readonly partial struct VertexLayoutHandle : IEquatable<VertexLayoutHandle>
{
    public bool Equals(VertexLayoutHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is VertexLayoutHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(VertexLayoutHandle left, VertexLayoutHandle right) => left.Equals(right);
    public static bool operator !=(VertexLayoutHandle left, VertexLayoutHandle right) => !left.Equals(right);
}

public readonly partial struct DynamicVertexBufferHandle : IEquatable<DynamicVertexBufferHandle>
{
    public bool Equals(DynamicVertexBufferHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is DynamicVertexBufferHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(DynamicVertexBufferHandle left, DynamicVertexBufferHandle right) => left.Equals(right);
    public static bool operator !=(DynamicVertexBufferHandle left, DynamicVertexBufferHandle right) => !left.Equals(right);
}

public readonly partial struct DynamicIndexBufferHandle : IEquatable<DynamicIndexBufferHandle>
{
    public bool Equals(DynamicIndexBufferHandle other) => idx == other.idx;
    public override bool Equals(object? obj) => obj is DynamicIndexBufferHandle other && Equals(other);
    public override int GetHashCode() => idx;
    public static bool operator ==(DynamicIndexBufferHandle left, DynamicIndexBufferHandle right) => left.Equals(right);
    public static bool operator !=(DynamicIndexBufferHandle left, DynamicIndexBufferHandle right) => !left.Equals(right);
}
