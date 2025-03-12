using Word = System.UInt16;
using Uint = System.UInt32;
using wsc.Core;

namespace wsc;

public static class Program
{
    public static void Main()
    {
#if true
        var data = File.ReadAllBytes(@"D:\Projects\Ambermoon Advanced\releases\english\ambermoon_advanced_english_1.31_extracted\Game.cs");

        var compressedData = Compress(data.Length % 2 == 0 ? data : [ ..data, 0 ]);

        if (compressedData.Length < data.Length)
        {
            Console.WriteLine($"Compressed from {data.Length} to {compressedData.Length}");

            File.WriteAllBytes(@"D:\Projects\Ambermoon Advanced\releases\english\ambermoon_advanced_english_1.31_extracted\Game.cmp", compressedData);
        }
#else
        var data = File.ReadAllBytes(@"D:\Projects\Ambermoon Advanced\releases\english\ambermoon_advanced_english_1.31_extracted\Game.cmp");

        var unc = Decompress(data);

        File.WriteAllBytes(@"D:\Projects\Ambermoon Advanced\releases\english\ambermoon_advanced_english_1.31_extracted\Game.raw", unc);
#endif
    }

    private static byte[] Decompress(byte[] data)
    {
        var reader = new BitReader(data);
        var huffmanReader = new HuffmanReader(reader);
        var symbolsByIndexes = new Dictionary<Word, Word>();
        Word nextIndex = 0;
        bool readSymbols = true;
        int useBits = 0;

        reader.ToNextByteBoundary();

        var length = reader.ReadBits(8) + 1;
        var output = new List<byte>();
        uint header = 0;

        void PutWord(Word value)
        {
            output.Add((byte)(value & 0xff));
            output.Add((byte)(value >> 8));
        }

        while (reader.Position < reader.Size)
        {
            while (length-- != 0)
            {
                if (useBits != 0)
                {
                    var mask = 1u << (6 - useBits);
                    mask &= header;
                    readSymbols = mask == 0;
                    useBits--;
                }

                if (readSymbols)
                {
                    if (reader.Position >= reader.Size - 1)
                        goto End;

                    var symbol = (Word)reader.ReadBits(16);
                    PutWord(symbol);

                    symbolsByIndexes[nextIndex++] = symbol;
                }
                else // index
                {
                    try
                    {
                        var index = huffmanReader.ReadIndex();
                        PutWord(symbolsByIndexes[index]);
                    }
                    catch (Exception)
                    {
                        if (reader.Position >= reader.Size - 1)
                            goto End;

                        throw;
                    }
                }
            }

            header = reader.ReadBits(8);

            if (header < 128) // symbols
            {
                readSymbols = true;
                length = header + 1;
            }
            else if ((header & 0xc0) == 0x80) // indexes
            {
                readSymbols = false;
                length = (header & 0x3f) + 1;
            }
            else // bit header
            {
                if (header == 0xc0) // end marker
                    break;

                useBits = 6;
                length = 6;
            }
        }

    End:
        return [.. output];
    }

