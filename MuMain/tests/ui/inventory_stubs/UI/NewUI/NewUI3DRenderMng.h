#pragma once
namespace SEASON3B
{
    class CNewUI3DRenderMng;
    class CNewUIObj
    {
    public:
        bool IsVisible() const { return true; }
        bool IsEnabled() const { return true; }
    };
    class INewUI3DRenderObj
    {
    public:
        virtual ~INewUI3DRenderObj() = default;
        virtual void Render3D() = 0;
    };
}
