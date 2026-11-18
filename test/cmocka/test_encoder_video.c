#include <stdarg.h>
#include <stddef.h>
#include <setjmp.h>
#include <stdlib.h>
#include <cmocka.h>

#include <obs.h>

static const char *encoder_name(void *data)
{
	UNUSED_PARAMETER(data);
	return "Test video encoder";
}

static void *encoder_create(obs_data_t *settings, obs_encoder_t *encoder)
{
	UNUSED_PARAMETER(settings);
	return encoder;
}

static void encoder_destroy(void *data)
{
	UNUSED_PARAMETER(data);
}

static bool encoder_encode(void *data, struct encoder_frame *frame, struct encoder_packet *packet, bool *received)
{
	UNUSED_PARAMETER(data);
	UNUSED_PARAMETER(frame);
	UNUSED_PARAMETER(packet);
	*received = false;
	return true;
}

static void encoder_frame_rate_test(void **state)
{
	UNUSED_PARAMETER(state);
#if defined(__unix__) && !defined(__APPLE__)
	/* The default Unix hotkey backend opens an X11 display during obs_startup. */
	if (!getenv("DISPLAY"))
		skip();
#endif
	const struct obs_encoder_info test_encoder = {
		.id = "test-video",
		.type = OBS_ENCODER_VIDEO,
		.codec = "test",
		.get_name = encoder_name,
		.create = encoder_create,
		.destroy = encoder_destroy,
		.encode = encoder_encode,
	};
	struct video_output_info info = {
		.name = "test",
		.format = VIDEO_FORMAT_NV12,
		.width = 16,
		.height = 16,
		.fps_num = 60000,
		.fps_den = 1001,
		.cache_size = 1,
	};
	assert_true(obs_startup("en-US", NULL, NULL));
	obs_register_encoder(&test_encoder);
	video_t *video = NULL;
	assert_int_equal(video_output_open(&video, &info), VIDEO_OUTPUT_SUCCESS);
	obs_encoder_t *encoder = obs_video_encoder_create(test_encoder.id, "test", NULL, NULL);
	assert_non_null(encoder);
	obs_encoder_set_video(encoder, video);
	assert_true(obs_encoder_set_frame_rate_divisor(encoder, 3));

	video_t *divided = obs_encoder_video(encoder);
	assert_non_null(divided);
	assert_int_equal(video_output_get_info(divided)->fps_den, 3003);
	assert_double_equal(video_output_get_frame_rate(divided), 60000.0 / 3003.0, 1e-9);
	assert_int_equal(video_output_get_frame_time(divided), 50050000);
	assert_double_equal(video_output_get_frame_rate(video), 60000.0 / 1001.0, 1e-9);
	assert_int_equal(video_output_get_frame_time(video), 16683333);

	obs_encoder_release(encoder);
	video_output_close(video);
	obs_shutdown();
}

int main(void)
{
	const struct CMUnitTest tests[] = {cmocka_unit_test(encoder_frame_rate_test)};
	return cmocka_run_group_tests(tests, NULL, NULL);
}