    private static byte[] Compress(byte[] data)
    {
        if (data.Length % 2 == 1)
            return data;

        if (data.Length < 4)
            return data;

        var originalData = data;

        data = [.. data];

        Span<Uint> symbolFrequencies = stackalloc Uint[Word.MaxValue + 1];
        Span<Word> indexes = stackalloc Word[Word.MaxValue + 1];
        Span<Word> symbols = stackalloc Word[Word.MaxValue + 1];
        Uint nextIndex = 0;
        Queue<Uint> counts = []; // first symbols, then indexes, then symbols, and so on
        uint currentCount = 0;

        // TODO: merge TrackSymbol and TrackIndex
        void TrackSymbol()
        {
            if (counts.Count % 2 == 0)
                ++currentCount;
            else
            {
                counts.Enqueue(currentCount);
                currentCount = 1;
            }
        }

        void TrackIndex()
        {
            if (counts.Count % 2 == 1)
                ++currentCount;
            else
            {
                counts.Enqueue(currentCount);
                currentCount = 1;
            }
        }

        unsafe
        {
            fixed (byte* ptr = data)
            {
                Word* current = (Word*)ptr;
                Word* end = current + data.Length / 2;

                while (current < end)
                {
                    if (symbolFrequencies[*current]++ == 0)
                    {
                        symbols[(Word)nextIndex] = *current;
                        indexes[*current++] = (Word)nextIndex++;
                        TrackSymbol();
                    }
                    else
                    {
                        *current = indexes[*current];
                        current++;
                        TrackIndex();
                    }
                }
            }
        }

        counts.Enqueue(currentCount);

        var indexFrequencies = new Uint[nextIndex];

        for (int i = 0; i < indexFrequencies.Length; i++)
        {
            indexFrequencies[i] = symbolFrequencies[symbols[i]];

            if (indexFrequencies[i] != 0)
                --indexFrequencies[i];
        }

        var output = new byte[data.Length];
        var writer = new BitWriter(output);
        var huffmanTree = new HuffmanTree(indexFrequencies);
        var canonicalCodes = huffmanTree.GenerateCanonicalCodes();

        if (canonicalCodes.Min(code => code.Value.Length) >= 16) // not worth compressing
            return originalData;

        if (canonicalCodes.Max(code => code.Value.Length) > 22) // can't encode the tree
            return originalData;

        var huffmanWriter = new HuffmanWriter(canonicalCodes, writer);

        Uint remainingCount = counts.Dequeue();
        bool writeSymbols = true;
        Uint nextHeaderCount = Math.Min(256, remainingCount);
        var nextCounts = new Uint[5];

        huffmanWriter.WriteTree();

        Uint PopNextCount()
        {
            var nextCount = nextCounts[0];

            for (int i = 0; i < 4; i++)
                nextCounts[i] = nextCounts[i + 1];

            nextCounts[4] = 0;

            return nextCount;
        }

        void WriteNextCounts(bool initial = false)
        {
            if (initial)
            {
                writer.WriteBits(Math.Min(256, remainingCount) - 1, 8);
            }
            else
            {
                if (remainingCount == 0)
                {
                    writeSymbols = !writeSymbols;

                    if (nextCounts[0] != 0)
                    {
                        remainingCount = PopNextCount();

                        if (nextHeaderCount != 0)
                            return;
                    }
                    else if (counts.Count == 0)
                    {
                        return;
                    }
                    else
                    {
                        remainingCount = counts.Dequeue();
                    }
                }

                if (remainingCount < 6 && nextCounts[4] == 0)
                {
                    // Counts 6 1 6 always need 3 headers.
                    // Starting at 5 1 6+, 6+ 1 5 and 5 1 5 we can shrink it to 2 headers.
                    // Most compression is counts 1 1 1 1 1 1.
                    // We store up to 5 next counts.
                    int first = 5;

                    for (int i = 0; i < 5; i++)
                    {
                        if (nextCounts[i] == 0)
                        {
                            first = i;
                            break;
                        }
                    }

                    for (int i = first; i < 5 && counts.Count != 0 && counts.Peek() < 6; i++)
                        nextCounts[i] = counts.Dequeue();
                }

                if (nextCounts[0] != 0 && remainingCount + nextCounts[0] <= 6) // use bit header in this case
                {
                    Uint bitCount = remainingCount;
                    int i = 0;

                    for (; i < 5; i++)
                    {
                        if (nextCounts[i] == 0 || bitCount + nextCounts[i] > 6)
                            break;

                        bitCount += nextCounts[i];
                    }

                    if (bitCount > remainingCount)
                    {
                        if (bitCount < 6)
                            bitCount = 6;

                        bool set = !writeSymbols;
                        Uint currentCount = remainingCount;
                        int nextCountIndex = 0;
                        byte header = 0xc0;

                        for (i = 0; i < bitCount; i++)
                        {
                            if (set)
                                header |= (byte)(1 << i);
                            else
                                header &= (byte)(~(1 << i) & 0xff);

                            if (--currentCount == 0 && i < 5)
                            {
                                currentCount = nextCounts[nextCountIndex++];

                                if (currentCount == 0)
                                    currentCount = (Uint)(5 - i);

                                set = !set;
                            }
                        }

                        nextHeaderCount = 6;
                        writer.WriteBits(header, 8);
                    }
                }
                else if (writeSymbols)
                {
                    if (nextHeaderCount == 0)
                    {
                        nextHeaderCount = Math.Min(128, remainingCount);
                        writer.WriteBits(Math.Min(128, remainingCount) - 1, 8);
                    }
                }
                else // indexes
                {
                    if (nextHeaderCount == 0)
                    {
                        nextHeaderCount = Math.Min(64, remainingCount);
                        writer.WriteBits(Math.Min(64, remainingCount) - 1 + 0x80, 8);
                    }
                }
            }
        }

        WriteNextCounts(true);

        unsafe
        {
            fixed (byte* ptr = data)
            {
                Word* current = (Word*)ptr;
                Word* end = current + data.Length / 2;

                while (current < end)
                {
                    if (writeSymbols)
                        writer.WriteBits(*current++, 16);
                    else
                        huffmanWriter.WriteIndex(*current++);

                    if (writer.Position >= data.Length - 1)
                        return originalData;

                    --remainingCount;

                    if (--nextHeaderCount == 0)
                    {
                        WriteNextCounts();

                        if (remainingCount != 0 && writer.Position >= data.Length - 1)
                            return originalData;
                    }
                    else if (remainingCount == 0)
                    {
                        WriteNextCounts();
                    }
                }
            }
        }

        writer.WriteBits(0xc0, 8); // end marker

        return [.. output.Take(writer.Size)];
    }
}