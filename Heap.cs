using System.Runtime.InteropServices;

namespace csharp_test;

internal static partial class Heap
{
    private const string Lib = "libcustom_malloc.so";

    [LibraryImport(Lib, EntryPoint = "custom_malloc")]
    public static partial IntPtr Malloc(nuint size);

    [LibraryImport(Lib, EntryPoint = "custom_free")]
    public static partial void Free(IntPtr ptr);

    [LibraryImport(Lib, EntryPoint = "custom_calloc")]
    public static partial IntPtr Calloc(nuint nmemb, nuint size);

    [LibraryImport(Lib, EntryPoint = "custom_realloc")]
    public static partial IntPtr Realloc(IntPtr ptr, nuint size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void BlockVisitor(nuint size, int isFree, IntPtr userData);

    [LibraryImport(Lib, EntryPoint = "custom_malloc_visit_blocks")]
    public static partial void VisitBlocks(BlockVisitor visitor, IntPtr userData);

    [LibraryImport(Lib, EntryPoint = "custom_malloc_reset")]
    public static partial void Reset();
}
