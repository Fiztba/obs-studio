#include "slideshow-random.h"

#include <util/platform.h>

#include <random>

size_t random_slide_index(size_t count, size_t previous)
{
	if (count < 2) {
		return 0;
	}

	static thread_local std::mt19937_64 engine{os_gettime_ns()};
	const size_t last = previous < count ? count - 2 : count - 1;
	const size_t index = std::uniform_int_distribution<size_t>{0, last}(engine);
	return index >= previous ? index + 1 : index;
}
