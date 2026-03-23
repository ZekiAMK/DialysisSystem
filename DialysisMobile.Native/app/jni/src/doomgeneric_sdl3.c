#include "doomgeneric.h"
#include "doomkeys.h"

#define SDL_MAIN_HANDLED
#include <SDL3/SDL.h>
#include <SDL3/SDL_main.h>

#define TAG "DoomGeneric"
#define LOGI(...) SDL_Log(__VA_ARGS__)

// INPUT HANDLING STUFF FOR TOUCH

typedef struct {
    float x, y;         // normalized 0-1
    int pressed;
    unsigned char doomKey;
} TouchButton;

static TouchButton s_touchButtons[] = {
    // left side - movement
    { 0.05f, 0.7f, 0, KEY_LEFTARROW  },
    { 0.20f, 0.7f, 0, KEY_RIGHTARROW },
    { 0.12f, 0.5f, 0, KEY_UPARROW    },
    { 0.12f, 0.85f,0, KEY_DOWNARROW  },
    // right side - actions
    { 0.80f, 0.7f, 0, KEY_FIRE       },
    { 0.92f, 0.7f, 0, KEY_USE        },
    // top
    { 0.5f,  0.1f, 0, KEY_ENTER      },
    { 0.1f,  0.1f, 0, KEY_ESCAPE     },
};
#define NUM_TOUCH_BUTTONS (sizeof(s_touchButtons) / sizeof(s_touchButtons[0]))

#define TOUCH_BUTTON_RADIUS 0.08f  // in normalized coords (adjust to taste)




static SDL_Window* s_window = NULL;
static SDL_Renderer* s_renderer = NULL;
static SDL_Texture* s_texture = NULL;

static unsigned char convertToDoomKey(SDL_Keycode key) {
    switch (key) {
        case SDLK_RETURN:   return KEY_ENTER;
        case SDLK_ESCAPE:   return KEY_ESCAPE;
        case SDLK_LEFT:     return KEY_LEFTARROW;
        case SDLK_RIGHT:    return KEY_RIGHTARROW;
        case SDLK_UP:       return KEY_UPARROW;
        case SDLK_DOWN:     return KEY_DOWNARROW;
        case SDLK_LCTRL:
        case SDLK_RCTRL:    return KEY_FIRE;
        case SDLK_SPACE:    return KEY_USE;
        case SDLK_LSHIFT:
        case SDLK_RSHIFT:   return KEY_RSHIFT;
        case SDLK_LALT:
        case SDLK_RALT:     return KEY_LALT;
        case SDLK_F2:       return KEY_F2;
        case SDLK_F3:       return KEY_F3;
        case SDLK_F4:       return KEY_F4;
        case SDLK_F5:       return KEY_F5;
        case SDLK_F6:       return KEY_F6;
        case SDLK_F7:       return KEY_F7;
        case SDLK_F8:       return KEY_F8;
        case SDLK_F9:       return KEY_F9;
        case SDLK_F10:      return KEY_F10;
        case SDLK_F11:      return KEY_F11;
        case SDLK_EQUALS:
        case SDLK_PLUS:     return KEY_EQUALS;
        case SDLK_MINUS:    return KEY_MINUS;
        default:
            if (key >= 32 && key < 128) return (unsigned char)key;
            return 0;
    }
}

