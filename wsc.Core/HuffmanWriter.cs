namespace wsc.Core;

public class HuffmanWriter(Dictionary<Word, HuffmanCode> huffmanCodes, BitWriter writer)
{
    // Usual code lengths are: 2, 5, 6, 7, 8, 9, 10, 11 and 12.
    // 1 and 17+ are rare, 12 is most common. 0 is used if the index is not used.
    // The codes are static like this:
    //
    // 0: 3 bits
    // 1: 7 bits
    // 2: 5 bits
    // 3: 5 bits
    // 4: 5 bits
    // 5: 5 bits
    // 6: 4 bits
    // 7: 4 bits
    // 8: 4 bits
    // 9: 4 bits
    // 10: 4 bits
    // 11: 4 bits
    // 12: 3 bits
    // 13: 3 bits
    // 14: 5 bits
    // 15: 6 bits
    // 16: 6 bits
    // 17: 6 bits
    // 18: 7 bits
    // 19: 7 bits
    // 20: 7 bits
    // 21: 7 bits
    // 22: 7 bits
    internal static readonly Uint[] LengthCodeLengths = [3, 7, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 3, 3, 5, 6, 6, 6, 7, 7, 7, 7, 7];

    public void WriteIndex(Word index)
    {
        if (!huffmanCodes.TryGetValue(index, out var code))
            throw new ArgumentOutOfRangeException(nameof(index));

        writer.WriteBits(code.Code, code.Length);
    }

    public void WriteTree()
    {
        Uint codeCount = huffmanCodes.Keys.Max() + 1u;
        var buffer = new byte[2 + codeCount];
        var treeWriter = new BitWriter(buffer);

        // First write the count of lengths
        treeWriter.WriteBits(codeCount - 1, 16);

        var lengthCodes = HuffmanTree.GenerateCanonicalCodes(LengthCodeLengths.Select((length, i) => new { Symbol = (ushort)i, Length = length }).ToDictionary(x => x.Symbol, x => x.Length));
        var lengthWriter = new HuffmanWriter(lengthCodes, treeWriter);

        // Optimization: If an index is not used it is common that there are more unused indexes following.
        // Therefore for the length 0 we append a 4 bit value which gives the amount of following zero-length indexes.
        int followZeroLengthCount = 0;
        int lastLength = -1;

        for (Uint code = 0; code < codeCount; code++)
        {
            int length = huffmanCodes.TryGetValue((Word)code, out var huffmanCode) ? huffmanCode.Length : 0;

            if (length == 0)
            {
                if (lastLength == 0)
                {
                    if (++followZeroLengthCount < 20)
                        continue;

                    // We reach 20 zeros and have to break the sequence into two.
                    treeWriter.WriteBits(0x7f, 7); // 1111111 -> 19 zeros
                    followZeroLengthCount = 0;
                }
            }
            else if (lastLength == 0)
            {
                if (followZeroLengthCount == 0)
                {
                    // 0 zeros encoded as 0b (1 bit)
                    treeWriter.WriteBits(0, 1);
                }
                else if (followZeroLengthCount < 4)
                {
                    // 1..3 zeros encoded as 100b..110b (3 bits)
                    treeWriter.WriteBits((Uint)(0x04 + followZeroLengthCount - 1), 3);
                    followZeroLengthCount = 0;
                }
                else
                {
                    // 4..19 zeros encoded as 1110000b..1111111b (7 bits)
                    treeWriter.WriteBits((Uint)(0x70 + followZeroLengthCount - 4), 7);
                    followZeroLengthCount = 0;
                }
            }

            lengthWriter.WriteIndex((ushort)length);
            lastLength = length;
        }

        writer.ToNextByteBoundary();
        writer.WriteBytes([.. buffer.Take(treeWriter.Size)]);
    }
}