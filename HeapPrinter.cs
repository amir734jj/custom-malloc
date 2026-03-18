using Xunit.Abstractions;

namespace csharp_test;

internal static class HeapPrinter
{
    private record Block(nuint Size, bool Free);

    private const int Scale = 8;  // bytes per bar cell

    public static void Print(string label, ITestOutputHelper output)
    {
        var blocks = new List<Block>();

        Heap.BlockVisitor cb = (size, isFree, _) => blocks.Add(new Block(size, isFree != 0));
        Heap.VisitBlocks(cb, IntPtr.Zero);

        output.WriteLine($"\n  ── {label} ──");

        nuint used = 0, free = 0;
        foreach (var b in blocks)
        {
            var cells = (int)Math.Max(1, b.Size / Scale);
            string bar = new(b.Free ? '.' : '#', cells);
            var tag = b.Free ? "FREE" : "USED";
            output.WriteLine($"  [{tag} {b.Size,5} B] {bar}");

            if (b.Free)
            {
                free += b.Size;
            }
            else
            {
                used += b.Size;
            }
        }

        if (blocks.Count == 0)
        {
            output.WriteLine("  (empty)");
        }

        var total = used + free;
        var fragPct = total == 0 ? 0 : free * 100 / total;
        output.WriteLine($"  {new string('-', 40)}");
        output.WriteLine($"  used={used} B   free={free} B   fragmentation≈{fragPct}%");
    }
}