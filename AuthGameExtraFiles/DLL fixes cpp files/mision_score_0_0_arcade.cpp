// mision_score_0_0_arcade.cpp
// -----------------------------------------------------------------------------
// Fills the green "0 / 0" arcade counter (TextViewer_CalcConPoint).
//
// FINDINGS (reverse engineering, Ghidra base 0x00E80000):
//   The "0/0" is drawn by FUN_01353b80 (a CArcadeScoreBoard method that writes
//   the child widget at scoreboard+0x10 via SetText):
//       localPlayer  = *(uint*)0x025BF528
//       numerator    = FUN_00f4e670(localPlayer + 0x238)   // ConPoint acquired
//       denominator  = FUN_00f4e670(localPlayer + 0x244)   // ConPoint total
//   FUN_00f4e670 is the getter for an anti-tamper encoded field (__fastcall,
//   ptr in ECX): it returns slot[0]^slot[2] and checks slot[0] == ~slot[1].
//   The matching setter is FUN_00f46c90 (this[2]=key; this[0]=v^key; this[1]=~this[0]).
//
//   Why it stays "0/0": NOTHING ever writes a real value into +0x238/+0x244.
//   The GameServer sends no ConPoint (only ArcadeSaveDateInfoAck = 1 byte) and
//   no handler sets those fields. A hardware write BP on lp+0x238 only ever hit a
//   PPL std::vector reallocation (not a setter). A runtime log of the encoded
//   setter FUN_00f46c90 across a full match showed the fields are written ONLY by
//   the constructor (FUN_018f3b90), to 0. The mission text itself comes from the
//   host Lua binding ViewMissionInfo{ MISSIONINFO_ID }, i.e. the whole mission
//   (text + objective progress) is host-Lua driven and does not run in this setup.
//   => Same situation as the boss HP. This is NOT server-fixable.
//
//   This hook is COSMETIC: it intercepts the getter and returns the chosen
//   numerator/denominator for those two fields so the "0/0" shows something.
//   Adjust NUM/DEN below (or wire real progress from client state later).
//
// BUILD (32-bit, x86 Native Tools, from tools/hooks):
//   cl /LD /EHsc /Ox /I Detours-main\include mision_score_0_0_arcade.cpp ^
//      Detours-main\detours.lib /link /OUT:C:\S4Plain\sneoz.dll
// -----------------------------------------------------------------------------

#define _CRT_SECURE_NO_WARNINGS
#include <Windows.h>
#include <cstdint>
#include "detours.h"

// Values shown in the "0/0" counter (numerator / denominator).
#define CONPOINT_NUM 100   // localPlayer+0x238 (acquired)
#define CONPOINT_DEN 200   // localPlayer+0x244 (total)

static const uintptr_t GHIDRA_BASE = 0x00E80000;
static uintptr_t g_base = 0;
static uintptr_t Rebase(uintptr_t a) { return a - GHIDRA_BASE + g_base; }

typedef unsigned int(__fastcall* Getter)(unsigned int* p, void* edx);
static Getter g_orig = nullptr;

static unsigned int __fastcall Hook(unsigned int* p, void* edx) {
    __try {
        uintptr_t lp = *(uintptr_t*)Rebase(0x025BF528);
        if (lp) {
            if ((uintptr_t)p == lp + 0x238) return CONPOINT_NUM;
            if ((uintptr_t)p == lp + 0x244) return CONPOINT_DEN;
        }
    } __except (EXCEPTION_EXECUTE_HANDLER) {}
    return g_orig(p, edx);
}

static DWORD WINAPI Setup(LPVOID) {
    g_base = (uintptr_t)GetModuleHandleA(NULL);
    g_orig = (Getter)Rebase(0x00F4E670);
    DetourTransactionBegin();
    DetourUpdateThread(GetCurrentThread());
    DetourAttach(&(PVOID&)g_orig, (PVOID)Hook);
    DetourTransactionCommit();
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE h, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        LoadLibraryA("mutex.dll");
        DisableThreadLibraryCalls(h);
        CreateThread(nullptr, 0, Setup, nullptr, 0, nullptr);
    }
    return TRUE;
}
