using System;
using System.IO;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

namespace GardenVR.SundialModel.Tests;

public class OnboardingSaveTests
{
    [Fact] public void Acceptance_and_packet_decision_reload_together_without_duplicate_admission()
    {
        string dir = Path.Combine(Path.GetTempPath(), "interview-" + Guid.NewGuid().ToString("N"));
        try
        {
            var clock = new FixedClock(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
            var service = new SundialService(clock, dir);
            service.Save.Interview = new OnboardingDraft { Proposals = ScriptedCoach.Onboarding(null) };
            service.SetFirstRunStep(Onboarding.Review);
            Assert.Null(service.AcceptInterview(0, "invalid"));
            Assert.True(service.Save.Interview.Pending(0));
            var habit = service.AcceptInterview(0, "midday");
            Assert.NotNull(habit);
            var reloaded = new SundialService(clock, dir);
            Assert.Equal(Onboarding.Review, reloaded.Save.FirstRunStep);
            Assert.Equal(habit.Id, Assert.Single(reloaded.Save.Habits).Id);
            Assert.False(reloaded.Save.Interview.Pending(0));
            Assert.Null(reloaded.AcceptInterview(0, "midday"));
            Assert.Single(reloaded.Save.Habits);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
