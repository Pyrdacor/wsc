namespace wsc.Core;

public class BitWriter(byte[] data)
{
    private int bitIndex = 0;

    public void WriteBits(Uint value, int bitCount)
    {
        if (bitCount == 0)
            return;

        if (bitIndex == 0)
        {
            if (bitCount % 8 == 0)
            {
                while (bitCount != 0)
                {
                    int shift = bitCount - 8;
                    data[Position++] = (byte)((value >> shift) & 0xff);
                    bitCount -= 8;
                }
            }
            else if (bitCount > 8)
            {
                int remainingBits = bitCount % 8;
                WriteBits(value >> remainingBits, bitCount - remainingBits);
                WriteBits(value & ((1u << remainingBits) - 1), remainingBits);
            }
            else
            {
                value <<= (8 - bitCount);
                data[Position] = (byte)(value & 0xff);
                bitIndex = bitCount;
            }
        }
        else
        {
            int remainingBits = 8 - bitIndex;

            if (bitCount >= remainingBits)
            {
                Uint part = value >> (bitCount - remainingBits);
                part &= (1u << remainingBits) - 1;
                data[Position++] |= (byte)part;

                bitCount -= remainingBits;
                bitIndex = 0;

                if (bitCount != 0)
                {
                    value &= (1u << bitCount) - 1;
                    WriteBits(value, bitCount);
                }
            }
            else
            {
                value &= (1u << bitCount) - 1;
                value <<= 8 - bitIndex - bitCount;
                data[Position] |= (byte)value;
                bitIndex += bitCount;
            }
        }
    }

    public void ToNextByteBoundary()
    {
        if (bitIndex != 0)
        {
            Position++;
            bitIndex = 0;
        }
    }

    public void WriteBytes(params byte[] bytes)
    {
        ToNextByteBoundary();

        Array.Copy(bytes, 0, data, Position, bytes.Length);
        Position += bytes.Length;
    }

    public int Position { get; private set; } = 0; // full bytes
    public int Size => Position + (bitIndex == 0 ? 0 : 1); // in full bytes needed
}