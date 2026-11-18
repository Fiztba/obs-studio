#pragma once

#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Pass count as the previous index when picking the first slide. */
size_t random_slide_index(size_t count, size_t previous);

#ifdef __cplusplus
}
#endif
