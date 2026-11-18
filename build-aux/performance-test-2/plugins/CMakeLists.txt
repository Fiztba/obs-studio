cmake_minimum_required(VERSION 3.28...3.30)
project(combined_obs_plugins VERSION 33.0.1 LANGUAGES C CXX RC)

list(PREPEND CMAKE_MODULE_PATH "${OBS_SOURCE}/cmake/common" "${OBS_SOURCE}/cmake/finders")
if(NOT CMAKE_VS_WINDOWS_TARGET_PLATFORM_VERSION)
  file(TO_CMAKE_PATH "$ENV{WindowsSDKVersion}" _windows_sdk_version)
  string(REGEX REPLACE "/+$" "" CMAKE_VS_WINDOWS_TARGET_PLATFORM_VERSION "${_windows_sdk_version}")
endif()
set(OS_WINDOWS TRUE)
include("${OBS_SOURCE}/cmake/windows/compilerconfig.cmake")
include(helpers_common)

set(OBS_VERSION_MAJOR 33)
set(OBS_VERSION_MINOR 0)
set(OBS_VERSION_PATCH 1)
set(OBS_VERSION_CANONICAL 33.0.1)
set(OBS_BETA 0)
set(OBS_RELEASE_CANDIDATE 0)
set(OBS_COMPANY_NAME "OBS Project")
set(OBS_PRODUCT_NAME "OBS Studio")
set(OBS_COMMENTS "Free and open source software for video recording and live streaming")
set(OBS_LEGAL_COPYRIGHT "(C) Lain Bailey")
set(OBS_DATA_PATH data)
set(OBS_PLUGIN_PATH obs-plugins)
set(OBS_PLUGIN_DESTINATION obs-plugins/64bit)
configure_file("${OBS_SOURCE}/libobs/obsconfig.h.in" "include/obsconfig.h")

add_library(OBS::libobs SHARED IMPORTED)
set_target_properties(OBS::libobs PROPERTIES IMPORTED_IMPLIB "${IMPORT_DIR}/obs.lib"
  INTERFACE_INCLUDE_DIRECTORIES "${OBS_SOURCE}/libobs;${CMAKE_BINARY_DIR}/include;${CMAKE_PREFIX_PATH}/include")
add_library(OBS::w32-pthreads SHARED IMPORTED)
set_target_properties(OBS::w32-pthreads PROPERTIES IMPORTED_IMPLIB "${IMPORT_DIR}/w32-pthreads.lib"
  INTERFACE_INCLUDE_DIRECTORIES "${OBS_SOURCE}/deps/w32-pthreads")

# Upstream's bundle/install rules are unnecessary for these standalone modules.
function(set_target_properties_obs target)
  file(MAKE_DIRECTORY "${CMAKE_BINARY_DIR}/public-symbols")
  set_target_properties(${target} PROPERTIES ${ARGN} RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/modules"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/modules" PDB_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/modules")
  target_link_options(${target} PRIVATE /PDBALTPATH:%_PDB% "/PDBSTRIPPED:${CMAKE_BINARY_DIR}/public-symbols/${target}.pdb")
endfunction()

add_subdirectory("${OBS_SOURCE}/plugins/image-source" image-source)
add_subdirectory("${OBS_SOURCE}/plugins/obs-filters" obs-filters)
