namespace PYC.Core;

public class BitReader(byte[] data)
{
    private int bitIndex = 0;

    public Uint PeekBits(int bitCount)
    {
        var oldPosition = Position;
        var oldBitIndex = bitIndex;

        int maxBits = bitIndex == 0 ? (Size - Position) * 8 : (Size - Position) * 8 + 8 - bitIndex;

        if (maxBits < bitCount)
            bitCount = maxBits;

        var result = ReadBits(bitCount);

        Position = oldPosition;
        bitIndex = oldBitIndex;

        return result;
    }

    public Uint ReadBits(int bitCount)
    {
        if (bitCount == 0)
            return 0;

        Uint result = 0;

        if (bitIndex == 0)
        {
            if (bitCount % 8 == 0)
            {
                while (bitCount != 0)
                {
                    int shift = bitCount - 8;
                    result |= data[Position++];
                    result <<= shift;
                    bitCount -= 8;
                }
            }
            else if (bitCount > 8)
            {
                int remainingBits = bitCount % 8;
                result = ReadBits(bitCount - remainingBits);
                result <<= remainingBits;
                result |= ReadBits(remainingBits);
            }
            else
            {
                result = data[Position];
                result >>= 8 - bitCount;
                bitIndex = bitCount;
            }
        }
        else
        {
            int remainingBits = 8 - bitIndex;

            if (bitCount >= remainingBits)
            {
                result = data[Position++];
                result &= (1u << remainingBits) - 1;
                result <<= bitCount - remainingBits;                
                bitCount -= remainingBits;
                bitIndex = 0;

                if (bitCount != 0)
                {
                    result |= ReadBits(bitCount);
                }
            }
            else
            {
                result = data[Position];
                result &= (1u << remainingBits) - 1;
                result >>= remainingBits - bitCount;

                bitIndex += bitCount;
            }
        }

        return result;
    }

    public void ToNextByteBoundary()
    {
        if (bitIndex != 0)
        {
            Position++;
            bitIndex = 0;
        }
    }

    public byte[] ReadBytes(int count)
    {
        ToNextByteBoundary();

        var bytes = new byte[count];
        Array.Copy(data, Position, bytes, 0, bytes.Length);
        Position += count;

        return bytes;
    }

    public int Position { get; private set; } = 0; // full bytes
    public int Size { get; } = data.Length; // in full bytes
}