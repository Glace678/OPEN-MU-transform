#include "stdafx.h"
#include <ctype.h>
#include "ZzzBMD.h"
#include "SMD.h"
#include "Core/Utilities/ReadScript.h"


NodeGroup_t     NodeGroup;
SkeletonGroup_t SkeletonGroup;
TriangleGroup_t TriangleGroup;
SMDMeshGroup_t  MeshGroup;

bool ParseNodes()
{
    SMDToken Token;
    NodeGroup_t* ng = &NodeGroup;
    ng->NodeNum = 0;

    while (true)
    {
        Token = (*GetToken)();
        if (Token == END) break;
        if (Token == NAME && strcmp("nodes", TokenString) == 0) break;
    }
    while (true)
    {
        Token = (*GetToken)();
        if (Token == END) break;
        if (Token == NAME && strcmp("end", TokenString) == 0) break;
        if (Token == NUMBER)
        {
            if (ng->NodeNum < 0 || ng->NodeNum >= NODE_MAX)
            {
                g_ErrorReport.Write(L"SMD node limit (%d) exceeded; aborting parse.\r\n", NODE_MAX);
                return false;
            }
            Node_t* n = &ng->Node[ng->NodeNum];
            Token = (*GetToken)(); strncpy(n->Name, TokenString, sizeof(n->Name) - 1); n->Name[sizeof(n->Name) - 1] = '\0';
            Token = (*GetToken)(); n->Parent = (short)TokenNumber;
        }
        ng->NodeNum++;
    }
    while (true)
    {
        Token = (*GetToken)();
        if (Token == END) break;
        if (Token == NAME && strcmp("skeleton", TokenString) == 0) break;
    }
    while (true)
    {
        Token = (*GetToken)();
        if (Token == END) break;
        if (Token == NAME)
        {
            if (strcmp("end", TokenString) == 0) break;
            if (strcmp("time", TokenString) == 0)
            {
                Token = (*GetToken)();
                Skeleton_t* s = &ng->Skeleton;
                for (int i = 0; i < ng->NodeNum; i++)
                {
                    Token = (*GetToken)();
                    Token = (*GetToken)(); s->Position[i][0] = TokenNumber;
                    Token = (*GetToken)(); s->Position[i][1] = TokenNumber;
                    Token = (*GetToken)(); s->Position[i][2] = TokenNumber;
                    Token = (*GetToken)(); s->Rotation[i][0] = TokenNumber;
                    Token = (*GetToken)(); s->Rotation[i][1] = TokenNumber;
                    Token = (*GetToken)(); s->Rotation[i][2] = TokenNumber;
                }
            }
        }
    }

    return true;
}

bool ParseSkeleton()
{
    SMDToken Token;
    while (true)
    {
        Token = (*GetToken)();
        if (Token == END) break;
        if (Token == NAME && strcmp("skeleton", TokenString) == 0) break;
    }

    SkeletonGroup_t* sg = &SkeletonGroup;
    sg->TimeNum = 0;
    while (true)
    {
        Token = (*GetToken)();
        if (Token == END) break;
        if (Token == NAME)
        {
            if (strcmp("end", TokenString) == 0) break;
            if (strcmp("time", TokenString) == 0)
            {
                Token = (*GetToken)();
                int TimeNum = (int)TokenNumber;
                if (TimeNum < 0 || TimeNum >= TIME_MAX || sg->TimeNum >= TIME_MAX)
                {
                    g_ErrorReport.Write(L"SMD skeleton time limit (%d) exceeded; aborting parse.\r\n", TIME_MAX);
                    return false;
                }
                // Store keyframes compactly (SMD keyframes are contiguous from
                // 0); indexing directly by the file's TimeNum could overrun
                // Skeleton[TIME_MAX] for an out-of-range value.
                Skeleton_t* s = &sg->Skeleton[sg->TimeNum];
                for (int i = 0; i < NodeGroup.NodeNum; i++)
                {
                    Token = (*GetToken)();
                    Token = (*GetToken)(); s->Position[i][0] = TokenNumber;
                    Token = (*GetToken)(); s->Position[i][1] = TokenNumber;
                    Token = (*GetToken)(); s->Position[i][2] = TokenNumber;
                    Token = (*GetToken)(); s->Rotation[i][0] = TokenNumber;
                    Token = (*GetToken)(); s->Rotation[i][1] = TokenNumber;
                    Token = (*GetToken)(); s->Rotation[i][2] = TokenNumber;
                }
                sg->TimeNum++;
            }
        }
    }

    return true;
}

