# World object data

The client reads both the plain `Terrain.obj` format and encrypted
`EncTerrain*.obj` files. Files with an incomplete header, invalid record count,
or truncated records are rejected before creating world objects.

Some existing maps contain isolated decorative objects with an invalid model
index or nonfinite coordinates. The client logs and skips those records while
loading the rest of the map. This avoids crashes without making the whole map
unavailable. The resource files on disk are not changed.

Saving world objects retains the existing encrypted format and counts only live
objects that are written. A map containing deleted objects can therefore be
loaded again without its recorded object count exceeding the data in the file.
