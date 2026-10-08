# PrepareAssets.cmake
#
# Reproducible clean-checkout asset prep for CI (F37):
#   cmake -DASSETS_DIR=<path-to-src/bin> -DTEMPLATE_DIR=<path-to-src/config> -P PrepareAssets.cmake
#
# src/bin holds the released game data and is excluded from git. A clean
# checkout only needs the directory, an empty Data/ tree and the tracked
# config.ini.template for configure + build + unit tests to run. Real game
# assets must be unpacked separately for a playable client.

if(NOT ASSETS_DIR OR NOT TEMPLATE_DIR)
  message(FATAL_ERROR "ASSETS_DIR and TEMPLATE_DIR must be provided")
endif()

file(MAKE_DIRECTORY "${ASSETS_DIR}/Data")

set(tracked_template "${TEMPLATE_DIR}/config.ini.template")
set(asset_template "${ASSETS_DIR}/config.ini.template")

if(NOT EXISTS "${tracked_template}")
  message(FATAL_ERROR "Tracked template missing: ${tracked_template}")
endif()

# Never overwrite a restored released-asset copy.
if(NOT EXISTS "${asset_template}")
  file(COPY_FILE "${tracked_template}" "${asset_template}")
  message(STATUS "Seeded ${asset_template} from the tracked template")
else()
  message(STATUS "Keeping existing ${asset_template}")
endif()
