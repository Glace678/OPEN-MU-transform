//*****************************************************************************
// File: GMKarutan1.h
//
// Desc: ųź1 , .
//
// producer: Ahn Sang-Kyu (10.08.03)
//*****************************************************************************

#if !defined(AFX_GMKARUTAN1_H__A2F56C80_26D8_4474_AECE_63DA2DA511A9__INCLUDED_)
#define AFX_GMKARUTAN1_H__A2F56C80_26D8_4474_AECE_63DA2DA511A9__INCLUDED_

#pragma once

#ifdef ASG_ADD_MAP_KARUTAN

#include "World/MapInfra/w_BaseMap.h"

class CGMKarutan1;
using CGMKarutan1Ptr = std::shared_ptr<CGMKarutan1>;
class CGMKarutan1 : public BaseMap
{
protected:
    CGMKarutan1();

public:
    virtual ~CGMKarutan1();

    static CGMKarutan1Ptr Make();

    // Object
        // Ʈ 
    virtual bool CreateObject(OBJECT* o);
    // Ʈ μ
    virtual bool MoveObject(OBJECT* o);
    // Ʈ Ʈ
    virtual bool RenderObjectVisual(OBJECT* o, BMD* b);
    // Ʈ 
    virtual bool RenderObjectMesh(OBJECT* o, BMD* b, bool ExtraMon = 0);
    // Ʈ Ʈ
    virtual void RenderAfterObjectMesh(OBJECT* o, BMD* b, bool ExtraMon = 0);

#ifdef ASG_ADD_KARUTAN_MONSTERS
    // Character
    virtual CHARACTER* CreateMonster(int iType, int PosX, int PosY, int Key);
    // (NPC) μ
    virtual bool MoveMonsterVisual(OBJECT* o, BMD* b);
    // ų Ʈ
    virtual void MoveBlurEffect(CHARACTER* c, OBJECT* o, BMD* b);
    // Ʈ ( Ϲ )
    virtual bool RenderMonsterVisual(CHARACTER* c, OBJECT* o, BMD* b);
    // Ʈ ( ų )
    virtual bool AttackEffectMonster(CHARACTER* c, OBJECT* o, BMD* b);
    // ų ִϸ̼ Լ
    virtual bool SetCurrentActionMonster(CHARACTER* c, OBJECT* o);

    // Sound
    virtual bool PlayMonsterSound(OBJECT* o);
#endif	// ASG_ADD_KARUTAN_MONSTERS
    // Ʈ 
    virtual void PlayObjectSound(OBJECT* o);
    void PlayBGM();
};

extern bool IsKarutanMap();

#endif	// ASG_ADD_MAP_KARUTAN

#endif // !defined(AFX_GMKARUTAN1_H__A2F56C80_26D8_4474_AECE_63DA2DA511A9__INCLUDED_)
