namespace wsc.Core;

public class HuffmanNode(int symbol, Uint frequency)
{
    public int Symbol { get; set; } = symbol;
    public Uint Frequency { get; set; } = frequency;
    public HuffmanNode? Left { get; set; } // Left child (0 branch)
    public HuffmanNode? Right { get; set; } // Right child (1 branch)
}
