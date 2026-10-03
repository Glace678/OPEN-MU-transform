// Streaming copy / write helpers shared by the launcher pages and the config
// writer. Mirrors the Android MobilePreferences implementation: a ~2 GB rawfile
// archive must never be pulled into the JS heap in one piece, and a write may
// legally come back short, so every write loops until the data is on disk.
import { fileIo as fs } from '@kit.CoreFileKit';

const COPY_CHUNK_BYTES = 8 * 1024 * 1024;

// Copies a bundled rawfile into the sandbox chunk by chunk.
// resourceManager.getRawFd opens the payload without reading it and hands back
// {fd, offset, length}, which keeps memory flat on low-end devices. Throws --
// after closing the half-written destination -- if the source ends early or
// the byte count does not add up, so a truncated archive is never mistaken
// for a complete one.
export async function copyRawfileTo(context: Context, rawName: string, destPath: string): Promise<void> {
  const source = await context.resourceManager.getRawFd(rawName);
  const out = fs.openSync(destPath, fs.OpenMode.CREATE | fs.OpenMode.READ_WRITE | fs.OpenMode.TRUNC);
  try {
    const buffer = new ArrayBuffer(COPY_CHUNK_BYTES);
    let copied = 0;
    while (copied < source.length) {
      const want = Math.min(COPY_CHUNK_BYTES, source.length - copied);
      const read = fs.readSync(source.fd, buffer, { offset: source.offset + copied, length: want });
      if (read <= 0) {
        throw new Error('unexpected end of ' + rawName + ' after ' + copied + ' bytes');
      }
      writeRange(out.fd, buffer, 0, read);
      copied += read;
    }
    if (copied !== source.length) {
      throw new Error('copied ' + copied + ' of ' + source.length + ' bytes of ' + rawName);
    }
  } finally {
    try {
      fs.closeSync(out);
    } catch (e) {
      // Best effort.
    }
    try {
      fs.closeSync(source.fd);
    } catch (e) {
      // Best effort: the descriptor may already have been released.
    }
  }
}

// Writes text (config.ini) through a short-write-safe loop. fs.writeSync can
// accept fewer bytes than offered; looping keeps a small config correct too.
export function writeTextSync(fd: number, text: string): void {
  const bytes = new TextEncoder().encode(text);
  // Copy into an exactly-sized buffer so offset 0 is unambiguous regardless of
  // how the writeSync offset option is interpreted.
  const exact = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer;
  writeRange(fd, exact, 0, exact.byteLength);
}

function writeRange(fd: number, buffer: ArrayBuffer, bufferOffset: number, length: number): void {
  let written = 0;
  while (written < length) {
    const n = fs.writeSync(fd, buffer, {
      offset: bufferOffset + written,
      length: length - written
    });
    if (n <= 0) {
      throw new Error('short write: stopped after ' + written + ' of ' + length + ' bytes');
    }
    written += n;
  }
}
