using System.Collections.Generic;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class CompanionPresetDriftTests
{
    // PresetKeys and the TryPreset switch are two hand-kept lists. The existing test names six keys by hand,
    // so a seventh key added to the array alone would pass it. This one walks the array itself.
    [Fact]
    public void Every_listed_preset_key_resolves_to_a_label_and_a_species()
    {
        var labels = new HashSet<string>();
        foreach (var key in Companions.PresetKeys)
        {
            string label;
            CompanionSpecies species;
            Assert.True(Companions.TryPreset(key, out label, out species), "no TryPreset case for '" + key + "'");
            Assert.False(string.IsNullOrEmpty(label));
            Assert.True(labels.Add(label), "duplicate label '" + label + "'");
        }
        string none;
        CompanionSpecies unused;
        Assert.False(Companions.TryPreset("not-a-preset", out none, out unused));
    }
}
