## Byte header

Maybe use only 6 bits for the literal count header and 7 bits for the alternating header. I guess it is less common to have more than 64 literals in a row than 7 alternating literals and indexes.

## Tree encoding

Instead of storing the length of all indexes, maybe it is better to use a Delta Encoding first. So store the distances from the last inded to the next.

This will reduce the amount of symbol lengths to store and will lower the possibility to encounter big length values.

### Example

Used indexes:

0001 0003 0004 0009 01ff 0200 0203

This needs 516 entries in the length table.

It can be first encoded as:

0001 0002 0001 0005 01f6 0003

This only needs 6 entries and has only 1 bigger value.
