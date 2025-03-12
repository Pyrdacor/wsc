# WSC

WSC stands for Word Sequence Compression. It is a simple compression method that interprets the input as a
sequence of words and compresses it by replacing repeated words by its index of first occurence.

Those indexes are then compressed by a dynamic canonical Huffman tree which allows code length up to 22 bits.

The tree is stored with the compressed data and is itself encoded in a special way.

The compression is quite fast in contrast to algorithms like LZ77 as it has not to search for reoccuring patterns.
You could say that the word indexes encode a byte sequence match of length 2. But it does not need any offset or
length information. The index itself is enough and has always the same size.

As indexes (repeated symboles) are less common as all possible 2^16 symbols, they are ideal to encode them with
Huffman coding in most cases.

The tricky part is to store the tree, as it might encode way more than 256 symbols and can consume a lot of space.


## Tree encoding

The Huffman tree is first based on a simple frequency table of all word indexes. Canonical huffman codes are then
generated from this table.

Only the lengths of the indexex are stored. First the total amount of indexes is stored as 16 bit value. This includes
all indexes up to the highest which is actually used (= repeated word).

Then the lengths of each index is stored in a bitstream. The lengths are stored in a special way to save space.

There is a static Huffman table for the lengths of the index lengths.

| Length | Bit representation |
|--------|--------------------|
|     0 | 000                 |
|    12 | 001                 |
|    13 | 010                 |
|     6 | 0110                |
|     7 | 0111                |
|     8 | 1000                |
|     9 | 1001                |
|    10 | 1010                |
|    11 | 1011                |
|     2 | 11000               |
|     3 | 11001               |
|     4 | 11010               |
|     5 | 11011               |
|    14 | 11100               |
|    15 | 111010              |
|    16 | 111011              |
|    17 | 111100              |
|     1 | 1111010             |
|    18 | 1111011             |
|    19 | 1111100             |
|    20 | 1111101             |
|    21 | 1111110             |
|    22 | 1111111             |

From those information a canonical huffman tree is generated in both the encoder and decoder.

The encoder writes the lengths with this Huffman tree. Non-used indexes have length 0.

As there are often sequences of zeros in the resulting data, as some words are tracked but not repeated,
the data can be 0-RLE encoded to save space.

In this case if a zero is encountered in the data, a zero is output and the next byte is interpreted as
a count of zeros to write. All other bytes are written as is. Typically an encoder will try to RLE encode
and only if the result is smaller than the original data plus needed header, it will write data as encoded.

To specify if the data is RLE encoded there is a header byte which is 0x00 if not encoded and 0x80+ if encoded.
If encoded the header byte and the following byte together form a 16 bit big endian value where the lower 15 bits
specify the size of the compressed data minus 1. So it is possible to specify lengths from 1 to 32768 bytes.

### Data format

Note that the tree data always starts on a full byte boundary!

#### RLE encoded data

| Offset | Length | Description                          |
|--------|--------|--------------------------------------|
| 0x00   | 2      | Number of indexes                    |
| 0x02   | 2      | 0x8000 + size of compressed data - 1 |
| 0x04   | n      | RLE-encoded tree data                |

n is the size of the compressed data. For example of the header is 0x8007, the compressed data size is 0x8007 - 0x8000 + 1 = 8 bytes.

To decompress the tree data just read byte by byte. If the byte is non-zero just write it to the output.
If it is zero read the next byte, add 1 and write that many zeros to the output.

For example the uncompressed data `0F 00 0A 0B 00 00 00 0C` would be encoded as `0F 00 00 0A 0B 00 02 0C`.

After decompression it has the same format as non-RLE uncompressed tree data (see below).

#### Non-RLE data

| Offset | Length | Description            |
|--------|--------|------------------------|
| 0x00   | 2      | Number of indexes      |
| 0x02   | 1      | 0x00                   |
| 0x03   | n      | Uncompressed tree data |

n is an arbitrary size but in total it should match the amount of bits needed to encode all the given indexes.

For every given index (0 up to the umber of indexes minus 1) a length is stored with the mentioned bit encoding.
From those length the main Huffman tree for the indexes can be reconstructed.

Note that after the tree data, it is ensured that following data will start at a full byte boundary!


## Decompression

After the tree, the actual compressed data follows. It is a sequence of 8-bit headers, 16-bit symbols and encoded indexes.

The data starts with an 8-bit header. This header directly specifies how many symbols follow. The value must be increased by
1, as 0 symbols at the beginning would not make sense.

Then this amount of symbols is read as 16-bit values. Note that the values are big endian as we effectively read from a bitstream.

For each symbol, its index of occurence is tracked. So for example the first symbol will be associated to index 0, the second symbol
to index 1, and so on.

After the symbols the next header is read as 8-bit. Now this and all following headers can have one of three encodings:

- If the value is less than 0x80 (128) it again gives a number of symbols minus 1, so it can encode 1 to 128 symbols.
- If the value is less than 0xC0 (192) the lowest 6 bits give a number of indexes minus 1, so it can encode 1 to 64 indexes.
- Otherwise the lowest 6 bits specify a bit mask where starting from lowest bit, each bit states if to read a symbol (0) or index (1).

The last two cases can also be interpreted as:

- If the upper two bits are 10, the lowest 6 bits give a number of indexes minus 1.
- If the upper two bits are 11, the lowest 6 bits specify the bit mask.

The bit mask is useful if small amounts of symbols and indexes are mixed. It is more efficient to store them in one byte than to specify
multiple headers with small amounts.

For example if the header is 0xD8 which in binary is 1101 1000, it means that there will be 3 symbols, then 2 indexes and then 1 symbol again.
The header bits are `11` and the meaningful bits in order are `0 0 0 1 1 0`.

Based on each header the data is read as symbols or indexes. Symbols are read as 16-bit BE values as before. Indexes are read with the
main Huffman tree. The read index can be transformed to the associated symbol via a mapping table.

For example if the first symbol was 0xABCD, it is associated to index 0. If later the index 0 is read, it means that the symbol 0xABCD is meant.

Going forward more and more indexes are tracked and can be used. For text data and uncompressed binary data with many similar word values, this
can lead to a good compression ratio. The fewer different indexes are used, the better the compression will be.