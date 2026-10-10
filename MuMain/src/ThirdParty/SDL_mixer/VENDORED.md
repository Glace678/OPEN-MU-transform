# Vendored upstream code

This directory is a vendored snapshot of a third-party upstream project. It is not
original project code.

- Upstream: https://github.com/libsdl-org/SDL_mixer
- License: Zlib (see LICENSE.txt in this directory)
- Usage in this project: 3.x development branch, built via add_subdirectory. The
  external/ directory contains bundled codec libraries.

Do not edit these files directly to add features. If a local patch is unavoidable,
keep it minimal, record it against a specific upstream version/SHA, and prefer
upstreaming the fix or moving the abstraction into project code.

## Local patches

Snapshot baseline: SDL_mixer 3.x development branch (vendored into MuMain). These are
explicitly recorded local patches; they are NOT presented as pristine upstream code.

1. `src/decoder_wav.c` — WAV smpl/LIST metadata heap bounds (L5 r3-71 findings 97-01, 97-02).
   - 97-01 `ParseSMPL`: require the chunk to hold the fixed `SamplerChunk` header
     (`offsetof(SamplerChunk, loops)` = 36 bytes) before reading `sample_loops`, and clamp the
     loop count to `(chunk_length - fixed_header_size) / sizeof(SampleLoop)`. A hostile smpl
     chunk declaring a huge loop count for a tiny buffer no longer reads past the allocation.
   - 97-02 `CheckWAVMetadataField`/`ParseLIST`: require tag+length header to fit, require the
     declared value length to fit in the REMAINING chunk bytes (`len <= chunk_length - (offset+4)`),
     and copy with a bounded `SDL_memcpy` + explicit NUL instead of `SDL_strlcpy`; `ParseLIST`
     requires `chunk_length >= 4` before reading the "INFO" tag.
   - Reason: offline sandbox could not reach upstream to fetch/commit the fix; landed as an
     explicitly labeled local patch. ACTION on next online sync: diff against SDL_mixer `main`,
     drop this patch if upstream already fixed it, otherwise upstream it. EXTERNAL BLOCKER: needs
     network access to diff the current upstream.
