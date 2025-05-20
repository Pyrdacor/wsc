namespace PYC.Core;

public class HuffmanTree(Uint[] frequencies)
{
    private readonly HuffmanNode root = BuildHuffmanTree(frequencies);

    private static HuffmanNode BuildHuffmanTree(Uint[] frequencies)
    {
        // Min-heap to store nodes sorted by frequency
        var pq = new SortedSet<HuffmanNode>(Comparer<HuffmanNode>.Create((a, b) =>
        {
            int cmp = a.Frequency.CompareTo(b.Frequency);
            return cmp == 0 ? a.Symbol.CompareTo(b.Symbol) : cmp;
        }));

        // Create a leaf node for each symbol
        ushort symbol = 0;
        foreach (var freq in frequencies)
        {
            if (freq != 0)
                pq.Add(new HuffmanNode(symbol, freq));
            
            symbol++;
        }

        int internalSymbol = int.MinValue;

        // Build the tree by merging the two lowest frequency nodes
        while (pq.Count > 1)
        {
            var left = pq.Min!;
            pq.Remove(left);
            var right = pq.Min!;
            pq.Remove(right);

            // Merge into a new node (internal node)
            var parent = new HuffmanNode(internalSymbol++, left.Frequency + right.Frequency)
            {
                Left = left,
                Right = right
            };

            pq.Add(parent);
        }

        return pq.First(); // Root of the Huffman tree
    }

    private Dictionary<Word, Uint> GetHuffmanCodeLengths()
    {
        var codeLengths = new Dictionary<Word, Uint>();

        void Traverse(HuffmanNode? node, Uint depth)
        {
            if (node == null) return;

            if (node.Left == null && node.Right == null) // Leaf node (actual symbol)
                codeLengths[(Word)node.Symbol] = depth;
            else
            {
                Traverse(node.Left, depth + 1);
                Traverse(node.Right, depth + 1);
            }
        }

        Traverse(root, 0);

        return codeLengths;
    }

    public static Dictionary<Word, HuffmanCode> GenerateCanonicalCodes(Dictionary<Word, Uint> codeLengths)
    {
        var sortedSymbols = codeLengths
            .OrderBy(kv => kv.Value) // Sort by code length
            .ThenBy(kv => kv.Key) // Then by symbol value
            .ToList();

        Dictionary<Word, HuffmanCode> huffmanCodes = [];
        Uint code = 0;
        Uint prevLength = 0;

        foreach (var (symbol, length) in sortedSymbols)
        {
            if (length > prevLength)
                code <<= (int)(length - prevLength); // Left shift to match new bit length

            huffmanCodes[symbol] = new(code, (int)length);
            code++;
            prevLength = length;
        }

        return huffmanCodes;
    }

    public Dictionary<Word, HuffmanCode> GenerateCanonicalCodes() => GenerateCanonicalCodes(GetHuffmanCodeLengths());

    public HuffmanWriter GetWriter(BitWriter writer) => new(GenerateCanonicalCodes(), writer);
}
