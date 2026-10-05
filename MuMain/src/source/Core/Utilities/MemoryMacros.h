#pragma once

// Behavior-preserving replacements for the former SAFE_DELETE / SAFE_DELETE_ARRAY
// macros. These are type-safe inline function templates. Ownership migration to
// std::unique_ptr / std::shared_ptr is performed gradually as the owning files
// are refactored (see docs/refactoring-plan.md). They mirror the old macro
// semantics exactly: a null pointer is skipped, and the pointer is reset to
// null after deletion.
template <class T>
inline void SafeDelete(T*& p)
{
    if (p != nullptr)
    {
        delete p;
        p = nullptr;
    }
}

template <class T>
inline void SafeDeleteArray(T*& p)
{
    if (p != nullptr)
    {
        delete[] p;
        p = nullptr;
    }
}