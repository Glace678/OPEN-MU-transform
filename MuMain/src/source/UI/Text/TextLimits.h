#pragma once

// Chat/filter/letter/dialog text length limit constants, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

#define MAX_TEXTS               3000
#define MAX_GLOBAL_TEXT_STRING	300
#define MAX_FILTERS 1000

#define MAX_NAMEFILTERS 500

#define MAX_LETTER_TITLE_LENGTH		60
#define MAX_LETTER_DATE_LENGTH		10
#define MAX_LETTER_TIME_LENGTH		8

#define MAX_LETTERTEXT_LENGTH		1000
#define MAX_CHATROOM_TEXT_LENGTH	150
#define MAX_LANGUAGE_NAME_LENGTH	4

#define MAX_GATES           512

#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM
#define MAX_GIFT_MESSAGE_SIZE	200
#endif // KJH_ADD_INGAMESHOP_UI_SYSTEM

#define MAX_LENGTH_DIALOG		( 300)

#define MAX_ANSWER_FOR_DIALOG	( 10)

#define MAX_LENGTH_ANSWER		( 64)
