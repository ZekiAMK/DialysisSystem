#define SDL_MAIN_HANDLED

#include <SDL3/SDL.h>
#include <SDL3/SDL_main.h>
#include "doomgeneric.h"
#include <stdarg.h>

#define LOGD(...) SDL_Log(__VA_ARGS__)
#define LOG_TAG "DOOM"

// game_bridge.c
#include "doomgeneric.h"
#include <SDL3/SDL.h>

#define LOGD(...) SDL_Log(__VA_ARGS__)




// SDL3 calls this instead of main()
int SDL_main(int argc, char* argv[]) {
    LOGD("SDL_main entered");

    // argv[1] should be the WAD path passed via Intent extras
    const char* wadPath = argc > 1 ? argv[1] : "/data/data/com.companyname.mobile/files/doom1.wad";

    FILE* f = fopen(wadPath, "rb");
    if (!f) {
        LOGD("WAD file cannot be opened: %s", wadPath);
        return 1;
    }
    fseek(f, 0, SEEK_END);
    long size = ftell(f);
    fclose(f);
    LOGD("WAD file exists, size: %ld bytes", size);

    char* doomArgv[4];
    char fullPath[512];
    snprintf(fullPath, sizeof(fullPath), "%s", wadPath);
    doomArgv[0] = "doom_generic";
    doomArgv[1] = "-iwad";
    doomArgv[2] = fullPath;
    doomArgv[3] = NULL;

    doomgeneric_Create(3, doomArgv);

    // Game loop
    while (1) {
        doomgeneric_Tick();
    }

    return 0;
}

void Game_Tick() {

    //LOGD("Game_Tick called");

    doomgeneric_Tick();
}

void Game_Stop() {
    return;
}
