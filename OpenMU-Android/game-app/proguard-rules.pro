# F1 (M1-02): release shrinking/obfuscation for the game shell.
# The game app drives native code through SDLActivity subclasses and JNI; keep
# the classes the native runtime calls back into and all native method bindings.

# Keep the game shell's own activities and policy/identity classes intact.
-keep class net.munique.openmu.game.** { *; }

# Keep SDL and its JNI bridge.
-keep class org.libsdl.** { *; }

# Keep all native methods (JNI entry points used by the C++/SDL layer).
-keepclasseswithmembernames class * {
    native <methods>;
}

# Keep enum valueOf/values referenced reflectively.
-keepclassmembers class * extends java.lang.Enum {
    public static **[] values();
    public static ** valueOf(java.lang.String);
}
