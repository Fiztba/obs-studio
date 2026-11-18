#include <stdarg.h>
#include <stddef.h>
#include <setjmp.h>
#include <cmocka.h>

#include <util/array-serializer.h>

static void serialize_test(void **state)
{
	UNUSED_PARAMETER(state);
	struct array_output_data output;
	struct serializer s;

	array_output_serializer_init(&s, &output);

	s_w8(&s, 0x01);
	s_w8(&s, 0xff);
	s_w8(&s, 0xe1);

	assert_int_equal(output.bytes.num, 3);
	uint8_t expected[3] = {0x01, 0xff, 0xe1};
	assert_memory_equal(output.bytes.array, expected, 3);

	assert_true(serializer_get_pos(&s) == 3);

	array_output_serializer_free(&output);
}

static void array_seek_position_test(void **state)
{
	UNUSED_PARAMETER(state);
	struct array_output_data output;
	struct serializer s;
	array_output_serializer_init(&s, &output);

	s_wb32(&s, 0x01020304);
	assert_int_equal(serializer_seek(&s, 1, SERIALIZE_SEEK_START), 1);
	assert_int_equal(serializer_get_pos(&s), 1);

	s_w8(&s, 0xff);
	assert_int_equal(serializer_get_pos(&s), 2);
	assert_int_equal(output.bytes.num, 4);
	const uint8_t expected[] = {0x01, 0xff, 0x03, 0x04};
	assert_memory_equal(output.bytes.array, expected, sizeof(expected));

	array_output_serializer_free(&output);
}

int main()
{
	const struct CMUnitTest tests[] = {
		cmocka_unit_test(serialize_test),
		cmocka_unit_test(array_seek_position_test),
	};

	return cmocka_run_group_tests(tests, NULL, NULL);
}
