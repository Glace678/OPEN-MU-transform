cmake_minimum_required(VERSION 3.24)
get_filename_component(REPO_ROOT "${CMAKE_CURRENT_LIST_DIR}/../.." ABSOLUTE)

# Contract checks compare source *semantics*, not formatting. Collapse every
# whitespace run (including newlines) so that clang-format re-wrapping /
# alignment (e.g. one-line "if" statements, aligned assignments) cannot cause
# a spurious contract failure.
function(normalize_ws text out)
    string(REGEX REPLACE "[ \t\r\n]+" " " normalized "${text}")
    string(STRIP "${normalized}" normalized)
    set(${out} "${normalized}" PARENT_SCOPE)
endfunction()

function(assert_contains text needle context)
    normalize_ws("${text}" text_n)
    normalize_ws("${needle}" needle_n)
    string(FIND "${text_n}" "${needle_n}" position)
    if(position LESS 0)
        message(FATAL_ERROR "${context}: missing ${needle}")
    endif()
endfunction()

function(read_section path start finish output)
    file(READ "${REPO_ROOT}/${path}" contents)
    string(FIND "${contents}" "${start}" first)
    string(FIND "${contents}" "${finish}" last)
    if(first LESS 0 OR last LESS first)
        message(FATAL_ERROR "Cannot locate source-contract section in ${path}")
    endif()
    math(EXPR length "${last} - ${first}")
    string(SUBSTRING "${contents}" ${first} ${length} section)
    normalize_ws("${section}" section)
    set(${output} "${section}" PARENT_SCOPE)
endfunction()

read_section("src/source/UI/Windows/LoginWin.cpp"
    "CLoginWin::ResizeState CLoginWin::CaptureResizeState()"
    "void CLoginWin::SetPosition(" state_code)
foreach(field username password previousUsername previousPassword rememberUsername savePassword initialFocusPending focus)
    assert_contains("${state_code}" "state.${field}" "Resize snapshot ${field}")
endforeach()
foreach(needle
    "m_pUsernameInputBox->SetText(state.username)"
    "m_pPasswordInputBox->SetText(state.password)"
    "m_aBtnRememberMe.SetCheck(state.rememberUsername)"
    "m_aBtnSavePassword.SetCheck(state.savePassword)"
    "!IsShow() || state.focus == EntryFocus::None")
    assert_contains("${state_code}" "${needle}" "Restore entered state without stealing focus")
endforeach()
assert_contains("${state_code}" "field->GiveFocus(FALSE, false)" "Silent focus restoration")
assert_contains("${state_code}" "FirstLoad = 0" "Restored focus must not trigger first-render focus")
assert_contains("${state_code}" "state.initialFocusPending ? 1 : 0" "Keep original first-render state")
assert_contains("${state_code}" "m_prevUsername, _countof(m_prevUsername), state.previousUsername" "Keep account edit baseline")
assert_contains("${state_code}" "m_prevPassword, _countof(m_prevPassword), state.previousPassword" "Keep password edit baseline")
normalize_ws("${state_code}" state_code_ws)
if(state_code_ws MATCHES "GameConfig|Encrypt|Decrypt|Save\\(|Log\\(|fopen|fprintf|Write\\(")
    message(FATAL_ERROR "Resize state must not read/write persisted credentials or log field contents")
endif()

read_section("src/source/UI/Legacy/UIMng.cpp" "void CUIMng::RepositionSceneUI()" "CWin* CUIMng::SetActiveWin(" resize_code)
foreach(needle "CaptureResizeState()" "CUITextInputBox::ReleaseFocus()" "CreateLoginScene()" "RestoreResizeState(loginEntry)")
    assert_contains("${resize_code}" "${needle}" "Resize-only integration")
endforeach()
string(FIND "${resize_code}" "CaptureResizeState()" capture)
string(FIND "${resize_code}" "CUITextInputBox::ReleaseFocus()" release)
string(FIND "${resize_code}" "CreateLoginScene();" recreate)
string(FIND "${resize_code}" "ShowWin(&m_LoginWin)" show)
string(FIND "${resize_code}" "RestoreResizeState(loginEntry)" restore)
if(NOT capture LESS release OR NOT release LESS recreate OR NOT recreate LESS show OR NOT show LESS restore)
    message(FATAL_ERROR "Login entry must be captured before recreation and restored after visibility")
endif()

file(READ "${REPO_ROOT}/src/source/UI/Legacy/UIControls.h" controls_header)
assert_contains("${controls_header}" "GiveFocus(BOOL bSel = FALSE, bool requestKeyboard = true)" "Existing focus calls must still request input")
read_section("src/source/UI/Legacy/UIControls.cpp" "void CUITextInputBox::GiveFocus(" "// Symmetric counterpart" focus_code)
assert_contains("${focus_code}" "s_bTextInputRequested = requestKeyboard" "Silent focus must clear the pending request")

file(READ "${REPO_ROOT}/src/source/UI/Windows/ServerSelWin.cpp" server_code)
assert_contains("${server_code}" "descriptionArtHeight = m_winDescription.GetHeight() / CSprite::ResolutionScaleY()" "Description must not be scaled twice")
assert_contains("${server_code}" "+ descriptionArtHeight" "Server layout must use converted description height")
message(STATUS "Login resize source contracts passed; Android keyboard interaction requires device validation")