void DG_Init() {
    LOGI("DG_Init: before SDL_Init");
    
    // Do NOT call SDL_SetMainReady() - SDLActivity already handles this
    
    if (!SDL_Init(SDL_INIT_VIDEO | SDL_INIT_AUDIO)) {
        LOGI("DG_Init: SDL_Init failed: %s", SDL_GetError());
        return;
    }
    LOGI("DG_Init: SDL_Init succeeded");
    
    s_window = SDL_CreateWindow("Doom", DOOMGENERIC_RESX, DOOMGENERIC_RESY, SDL_WINDOW_FULLSCREEN);
    if (!s_window) {
        LOGI("DG_Init: SDL_CreateWindow failed: %s", SDL_GetError());
        return;
    }
    LOGI("DG_Init: window=%p", s_window);
    
    s_renderer = SDL_CreateRenderer(s_window, NULL);
    if (!s_renderer) {
        LOGI("DG_Init: SDL_CreateRenderer failed: %s", SDL_GetError());
        return;
    }
    LOGI("DG_Init: renderer=%p", s_renderer);
    
    s_texture = SDL_CreateTexture(s_renderer, SDL_PIXELFORMAT_RGBX8888,
        SDL_TEXTUREACCESS_STREAMING, DOOMGENERIC_RESX, DOOMGENERIC_RESY);
    if (!s_texture) {
        LOGI("DG_Init: SDL_CreateTexture failed: %s", SDL_GetError());
        return;
    }
    
    LOGI("DG_Init done");
}
void DG_DrawFrame() {
    //LOGI("DG_DrawFrame called");
    
    if (!s_renderer) {
        LOGI("DG_DrawFrame: no renderer");
        return;
    }
    if (!s_texture) {
        LOGI("DG_DrawFrame: no texture");
        return;
    }

    int result = SDL_UpdateTexture(s_texture, NULL, DG_ScreenBuffer, DOOMGENERIC_RESX * 4);
    if (!result) {
        LOGI("SDL_UpdateTexture failed: %s", SDL_GetError());
        return;
    }

    SDL_RenderClear(s_renderer);
    SDL_RenderTexture(s_renderer, s_texture, NULL, NULL);
    SDL_RenderPresent(s_renderer);
    //LOGI("DG_DrawFrame: frame posted");
}

void DG_SleepMs(uint32_t ms) {
    SDL_Delay(ms);
}

uint32_t DG_GetTicksMs() {
    return (uint32_t)SDL_GetTicks();
}
// OLD WITH KEYS
/*
int DG_GetKey(int* pressed, unsigned char* doomKey) {
    SDL_Event e;
    while (SDL_PollEvent(&e)) {
        if (e.type == SDL_EVENT_KEY_DOWN || e.type == SDL_EVENT_KEY_UP) {
            *pressed = (e.type == SDL_EVENT_KEY_DOWN) ? 1 : 0;
            *doomKey = convertToDoomKey(e.key.key);
            return 1;
        }
    }
    return 0;
}
*/

int DG_GetKey(int* pressed, unsigned char* doomKey) {
    SDL_Event e;
    while (SDL_PollEvent(&e)) {
        if (e.type == SDL_EVENT_KEY_DOWN || e.type == SDL_EVENT_KEY_UP) {
            *pressed = (e.type == SDL_EVENT_KEY_DOWN) ? 1 : 0;
            *doomKey = convertToDoomKey(e.key.key);
            if (*doomKey == 0) continue;  // skip unknown keys
            return 1;
        }

        if (e.type == SDL_EVENT_FINGER_DOWN || e.type == SDL_EVENT_FINGER_UP) {
            float tx = e.tfinger.x;  // normalized 0-1
            float ty = e.tfinger.y;

            for (int i = 0; i < NUM_TOUCH_BUTTONS; i++) {
                float dx = tx - s_touchButtons[i].x;
                float dy = ty - s_touchButtons[i].y;
                int hit = (dx > -TOUCH_BUTTON_RADIUS && dx < TOUCH_BUTTON_RADIUS &&
                           dy > -TOUCH_BUTTON_RADIUS && dy < TOUCH_BUTTON_RADIUS);

                if (hit) {
                    *pressed = (e.type == SDL_EVENT_FINGER_DOWN) ? 1 : 0;
                    *doomKey = s_touchButtons[i].doomKey;
                    return 1;
                }
            }
        }
    }
    return 0;
}

void DG_SetWindowTitle(const char* title) {
    if (s_window) SDL_SetWindowTitle(s_window, title);
}