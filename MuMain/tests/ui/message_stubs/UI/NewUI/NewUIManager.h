#pragma once
namespace SEASON3B
{
    inline constexpr int INTERFACE_MESSAGEBOX = 1;
    class CNewUIManager
    {
    public:
        template<class T> void AddUIObj(int, T*) {}
        template<class T> void RemoveUIObj(T*) {}
    };
}
