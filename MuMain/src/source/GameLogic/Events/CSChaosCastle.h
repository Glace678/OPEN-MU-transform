//////////////////////////////////////////////////////////////////////////
//
//  CSChaosCastle.h
//
//  Content : Chaos Castle match.
//
//  Date : 2004/04/22
//
//  Author : Cho Kyu-ha.
//
//////////////////////////////////////////////////////////////////////////
#ifndef __CSCHAOS_CASTLE_H__
#define __CSCHAOS_CASTLE_H__

void    ClearChaosCastleHelper(CHARACTER* c);
void    ChangeChaosCastleUnit(CHARACTER* c);
bool    CreateChaosCastleObject(OBJECT* o);
bool    MoveChaosCastleObjectSetting(int& objCount, int object);
bool    MoveChaosCastleObject(OBJECT* o, int& object, int& visibleObject);
bool    MoveChaosCastleAllObject(OBJECT* o);
bool    RenderChaosCastleVisual(OBJECT* o, BMD* b);
void    RenderTerrainVisual(int xi, int yi);

#endif// __CSCHAOS_CASTLE_H__
