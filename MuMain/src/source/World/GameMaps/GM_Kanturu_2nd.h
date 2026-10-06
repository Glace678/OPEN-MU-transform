// GM_Kanturu_In.h: interface for the GM_Kanturu_In class.
//
//////////////////////////////////////////////////////////////////////

#if !defined(AFX_GM_KANTURU_IN_H__37F1E344_37B1_497A_9B04_84409C768A74__INCLUDED_)
#define AFX_GM_KANTURU_IN_H__37F1E344_37B1_497A_9B04_84409C768A74__INCLUDED_

#pragma once

#include "Render/Models/ZzzBMD.h"

// TODO
namespace M38Kanturu2nd
{
    // Kanturu objects
    bool Create_Kanturu2nd_Object(OBJECT* o);										// Object creation
    bool Move_Kanturu2nd_Object(OBJECT* o);											// Object update
    bool Render_Kanturu2nd_ObjectVisual(OBJECT* o, BMD* b);							// Object effect
    bool Render_Kanturu2nd_ObjectMesh(OBJECT* o, BMD* b, bool ExtraMon = 0);			// Object rendering (including monsters)
    void Render_Kanturu2nd_AfterObjectMesh(OBJECT* o, BMD* b);

    // Kanturu interior monsters
    CHARACTER* Create_Kanturu2nd_Monster(int iType, int PosX, int PosY, int Key);	// Monster creation function
    bool	Set_CurrentAction_Kanturu2nd_Monster(CHARACTER* c, OBJECT* o);		// Set the monster's current action
    bool	AttackEffect_Kanturu2nd_Monster(CHARACTER* c, OBJECT* o, BMD* b);	// Monster attack effect
    bool	Move_Kanturu2nd_MonsterVisual(CHARACTER* c, OBJECT* o, BMD* b);		// Monster effect update
    void	Move_Kanturu2nd_BlurEffect(CHARACTER* c, OBJECT* o, BMD* b);		// Monster weapon afterimage processing
    bool	Render_Kanturu2nd_MonsterObjectMesh(OBJECT* o, BMD* b, int ExtraMon);	// Monster object rendering
    bool	Render_Kanturu2nd_MonsterVisual(CHARACTER* c, OBJECT* o, BMD* b);	// Monster effect rendering

    // Kanturu interior map
    bool		Is_Kanturu2nd();						// Is this the Kanturu interior map?
    bool		Is_Kanturu2nd_3rd();					// Is this the Kanturu interior and 3rd map?

    // Sound
    void	Sound_Kanturu2nd_Object(OBJECT* o);		// Object sound
    void	PlayBGM();
};

class CTrapCanon
{
public:
    CTrapCanon();
    ~CTrapCanon();

private:
    void Initialize();
    void Destroy();

public:
    void Open_TrapCanon();
    CHARACTER* Create_TrapCanon(int iPosX, int iPosY, int iKey);
    void Render_Object(OBJECT* o, BMD* b);
    void Render_Object_Visual(CHARACTER* c, OBJECT* o, BMD* b);
    void Render_AttackEffect(CHARACTER* c, OBJECT* o, BMD* b);
};

extern CTrapCanon g_TrapCanon;

#endif // !defined(AFX_GM_KANTURU_IN_H__37F1E344_37B1_497A_9B04_84409C768A74__INCLUDED_)
