get_filename_component(REPO_ROOT "${CMAKE_CURRENT_LIST_DIR}/../.." ABSOLUTE)
file(READ "${REPO_ROOT}/src/source/App/Platform/Windows/Winmain.cpp" main)

function(require_block source marker needle)
    string(FIND "${source}" "${marker}" start)
    if(start LESS 0)
        message(FATAL_ERROR "Missing startup window contract: ${marker}")
    endif()
    string(SUBSTRING "${source}" ${start} 1600 block)
    string(FIND "${block}" "${needle}" found)
    if(found LESS 0)
        message(FATAL_ERROR "${marker} must contain ${needle}")
    endif()
endfunction()

require_block("${main}" "static bool ApplyInitialWindowMode()"
              "SDL_SetWindowFullscreenMode(g_sdlWindow, nullptr)")
require_block("${main}" "static bool ApplyInitialWindowMode()"
              "SDL_SetWindowFullscreen(g_sdlWindow, true)")
require_block("${main}" "static bool ApplyInitialWindowMode()"
              "SDL_SyncWindow(g_sdlWindow)")

string(FIND "${main}" "SDL_WindowFlags windowFlags = SDL_WINDOW_OPENGL;" flags_start)
if(flags_start LESS 0)
    message(FATAL_ERROR "Missing startup window flag initialization")
endif()
string(SUBSTRING "${main}" ${flags_start} 300 flags_block)
string(FIND "${flags_block}" "#if !defined(_WIN32)" windows_guard)
if(windows_guard LESS 0)
    message(FATAL_ERROR "Windows must not request exclusive fullscreen during SDL_CreateWindow")
endif()

require_block("${main}" "if (!ApplyInitialWindowMode())"
              "Initial borderless fullscreen setup failed")
message(STATUS "Windows startup uses borderless fullscreen without an exclusive mode switch")
