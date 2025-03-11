using System.Linq;

namespace wsc.Core;

public class HuffmanWriter(Dictionary<Word, HuffmanCode> huffmanCodes, BitWriter writer)
{
    // Usual code lengths are: 2, 5, 6, 7, 8, 9, 10, 11 and 12.
    // 1 and 17 are rare, 12 is most common. 0 is used if the index is not used.
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
    // 11: 3 bits
    // 12: 3 bits
    // 13: 3 bits
    // 14: 6 bits
    // 15: 6 bits
    // 16: 6 bits
    // 17: 7 bits
    internal static readonly Uint[] LengthCodeLengths = [3, 7, 5, 5, 5, 5, 4, 4, 4, 4, 4, 3, 3, 3, 6, 6, 6, 7];

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

        for (Uint code = 0; code < codeCount; code++)
        {
            int length = huffmanCodes.TryGetValue((Word)code, out var huffmanCode) ? huffmanCode.Length : 0;

            lengthWriter.WriteIndex((ushort)length);
        }

        writer.ToNextByteBoundary();

        // Sometimes it makes sense to use 0-RLE for the tree. Especially if many indexes are not used.
        // We can add a header byte which starts with the bit sequence 01..., as no length code can start with a single 0.
        // So we choose 0x40 but can insert parts of the total header size in there as well.
        // Thus the header is a word of the form 0x4000 + <compressed tree data size>. The size must not exceed 2^14-1 (16383),
        // but we can add 1 and allow up to 2^14 (16384). If the size would exceed this, 0-RLE is not allowed.

        byte[] treeData = [.. buffer.Take(treeWriter.Size)];
        List<byte> rleData = new(treeData.Length - 2);
        int zeroCount = 0;

        void PutZeros()
        {
            while (zeroCount != 0)
            {
                int count = Math.Min(256, zeroCount);
                rleData.Add(0);
                rleData.Add((byte)(count - 1));

                zeroCount -= count;
            }
        }

        for (int i = 2; i < treeData.Length; i++)
        {
            byte b = treeData[i];

            if (b == 0)
            {
                ++zeroCount;
            }
            else
            {
                PutZeros();
                rleData.Add(b);
            }
        }

        PutZeros();

        if (rleData.Count < treeData.Length - 2 - 2 && rleData.Count <= 16384) // 2 for the size header (not part of data) and 2 for the needed rle header
        {
            var header = (Word)(0x4000 + rleData.Count - 1);
            byte[] headerData = [(byte)(header >> 8), (byte)(header & 0xff)];

            writer.WriteBytes([.. treeData.Take(2), .. headerData, .. rleData]);
        }
        else
        {
            writer.WriteBytes(treeData);
        }
    }
}