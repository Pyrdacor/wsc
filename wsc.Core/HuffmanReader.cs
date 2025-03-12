namespace wsc.Core;

public class HuffmanReader(BitReader reader)
{
    private static readonly Uint[] LengthCodeLengths = HuffmanWriter.LengthCodeLengths;
    private readonly BitReader reader = reader;
    private readonly Dictionary<int, Dictionary<Uint, Word>> lookupTable = ReadTree(reader);

    public Word ReadIndex()
    {
        return ReadWithTree(lookupTable, reader);
    }

    private static Word ReadWithTree(Dictionary<int, Dictionary<Uint, Word>> lookupTable, BitReader reader)
    {
        Uint code = 0;
        int maxLength = lookupTable.Keys.Max();

        for (int bitLength = 1; bitLength <= maxLength; bitLength++)
        {
            code = (code << 1) | reader.ReadBits(1); // Read one bit at a time

            if (lookupTable.TryGetValue(bitLength, out var subTable) && subTable.TryGetValue(code, out var symbol))
            {
                return symbol; // Found valid symbol
            }
        }

        throw new Exception("Invalid Huffman code encountered!");
    }

    private static Dictionary<int, Dictionary<Uint, Word>> CreateLookupTable(Dictionary<Word, Uint> codeLengths)
    {
        var huffmanCodes = HuffmanTree.GenerateCanonicalCodes(codeLengths);

        Dictionary<int, Dictionary<Uint, Word>> lookupTable = [];

        foreach (var (symbol, code) in huffmanCodes)
        {
            if (!lookupTable.ContainsKey(code.Length))
                lookupTable[code.Length] = [];

            lookupTable[code.Length][code.Code] = symbol;
        }

        return lookupTable;
    }

    private static Dictionary<int, Dictionary<Uint, Word>> ReadTree(BitReader reader)
    {
        reader.ToNextByteBoundary();

        var lengthCount = (int)reader.ReadBits(16) + 1;
        var treeLookup = CreateLookupTable(LengthCodeLengths.Select((length, i) => new { Symbol = (ushort)i, Length = length }).ToDictionary(x => x.Symbol, x => x.Length));
        var huffmanCodeLengths = new Dictionary<Word, Uint>(lengthCount);
        var treeReader = reader;

        // 0-RLE used?
        if ((reader.PeekBits(8) & 0x80) == 0x80)
        {
            int rleDataSize = 1 + (int)(reader.ReadBits(16) & 0x7fff);
            var rleData = reader.ReadBytes(rleDataSize);
            var treeData = new List<byte>();

            for (int i = 0; i < rleData.Length; i++)
            {
                byte b = rleData[i];

                if (b == 0)
                {
                    int count = rleData[++i] + 1;

                    for (int j = 0; j < count; j++)
                        treeData.Add(0);
                }
                else
                {
                    treeData.Add(b);
                }
            }

            treeReader = new BitReader([.. treeData]);
        }
        else
        {
            if (reader.ReadBits(8) != 0) // skip and check header
                throw new Exception("Invalid Huffman tree header encountered!");
        }

        Word index = 0;

        for (int i = 0; i < lengthCount; i++)
        {
            var length = ReadWithTree(treeLookup, treeReader);

            if (length != 0)
                huffmanCodeLengths[index++] = length;
            else
                index++;
        }

        reader.ToNextByteBoundary();

        return CreateLookupTable(huffmanCodeLengths);
    }
}