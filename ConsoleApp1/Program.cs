uint foo = 0x80000000;

int shift = -5;

foo >>= shift;

Console.WriteLine(foo);


var data = new byte[20 * 65535 + 2];

unsafe
{
    fixed (byte* ptr = data)
    {
        ushort* p = (ushort*)ptr;
        ushort* end = p + data.Length / 2;
        int count = 0;
        ushort symbol = 0;

        while (p < end)
        {
            if (symbol != 0xffff)
            {
                for (int i = 0; i < 10; i++)
                {
                    *p++ = symbol;
                    count++;
                }

                symbol++;
            }
            else
            {
                *p++ = symbol;
                count++;
            }
        }

        File.WriteAllBytes(@"D:\Projects\Ambermoon Advanced\releases\english\ambermoon_advanced_english_1.31_extracted\test.raw", data);
    }
}