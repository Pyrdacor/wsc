namespace WSC.Compression;

using Core;
using Word = System.UInt16;

public static class Decompressor
{
    public static byte[] Decompress(byte[] data)
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
}
