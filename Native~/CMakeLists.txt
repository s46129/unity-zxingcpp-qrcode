cmake_minimum_required(VERSION 3.21)
project(S46129QRCodeNative LANGUAGES CXX)

include(FetchContent)

if(MSVC)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>" CACHE STRING "Use a self-contained MSVC runtime" FORCE)
endif()

set(BUILD_SHARED_LIBS ON CACHE BOOL "Build ZXing as the Unity native plugin" FORCE)
set(ZXING_READERS ON CACHE BOOL "Enable barcode readers" FORCE)
set(ZXING_WRITERS OFF CACHE STRING "Disable barcode writers" FORCE)
set(ZXING_C_API ON CACHE BOOL "Export the stable C API used by P/Invoke" FORCE)
set(ZXING_EXAMPLES OFF CACHE BOOL "Disable examples" FORCE)
set(ZXING_EXAMPLES_QT OFF CACHE BOOL "Disable Qt examples" FORCE)
set(ZXING_BLACKBOX_TESTS OFF CACHE BOOL "Disable upstream black-box tests" FORCE)
set(ZXING_UNIT_TESTS OFF CACHE BOOL "Disable upstream unit tests" FORCE)
set(ZXING_ENABLE_1D OFF CACHE BOOL "Disable linear formats" FORCE)
set(ZXING_ENABLE_AZTEC OFF CACHE BOOL "Disable Aztec" FORCE)
set(ZXING_ENABLE_DATAMATRIX OFF CACHE BOOL "Disable Data Matrix" FORCE)
set(ZXING_ENABLE_MAXICODE OFF CACHE BOOL "Disable MaxiCode" FORCE)
set(ZXING_ENABLE_PDF417 OFF CACHE BOOL "Disable PDF417" FORCE)
set(ZXING_ENABLE_QRCODE ON CACHE BOOL "Enable QR Code" FORCE)

FetchContent_Declare(
    zxing_cpp
    GIT_REPOSITORY https://github.com/zxing-cpp/zxing-cpp.git
    GIT_TAG 885baaf0840335153c1a487fa65f9c1388702c81
)
FetchContent_MakeAvailable(zxing_cpp)

get_filename_component(S46129_PACKAGE_ROOT "${CMAKE_CURRENT_LIST_DIR}/.." ABSOLUTE)

if(WIN32)
    if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
        message(FATAL_ERROR "ZXing-C++ QR Code supports Windows x86_64 only.")
    endif()
    set(S46129_PLUGIN_DIR "${S46129_PACKAGE_ROOT}/Runtime/Plugins/Windows/x86_64")
elseif(ANDROID)
    if(NOT ANDROID_ABI STREQUAL "arm64-v8a")
        message(FATAL_ERROR "ZXing-C++ QR Code supports Android arm64-v8a only.")
    endif()
    # Unity can pair a newer NDK with an older bundled CMake that omits libc++ from shared-library link lines.
    target_link_libraries(ZXing PRIVATE c++_static c++abi)
    set(S46129_PLUGIN_DIR "${S46129_PACKAGE_ROOT}/Runtime/Plugins/Android/arm64-v8a")
else()
    message(FATAL_ERROR "This package build supports only Windows and Android.")
endif()

if(ANDROID)
    add_custom_target(
        S46129CopyZXing ALL
        COMMAND "${CMAKE_COMMAND}" -E make_directory "${S46129_PLUGIN_DIR}"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "$<TARGET_FILE:ZXing>" "${S46129_PLUGIN_DIR}/$<TARGET_FILE_NAME:ZXing>"
        COMMAND "${CMAKE_STRIP}" --strip-unneeded "${S46129_PLUGIN_DIR}/$<TARGET_FILE_NAME:ZXing>"
        DEPENDS ZXing
        COMMENT "Copying and stripping ZXing native plugin into the Unity package"
    )
else()
    add_custom_target(
        S46129CopyZXing ALL
        COMMAND "${CMAKE_COMMAND}" -E make_directory "${S46129_PLUGIN_DIR}"
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "$<TARGET_FILE:ZXing>" "${S46129_PLUGIN_DIR}/$<TARGET_FILE_NAME:ZXing>"
        DEPENDS ZXing
        COMMENT "Copying ZXing native plugin into the Unity package"
    )
endif()
