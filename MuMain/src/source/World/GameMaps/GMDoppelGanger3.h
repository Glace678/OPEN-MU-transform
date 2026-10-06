// GMDoppelGanger3.h: interface for the GMDoppelGanger3 class.
//////////////////////////////////////////////////////////////////////
#pragma once
#include "World/MapInfra/w_BaseMap.h"

class CGMDoppelGanger3;
using CGMDoppelGanger3Ptr = std::shared_ptr<CGMDoppelGanger3>;class CGMDoppelGanger3 : public BaseMap
{
public:
    static CGMDoppelGanger3Ptr Make();
    virtual ~CGMDoppelGanger3();

public:	// Object
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

public:	// Character
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

public: // Sound
    virtual bool PlayMonsterSound(OBJECT* o);
    // Ʈ 
    virtual void PlayObjectSound(OBJECT* o);

public:
    void Init();
    void Destroy();

protected:
    CGMDoppelGanger3();
};

extern bool IsDoppelGanger3();
