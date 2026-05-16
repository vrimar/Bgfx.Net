using System.Reflection;
using Bgfx.Net;

// Minimal AOT-friendly entry point.
//
// Goal: validate that Bgfx.Net's generated bindings compile under PublishAot without
// trim/AOT warnings. We do not call into bgfx_init here because the native lib may
// not be deployed alongside the AOT executable in all CI lanes; the test is purely
// compile-time + boot-time.

var revision = typeof(ShaderHandle).Assembly
    .GetCustomAttributes<AssemblyMetadataAttribute>()
    .FirstOrDefault(a => a.Key == "BgfxRevision")?.Value;

Console.WriteLine($"Bgfx.Net AOT sample. Pinned bgfx revision: {revision ?? "(unknown)"}.");

// Exercise a handful of value-type APIs to keep them rooted under trimming.
var handle = new ShaderHandle(ushort.MaxValue);
Console.WriteLine($"Default handle valid? {handle.Valid}");

return 0;
