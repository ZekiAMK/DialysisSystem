cmake_minimum_required(VERSION 3.6)
project(my_app)

if(NOT TARGET SDL3::SDL3)
    find_package(SDL3 CONFIG REQUIRED)
endif()

# All doomgeneric sources except the original SDL backend
file(GLOB SRC_DOOM "doomgeneric/*.c")
list(FILTER SRC_DOOM EXCLUDE REGEX "doomgeneric_[a-z]+\\.c$")
list(FILTER SRC_DOOM EXCLUDE REGEX "i_allegro[a-z]+\\.c$")
list(FILTER SRC_DOOM EXCLUDE REGEX "i_sdlsound\\.c$")
list(FILTER SRC_DOOM EXCLUDE REGEX "i_sdlmusic\\.c$")

# Add back only our SDL3/Android backend
list(APPEND SRC_DOOM
    "${CMAKE_CURRENT_SOURCE_DIR}/doomgeneric_sdl3.c"
)

add_library(main SHARED
    ${SRC_DOOM}
    doomgeneric_sdl3.c
    game_bridge.c
)

target_link_options(main PRIVATE "-Wl,--allow-multiple-definition")

target_include_directories(main PRIVATE
    doomgeneric/
)

#target_compile_definitions(main PRIVATE FEATURE_SOUND)

target_link_libraries(main PRIVATE
    SDL3::SDL3-static
    android
    log
    m
)