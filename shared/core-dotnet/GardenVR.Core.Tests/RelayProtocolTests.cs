using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

// Upgrade plan C9: the wire format between the app and the coach relay.
public class RelayProtocolTests
{
    [Fact]
    public void A_request_is_encoded_with_its_kind_prompts_and_cap()
    {
        CoachRequest r = CoachPrompts.Reflection(CoachVoice.Terrarium, "a \"quiet\" day", null);
        JsonObject obj = Json.ParseObject(RelayProtocol.Encode(r));
        Assert.Equal("Reflection", obj.Get("kind").AsString());
        Assert.Equal(r.System, obj.Get("system").AsString());
        Assert.Equal(r.User, obj.Get("user").AsString());
        Assert.Equal(r.MaxTokens, obj.Get("maxTokens").AsInt());
    }

    [Theory]
    [InlineData(200, "{\"ok\":true,\"text\":\"A calm day.\"}", true, CoachFailure.None)]
    [InlineData(200, "{\"ok\":false,\"failure\":\"Refused\"}", false, CoachFailure.Refused)]
    [InlineData(200, "{\"ok\":false,\"failure\":\"Error\"}", false, CoachFailure.Error)]
    [InlineData(429, "{\"ok\":false,\"failure\":\"RateLimited\"}", false, CoachFailure.Error)]
    [InlineData(400, "{\"ok\":false,\"failure\":\"Rejected\"}", false, CoachFailure.Error)]
    [InlineData(500, "<html>", false, CoachFailure.Error)]
    [InlineData(502, "", false, CoachFailure.Error)]
    [InlineData(200, "{\"ok\":true}", false, CoachFailure.Error)]
    [InlineData(404, "{\"ok\":true,\"text\":\"x\"}", false, CoachFailure.Error)]
    public void Replies_decode_to_success_refusal_or_error(int status, string body, bool ok, CoachFailure failure)
    {
        CoachReply reply = RelayProtocol.Decode(status, body);
        Assert.Equal(ok, reply.Ok);
        Assert.Equal(failure, reply.Failure);
    }

    [Fact]
    public void Install_keys_are_random_hex_and_the_relay_pattern_accepts_them()
    {
        string a = RelayProtocol.NewInstallKey(), b = RelayProtocol.NewInstallKey();
        Assert.NotEqual(a, b);
        Assert.Matches("^[A-Za-z0-9-]{8,64}$", a);
    }

    [Fact]
    public void One_system_prompt_per_voice_is_allowed_and_every_request_uses_one()
    {
        var systems = RelayProtocol.AllowedSystems();
        Assert.Equal(2, systems.Count);
        foreach (CoachVoice v in new[] { CoachVoice.Terrarium, CoachVoice.Sundial })
        {
            Assert.Contains(CoachPrompts.Onboarding(v, new[] { "a" }).System, systems);
            Assert.Contains(CoachPrompts.Reflection(v, "b", new ReflectionContext { Weekday = "Monday" }).System, systems);
        }
    }
}