bool ParseTriangles(bool Flip)
{
    SMDToken Token;
    while (true)
    {
        Token = (*GetToken)();
        if (Token == END) break;
        if (Token == NAME && strcmp("triangles", TokenString) == 0) break;
    }

    TriangleGroup_t* tg = &TriangleGroup;
    tg->TriangleNum = 0;
    while (true)
    {
        Token = (*GetToken)();
        if (Token == END) break;
        if (Token == NAME)
        {
            if (strcmp("end", TokenString) == 0) break;
            if (tg->TriangleNum < 0 || tg->TriangleNum >= TRIANGLE_MAX)
            {
                g_ErrorReport.Write(L"SMD triangle limit (%d) exceeded; rejecting the model.\r\n", TRIANGLE_MAX);
                return false;
            }

            constexpr int MaximumTextureNameLength = sizeof(tg->TextureName[0]);
            strncpy(tg->TextureName[tg->TriangleNum], TokenString, MaximumTextureNameLength - 1);
            tg->TextureName[tg->TriangleNum][MaximumTextureNameLength - 1] = '\0';
            if (!Flip)
            {
                for (int i = 0; i < 3; i++)
                {
                    SMDVertex_t* v = &tg->Vertex[tg->TriangleNum][i];
                    Token = (*GetToken)(); v->Node = (short)TokenNumber;
                    Token = (*GetToken)(); v->Position[0] = TokenNumber;
                    Token = (*GetToken)(); v->Position[1] = TokenNumber;
                    Token = (*GetToken)(); v->Position[2] = TokenNumber;
                    Token = (*GetToken)(); v->Normal[0] = TokenNumber;
                    Token = (*GetToken)(); v->Normal[1] = TokenNumber;
                    Token = (*GetToken)(); v->Normal[2] = TokenNumber;
                    Token = (*GetToken)(); v->TexCoordU = TokenNumber;
                    Token = (*GetToken)(); v->TexCoordV = 1.f - TokenNumber;
                }
            }
            else
            {
                for (int i = 2; i >= 0; i--)
                {
                    SMDVertex_t* v = &tg->Vertex[tg->TriangleNum][i];
                    Token = (*GetToken)(); v->Node = (short)TokenNumber;
                    Token = (*GetToken)(); v->Position[0] = TokenNumber;
                    Token = (*GetToken)(); v->Position[1] = TokenNumber;
                    Token = (*GetToken)(); v->Position[2] = TokenNumber;
                    Token = (*GetToken)(); v->Normal[0] = TokenNumber;
                    Token = (*GetToken)(); v->Normal[1] = TokenNumber;
                    Token = (*GetToken)(); v->Normal[2] = TokenNumber;
                    Token = (*GetToken)(); v->TexCoordU = TokenNumber;
                    Token = (*GetToken)(); v->TexCoordV = 1.f - TokenNumber;
                }
            }
            tg->TriangleNum++;
        }
    }

    return true;
}

bool OpenSMDFile(wchar_t* FileName, int Type, bool Flip)
{
    if (FileName == NULL) return false;
    if ((SMDFile = _wfopen(FileName, L"rb")) == NULL)
    {
#ifdef _DEBUG
        extern HWND g_hWnd;
        wchar_t Text[1024];
        mu_swprintf(Text, L"%ls - File not exist..\r\n", FileName);
        g_ErrorReport.Write(Text);
        MessageBox(g_hWnd, Text, NULL, MB_OK);
#endif
        return false;
    }

    (*GetToken)();
    (*GetToken)();

    bool parsed = true;
    if (Type == REFERENCE_FRAME)
    {
        parsed = ParseNodes();
        if (parsed) parsed = ParseTriangles(Flip);
    }
    if (Type == SKELETAL_ANIMATION)
    {
        parsed = ParseSkeleton();
    }

    fclose(SMDFile);
    return parsed;
}

bool FixupSMD();
void Triangle2Strip();
void SMD2BMDModel(int ID, int Actions);
void SMD2BMDAnimation(int ID, bool LockPosition);

bool OpenSMDModel(int ID, wchar_t* FileName1, int Actions, bool Flip)
{
    if (Models[ID].NumMeshs > 0) return false;

    if (OpenSMDFile(FileName1, REFERENCE_FRAME, Flip))
    {
        WideCharToMultiByte(CP_UTF8, 0, FileName1, wcslen(FileName1), Models[ID].Name, 32, 0, 0);
        Models[ID].Version = 10;
        if (!FixupSMD())
        {
            return false;
        }
        SMD2BMDModel(ID, Actions);
        return true;
    }
    return false;
}

bool OpenSMDAnimation(int ID, wchar_t* FileName2, bool LockPosition)
{
    if (Models[ID].NumBones > 0)
    {
        if (OpenSMDFile(FileName2, SKELETAL_ANIMATION, false))
        {
            SMD2BMDAnimation(ID, LockPosition);
            return true;
        }
    }
    return false;
}
