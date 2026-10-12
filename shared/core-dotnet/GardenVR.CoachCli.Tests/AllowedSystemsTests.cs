using System.IO;
using System.Runtime.CompilerServices;
using GardenVR.CoachCli;
using Xunit;

namespace GardenVR.CoachCli.Tests;

// The relay accepts only the system prompts core builds. Its allowed-systems.json is written from core; this test fails
// when a prompt change in core was not followed by: dotnet run --project shared/core-dotnet/GardenVR.CoachCli -- systems tools/relay/allowed-systems.json
public class AllowedSystemsTests
{
    static string Here([CallerFilePath] string path = "") { return Path.GetDirectoryName(path); }

    [Fact]
    public void The_relay_file_matches_core()
    {
        string file = Path.Combine(Here(), "..", "..", "..", "tools", "relay", "allowed-systems.json");
        Assert.Equal(Program.SystemsJson().Replace("\r\n", "\n"), File.ReadAllText(file).Replace("\r\n", "\n"));
    }
}
