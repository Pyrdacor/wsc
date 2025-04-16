## Byte header

Maybe use only 6 bits for the literal count header and 7 bits for the alternating header. I guess it is less common to have more than 64 literals in a row than 7 alternating literals and indexes.

## Tree encoding

There might be gaps of zero-length symbols as indexes might not be used (16 bit symbols occur only once inside the data).

To address this the length tree has a special encoding.

If a length of 0 is read, the next bit is checked. If it is 0, there is no further length of zero. But if the bit is 1, the next 2 bits give the amount of following zeros minus 1.

For a single zero length this wastes 1 bit, for 2 zero lengths in a row it is not changing the bit count (as the length zero needs 3 bits to encode).
For 3, 4 or 5 zero lengths in a row, it saves 3, 6 or 9 bits respectively.

So for larger gaps this can save quite some bits.
