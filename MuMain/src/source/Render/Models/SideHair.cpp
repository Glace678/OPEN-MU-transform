// SideHair.cpp: implementation of the CSideHair
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Render/Models/ZzzBMD.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Engine/Object/ZzzObject.h"
#include "Render/Models/ShadowVolume.h"
#include "Render/Terrain/ZzzLodTerrain.h"
#include "Render/Textures/ZzzTexture.h"
#include "SideHair.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Render/Core/ImmediateRenderer.h"
#include "Render/Shaders/PassthroughShader.h"
#include "Render/Core/RenderConfig.h"

//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

CSideHair::CSideHair() = default;

CSideHair::~CSideHair()
{
    Destroy();
}

namespace
{
    constexpr int HairMesh = 1;

    bool HasVisibleHair(const BMD* model, const OBJECT* object)
    {
        return model != nullptr && object != nullptr && model->NumMeshs > HairMesh
            && object->Alpha >= 0.01f
            && object->HiddenMesh != -2 && object->BlendMesh != -2
            && object->HiddenMesh != HairMesh && object->BlendMesh != HairMesh;
    }
}

void CSideHair::Create(vec3_t ppVertexTransformed[MAX_MESH][MAX_VERTICES], BMD* b, OBJECT* o, bool SkipTga)
{
    Destroy();
    if (!HasVisibleHair(b, o))
        return;
    const bool hasAlpha = Bitmaps[b->IndexTexture[HairMesh]].Components == 4;
    if (SkipTga && hasAlpha)
        return;

    VectorSubtract(Hero->Object.Position, g_Camera.Position, m_vLight);
    VectorNormalize(m_vLight);
    const auto& mesh = b->Meshs[HairMesh];
    if (mesh.NumTriangles <= 0)
        return;
    constexpr int EdgesPerTriangle = 3;
    m_pEdges = new St_Edges[mesh.NumTriangles * EdgesPerTriangle];
    DeterminateSilhouette(HairMesh, ppVertexTransformed, mesh.NumTriangles, mesh.Triangles, hasAlpha);
}

void CSideHair::Destroy(void)
{
    delete[] m_pEdges;
    delete[] m_pVertices;
    Clear();
}

void CSideHair::Render(vec3_t ppVertexTransformed[MAX_MESH][MAX_VERTICES], vec3_t ppLightTransformed[MAX_MESH][MAX_VERTICES])
{
    for (int i = 0; i < m_iNumEdge; ++i)
    {
        RenderLine(ppVertexTransformed[m_pEdges[i].m_nMesh][m_pEdges[i].m_nVertexIndex[0]],
            ppVertexTransformed[m_pEdges[i].m_nMesh][m_pEdges[i].m_nVertexIndex[1]],
            ppLightTransformed[m_pEdges[i].m_nMesh][m_pEdges[i].m_nNormalIndex[0]],
            ppLightTransformed[m_pEdges[i].m_nMesh][m_pEdges[i].m_nNormalIndex[1]]);
    }
}

void CSideHair::RenderLine(vec3_t v1, vec3_t v2, vec3_t c1, vec3_t c2)
{
    vec3_t p1, p2, d;

    glColor3f(1.f, 1.f, 1.f);
    VectorSubtract(v2, v1, d);
    const float fLength = VectorLength(d);
    float fTextureMove = 0.0f;
    fTextureMove = (50.0f - fLength) * 0.5f / 50.0f;

    VectorCopy(v1, p1);
    VectorCopy(v2, p2);
    VectorSubtract(p2, p1, d);
    VectorScale(d, 0.1f, d);
    VectorSubtract(p1, d, p1);
    VectorAdd(p2, d, p2);

    float fTextureV = (float)(rand() % 100) * 0.01f;
    glColor3f(1.f, 1.f, 1.f);
    BindTexture(BITMAP_ROBE + 4);
    EnableAlphaBlendMinus();
    vec3_t vOrtho;
    CrossProduct(m_vLight, d, vOrtho);
    VectorNormalize(vOrtho);
    VectorScale(vOrtho, 10.f, vOrtho);

    IR::Begin(GL_QUADS);
    PassthroughShader::Instance().SetUseTexture(true);
    IR::Color3f(1.f, 1.f, 1.f);
    IR::TexCoord2f(0.f, 0.f + fTextureMove + fTextureV); IR::Vertex3f(p1[0] - vOrtho[0], p1[1] - vOrtho[1], p1[2] - vOrtho[2]);
    IR::TexCoord2f(0.f, 1.f - fTextureMove + fTextureV); IR::Vertex3f(p2[0] - vOrtho[0], p2[1] - vOrtho[1], p2[2] - vOrtho[2]);
    IR::TexCoord2f(1.f, 1.f - fTextureMove + fTextureV); IR::Vertex3f(p2[0] + vOrtho[0], p2[1] + vOrtho[1], p2[2] + vOrtho[2]);
    IR::TexCoord2f(1.f, 0.f + fTextureMove + fTextureV); IR::Vertex3f(p1[0] + vOrtho[0], p1[1] + vOrtho[1], p1[2] + vOrtho[2]);
    IR::End();
}
