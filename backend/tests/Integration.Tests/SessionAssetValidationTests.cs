using Academy.Application.Programs;
using Academy.Domain.Enums;
using Academy.Infrastructure.Learning;
using Academy.Infrastructure.Programs;

namespace Academy.Integration.Tests;

/// <summary>
/// The admin form used to default new video sessions to the asset id "sample". Under Bunny that is
/// not a video: the session saves fine and every learner gets a 403. Bunny video ids are GUIDs, so
/// under Bunny anything else is refused at save time, where an admin can still see the mistake.
///
/// Pure tests: the rule is a static function. Under the dev provider — which the integration
/// suite runs — it is a no-op by design, so seeding and tests keep using "sample".
/// </summary>
public class SessionAssetValidationTests
{
    private static readonly VideoOptions Dev = new() { Provider = "dev" };
    private static readonly VideoOptions Bunny = new() { Provider = "bunny" };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sample")]
    [InlineData("not-a-guid")]
    [InlineData("{3f2b8c1e-9a4d-4e6b-8c7a-1d2e3f4a5b6c}")]
    [InlineData("3f2b8c1e9a4d4e6b8c7a1d2e3f4a5b6c")]
    public void Under_bunny_a_video_session_without_a_real_video_id_is_refused(string? asset)
    {
        var e = Assert.Throws<ProgramException>(
            () => ProgramAdminService.ValidateVideoAsset(SessionType.Video, asset, Bunny));

        Assert.Equal(400, e.StatusCode);
    }

    [Fact]
    public void Under_bunny_a_video_session_with_a_bunny_id_is_accepted()
    {
        ProgramAdminService.ValidateVideoAsset(SessionType.Video, Guid.NewGuid().ToString(), Bunny);
    }

    [Fact]
    public void Under_dev_the_placeholder_is_still_accepted()
    {
        // The seeder and the integration suite create video sessions with "sample".
        ProgramAdminService.ValidateVideoAsset(SessionType.Video, "sample", Dev);
        ProgramAdminService.ValidateVideoAsset(SessionType.Video, null, Dev);
    }

    [Theory]
    [InlineData(SessionType.Live)]
    [InlineData(SessionType.FinalAssessment)]
    public void Sessions_that_are_not_videos_need_no_video_id(SessionType type)
    {
        ProgramAdminService.ValidateVideoAsset(type, null, Bunny);
    }
}
