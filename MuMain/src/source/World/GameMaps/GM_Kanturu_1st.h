//*****************************************************************************
// File: GM_kanturu_1st.h
//
// Desc: Kanturu 1st (outer) map, monsters.
//
// producer: Ahn Sang-Kyu
//*****************************************************************************

#ifndef _GM_KANTURU_1ST_H_
#define _GM_KANTURU_1ST_H_

namespace M37Kanturu1st
{
    bool IsKanturu1st();						// Is this the Kanturu outer map?

    // Objects
    bool CreateKanturu1stObject(OBJECT* pObject);
    bool MoveKanturu1stObject(OBJECT* pObject);
    bool RenderKanturu1stObjectVisual(OBJECT* pObject, BMD* pModel);
    bool RenderKanturu1stObjectMesh(OBJECT* o, BMD* b, bool ExtraMon = 0);	// Object render (including monsters)
    void RenderKanturu1stAfterObjectMesh(OBJECT* o, BMD* b);				// Render translucent objects later.

    // Monsters
    CHARACTER* CreateKanturu1stMonster(int iType, int PosX, int PosY, int Key);	// Monster creation
    bool SetCurrentActionKanturu1stMonster(CHARACTER* c, OBJECT* o);		// Set the monster's current action
    bool AttackEffectKanturu1stMonster(CHARACTER* c, OBJECT* o, BMD* b);	// Monster attack effect
    bool MoveKanturu1stMonsterVisual(CHARACTER* c, OBJECT* o, BMD* b);		// Monster effect update
    void MoveKanturu1stBlurEffect(CHARACTER* c, OBJECT* o, BMD* b);		// Monster weapon afterimage processing
    bool RenderKanturu1stMonsterObjectMesh(OBJECT* o, BMD* b, int ExtraMon);	// Monster object rendering
    bool RenderKanturu1stMonsterVisual(CHARACTER* c, OBJECT* o, BMD* b);	// Monster effect rendering
};

#endif	// _GM_KANTURU_1ST_H_