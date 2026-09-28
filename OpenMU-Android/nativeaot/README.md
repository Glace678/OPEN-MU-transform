# MuMain ClientLibrary Android NativeAOT

This directory contains the reproducible WSL build support and logs for the
Android ARM64 NativeAOT version of `MUnique.Client.Library`.

The final library is copied to:

`../native/arm64-v8a/libMUnique.Client.Library.so`

From PowerShell:

```powershell
wsl.exe -d Ubuntu --cd . -- bash ./OpenMU-Android/nativeaot/build-clientlibrary-android.sh
```

The script:

1. Downloads Android NDK r27c into the WSL user's cache when needed.
2. Verifies the archive against Google's published SHA-1 checksum.
3. Publishes the existing ClientLibrary for `linux-bionic-arm64` with
   `NativeLib=Shared` and isolated build directories.
4. Copies the deployable library into the Android ABI directory.
5. Checks the ELF architecture, Android system dependencies, every managed
   `UnmanagedCallersOnly` export, and the SHA-256 digest.

`publish.log` contains the latest compiler output and `verify.txt` contains the
latest binary audit. The final shared library only depends on Android's
`libdl.so`, `libm.so`, and `libc.so`.
