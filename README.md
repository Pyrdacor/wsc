# WSC

WSC stands for Word Sequence Compression. It is a simple compression method that interprets the input as a
sequence of words and compresses it by replacing repeated words by its index of first occurence.

Those indexes are then compressed by a dynamic canonical Huffman tree which allows code length up to 22 bits.

The tree is stored with the compressed data and is itself encoded in a special way.

The compression is quite fast in contrast to algorithms like LZ77 as it has not to search for reoccuring patterns.
You could say that the word indexes encode a byte sequence match of length 2. But it does not need any offset or
length information. The index itself is enough and has always the same size.

As indexes (repeated symbols) are less common as all possible 2^16 symbols, they are ideal to encode them with
Huffman coding in most cases.

The tricky part is to store the tree, as it might encode way more than 256 symbols and can consume a lot of space.

The compression works pretty good on binary data with repeated word values, uncompressed assemblies, uncompressed bitmaps with low color count and even text data. 

## Compression

The raw data is interpreted as a sequence of little endian 16-bit values (words). If the raw data size is odd a padding byte with value zero is appended to the data.

If a word value is found for the first time, an auto increased index is assigned which starts at zero. So the first found word gets index 0, the second found word index 1 and so on.

If the same word is encountered again, it is replaced by its index.

In addition a counter is used to track the count of sequential words and sequential indexes.

The compressed output is then generated as a bit stream. To identify words and indexes in the output, header bytes are used. As every compressed data has to start with at least one normal word value, the first header just gives the number of following words. As a byte can express values from 0 to 255 but a count of 0 is impossible, the first header stores the number of words minus 1. So a header value of 5 means that 6 words will follow.

Words are written directly as 16 bits to the output stream. Note that this is basically big endian encoding as the bits are written to the stream from highest to lowest bit. This is true in general for writing data to the bit stream.

After the first word sequence, the next header must be inserted. This and all following headers now have a special format but they are still always 8 bits in size. Dependent on the data a header can take one of three formats:

- If the value is less than 0x80 (128) it again gives a number of words minus 1, so it can encode 1 to 128 words.
- If the value is less than 0xC0 (192) the lowest 6 bits give a number of indexes minus 1, so it can encode 1 to 64 indexes.
- Otherwise the lowest 6 bits specify a bit mask where starting from lowest bit, each bit states if a symbol (0) or index (1) follows.

The last two cases can also be interpreted as:

- If the upper two bits are 10, the lowest 6 bits give a number of indexes minus 1.
- If the upper two bits are 11, the lowest 6 bits specify the bit mask.

The bit mask can be used if words and indexes change frequently. In general it is beneficial if the amount of words and the amount of indexes together is less or equal than 6. For example if you have 5 words, then 1 index and then words again, you can encode the 5 words and the index in one bit mask header. Otherwise it would need two headers.

If a sequence of words exceeds 128, the same header can be used again of course. Same with sequences of indexes which exceed an amount of 64.

The header 0xc0 is the end marker. It marks the end of the compressed data. In general it would mean that it is a bit mask header but as all bits are 0, this would mean that just 6 words follow. But this can be expressed with the first header format so this header is not needed. So never use header 0xc0 to express a word sequence. Always end the compressed data with that header.

Whenever writing an index, it is Huffman encoded. A canonical Huffman tree is used which depends on the frequency an index occurs. For example if the input data contains word 0x1234 only once, the index frequency is zero as it is never used. If another word appears 5 times, the frequency of its index would be 4, as the index only appears 4 times while the word itself appears once.

As the index is associated even if it is not used again, the frequency of 0 might be common. Those indexes are encoded in the Huffman tree as having a length of 0, as they are never used. This is why the symbol length 0 is encoded small when storing the pretree for the main Huffman tree (see Tree encoding below).


## Tree encoding

The Huffman tree is first based on a simple frequency table of all word indexes. Canonical huffman codes are then
generated from this table. If an index is not used at all (frequency = 0), then it is stored with a length of zero in the tree.

Only the length of the indexes are stored. First the total amount of indexes is stored as a 16 bit value. This includes
all indexes up to the highest which is actually used (= repeated word).

Then the lengths of each index is stored in a bitstream. The lengths are stored in a special way to save space.

There is a static Huffman table for the lengths of the index lengths (aka the pretree).

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

Note that the whole tree data section always starts at a full byte boundary!

#### RLE encoded data

| Offset | Length | Description                          |
|--------|--------|--------------------------------------|
| 0x00   | 2      | Number of indexes                    |
| 0x02   | 2      | 0x8000 + size of compressed data - 1 |
| 0x04   | n      | RLE-encoded tree data                |

n is the size of the compressed data. For example if the header is 0x8007, the compressed data size is 0x8007 - 0x8000 + 1 = 8 bytes.

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

For every given index (0 up to the number of indexes minus 1) a length is stored with the mentioned bit encoding.
From those lengths the main Huffman tree for the indexes can be reconstructed.

Note that after the tree data, it is ensured that following data will start at a full byte boundary!


## Decompression

After the tree, the actual compressed data follows. It is a sequence of 8-bit headers, 16-bit symbols and encoded indexes.

The data starts with an 8-bit header. This header directly specifies how many symbols follow. The value must be increased by
1, as 0 symbols at the beginning would not make sense. So an amount of 1 to 256 symbols can be given here.

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
