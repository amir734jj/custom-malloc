using Xunit;
using Xunit.Abstractions;

namespace csharp_test;

public class HeapTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public HeapTests(ITestOutputHelper output)
    {
        _output = output;
        Heap.Reset();
    }

    private static (nuint used, nuint free) Stats()
    {
        nuint used = 0, free = 0;
        var cb = new Heap.BlockVisitor((size, isFree, _) =>
        {
            if (isFree != 0)
            {
                free += size;
            }
            else
            {
                used += size;
            }
        });
        Heap.VisitBlocks(cb, IntPtr.Zero);
        return (used, free);
    }


    [Fact]
    public void Malloc_ReturnsNonNull()
    {
        var p = Heap.Malloc(64);
        Assert.NotEqual(IntPtr.Zero, p);
        Heap.Free(p);
    }

    [Fact]
    public void Malloc_Zero_ReturnsNull()
    {
        var p = Heap.Malloc(0);
        Assert.Equal(IntPtr.Zero, p);
    }

    [Fact]
    public void Free_ReleasesMemory()
    {
        var p = Heap.Malloc(64);
        Heap.Free(p);
        var (used, free) = Stats();
        Assert.Equal((nuint)0, used);
        Assert.Equal((nuint)0, free);
    }

    [Fact]
    public void Malloc_MultipleBlocks_AllNonNull()
    {
        int[] sizes = [32, 64, 128, 256];
        var ptrs = sizes.Select(s => Heap.Malloc((nuint)s)).ToArray();

        Assert.All(ptrs, p => Assert.NotEqual(IntPtr.Zero, p));

        foreach (var p in ptrs) { Heap.Free(p); }
    }

    [Fact]
    public void Malloc_MultipleBlocks_UniqueAddresses()
    {
        var a = Heap.Malloc(64);
        var b = Heap.Malloc(64);
        Assert.NotEqual(a, b);
        Heap.Free(a);
        Heap.Free(b);
    }

    [Fact]
    public void Free_Coalesces_FragmentedBlocks()
    {
        int[] sizes = [32, 64, 128, 32, 256, 64];
        var ptrs = sizes.Select(s => Heap.Malloc((nuint)s)).ToArray();

        for (var i = 0; i < ptrs.Length; i += 2) { Heap.Free(ptrs[i]); }
        for (var i = 1; i < ptrs.Length; i += 2) { Heap.Free(ptrs[i]); }

        var blocks = new List<(nuint size, bool free)>();
        Heap.BlockVisitor cb = (size, isFree, _) => blocks.Add((size, isFree != 0));
        Heap.VisitBlocks(cb, IntPtr.Zero);

        Assert.True(blocks.Count <= 1);
        Assert.All(blocks, b => Assert.True(b.free));
    }

    [Fact]
    public void Stats_ReflectsFragmentation()
    {
        var a = Heap.Malloc(64);
        var b = Heap.Malloc(128);
        var c = Heap.Malloc(64); // pin: keeps b from being the tail

        Heap.Free(b);

        HeapPrinter.Print("fragmented: a(64) free(128) c(64)", _output);
        var (used, free) = Stats();
        Assert.Equal((nuint)(64 + 64), used); // a + c still allocated
        Assert.Equal((nuint)128, free); // b's payload is free
        Heap.Free(a);
        Heap.Free(c);
    }

    [Fact]
    public void Malloc_ReusesFreedBlock()
    {
        var first = Heap.Malloc(64);
        Heap.Free(first);
        var second = Heap.Malloc(64);
        // The allocator should hand back the same slot.
        Assert.Equal(first, second);
        Heap.Free(second);
    }

    [Fact]
    public void Calloc_ReturnsZeroedMemory()
    {
        var p = Heap.Calloc(16, 4); // 64 bytes
        Assert.NotEqual(IntPtr.Zero, p);

        unsafe
        {
            var span = new Span<byte>((void*)p, 64);
            Assert.All(span.ToArray(), b => Assert.Equal(0, b));
        }

        Heap.Free(p);
    }

    [Fact]
    public void Calloc_Zero_ReturnsNull()
    {
        Assert.Equal(IntPtr.Zero, Heap.Calloc(0, 4));
        Assert.Equal(IntPtr.Zero, Heap.Calloc(4, 0));
    }

    [Fact]
    public void Realloc_Null_ActsLikeMalloc()
    {
        var p = Heap.Realloc(IntPtr.Zero, 64);
        Assert.NotEqual(IntPtr.Zero, p);
        Heap.Free(p);
    }

    [Fact]
    public void Realloc_ZeroSize_ActsLikeFree()
    {
        var p = Heap.Malloc(64);
        var result = Heap.Realloc(p, 0);
        Assert.Equal(IntPtr.Zero, result);
    }

    [Fact]
    public void Realloc_SmallerSize_ReturnsSamePointer()
    {
        var p = Heap.Malloc(128);
        var p2 = Heap.Realloc(p, 64);
        Assert.Equal(p, p2); // shrink-in-place
        Heap.Free(p2);
    }

    [Fact]
    public void Realloc_InPlace_WhenFreeNeighbourExists()
    {
        var a = Heap.Malloc(64);
        var nb = Heap.Malloc(128);
        var pin = Heap.Malloc(8); // prevents sbrk shrink from eating nb when freed
        Heap.Free(nb); // nb is now a free hole between a and pin

        var a2 = Heap.Realloc(a, 160);
        Assert.Equal(a, a2); // should extend in-place, same pointer
        Heap.Free(a2);
        Heap.Free(pin);
    }

    [Fact]
    public void Realloc_PreservesData()
    {
        var p = Heap.Malloc(64);
        unsafe
        {
            *(long*)p = 0xDEADBEEF;
        }

        var p2 = Heap.Realloc(p, 128);
        Assert.NotEqual(IntPtr.Zero, p2);
        unsafe
        {
            Assert.Equal(0xDEADBEEF, *(long*)p2);
        }

        Heap.Free(p2);
    }

    public void Dispose()
    {
        var (used, free) = Stats();
        if (used > 0 || free > 0)
        {
            HeapPrinter.Print("LEAK — heap non-empty at end of test", _output);
            throw new Exception("Memory leak detected");
        }

        Heap.Reset();
        GC.SuppressFinalize(this);
    }
}
