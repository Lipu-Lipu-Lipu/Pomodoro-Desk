using System.Runtime.InteropServices;
using System.Text;

namespace PomodoroTimer;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    private const int AttachParentProcess = -1;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--selftest"))
        {
            return RunSelfTest();
        }

        ApplicationConfiguration.Initialize();
        using var form = new MainForm();
        Application.Run(form);
        return 0;
    }

    /// <summary>
    /// 使用模拟时钟对状态机做自动化自测，通过 exit code 反映结果（0 = 全部通过）。
    /// </summary>
    private static int RunSelfTest()
    {
        _ = AttachConsole(AttachParentProcess);

        long now = 1_000_000;
        var engine = new PomodoroEngine(() => now) { WorkSeconds = 120, RestSeconds = 60 };
        int failures = 0;

        void Check(bool condition, string name)
        {
            Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
            if (!condition)
            {
                failures++;
            }
        }

        engine.Start();
        Check(engine.CurrentPhase == Phase.Work, "Start enters Work");

        PhaseSnapshot s = engine.Poll();
        Check(!s.JustFiredWarn && !s.JustFiredEnd && s.RemainingSeconds == 120 && !s.IsOvertime,
            "Initial snapshot is 120s remaining");

        now += 60_000;
        s = engine.Poll();
        Check(s.JustFiredWarn && !s.JustFiredEnd, "Warn fires once at T-60s");
        s = engine.Poll();
        Check(!s.JustFiredWarn && !s.JustFiredEnd, "Warn does not fire twice");

        now += 59_000;
        s = engine.Poll();
        Check(!s.JustFiredEnd && !s.IsOvertime, "Not ended at T-1s");

        now += 1_000;
        s = engine.Poll();
        Check(s.JustFiredEnd && s.IsOvertime && s.OvertimeSeconds == 0, "End fires at T-0 and enters overtime");

        now += 90_000;
        s = engine.Poll();
        Check(s.IsOvertime && s.OvertimeSeconds == 90 && !s.JustFiredEnd, "Overtime counts up to +90s");

        engine.EndCurrentPhase();
        Check(engine.CurrentPhase == Phase.Rest, "End switches to Rest");
        s = engine.Poll();
        Check(!s.JustFiredWarn && !s.JustFiredEnd && !s.IsOvertime && s.RemainingSeconds == 60,
            "Rest auto-starts with flags reset");

        now += 60_000;
        s = engine.Poll();
        Check(!s.JustFiredWarn && s.JustFiredEnd, "No warn for 60s rest phase; end fires");

        engine.EndCurrentPhase();
        Check(engine.CurrentPhase == Phase.Work, "Switch back to Work");

        engine.Reset();
        Check(engine.CurrentPhase == Phase.Idle && !engine.IsRunning, "Reset returns to Idle");
        s = engine.Poll();
        Check(s.Phase == Phase.Idle, "Idle snapshot");

        engine.WorkSeconds = 30;
        engine.Start();
        now += 29_000;
        s = engine.Poll();
        Check(!s.JustFiredWarn && !s.JustFiredEnd, "No warn for 30s phase before end");
        now += 1_000;
        s = engine.Poll();
        Check(!s.JustFiredWarn && s.JustFiredEnd && s.IsOvertime, "30s phase ends without warn");

        byte[] startWav = BeepPlayer.StartWav.Value;
        byte[] warnWav = BeepPlayer.WarnWav.Value;
        byte[] endWav = BeepPlayer.EndWav.Value;

        Check(WavHeaderOk(startWav) && WavHeaderOk(warnWav) && WavHeaderOk(endWav),
            "WAV headers valid (RIFF/WAVE/data)");
        Check(WavPeakRatio(warnWav) >= 0.45 && WavPeakRatio(endWav) >= 0.45,
            "Warn/End WAV peak >= 45% full scale");
        int warnMs = WavDurationMs(warnWav);
        Check(warnMs is >= 200 and <= 900, $"Warn WAV duration {warnMs}ms in [200,900]");
        int endMs = WavDurationMs(endWav);
        Check(endMs is >= 2500 and <= 4500, $"End WAV duration {endMs}ms in [2500,4500]");
        int startMs = WavDurationMs(startWav);
        Check(startMs is >= 150 and <= 600, $"Start WAV duration {startMs}ms in [150,600]");

        Check(MainForm.TotalSecondsOf(2, 30) == 150 && MainForm.TotalSecondsOf(0, 45) == 45,
            "TotalSecondsOf converts minutes+seconds");
        Check(MainForm.TotalSecondsOf(0, 0) == 1, "TotalSecondsOf clamps to 1 second minimum");

        AppSettings clamped = SettingsStore.Normalize(new AppSettings(200, 75, -3, 99));
        Check(clamped == new AppSettings(120, 59, 0, 59), "Settings Normalize clamps to control ranges");
        string tmpSettingsPath = Path.Combine(Path.GetTempPath(), "pomodoro_selftest_settings.json");
        AppSettings sampleSettings = new(30, 15, 10, 45);
        SettingsStore.Save(tmpSettingsPath, sampleSettings);
        Check(SettingsStore.Load(tmpSettingsPath) == sampleSettings, "Settings save/load round-trip");
        try
        {
            File.Delete(tmpSettingsPath);
        }
        catch
        {
        }

        Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURES");
        return failures == 0 ? 0 : 1;
    }

    private static bool WavHeaderOk(byte[] wav)
    {
        return wav.Length > 44
            && Encoding.ASCII.GetString(wav, 0, 4) == "RIFF"
            && Encoding.ASCII.GetString(wav, 8, 4) == "WAVE"
            && Encoding.ASCII.GetString(wav, 36, 4) == "data";
    }

    private static int WavDurationMs(byte[] wav)
    {
        return (wav.Length - 44) * 1000 / (44100 * 2);
    }

    private static double WavPeakRatio(byte[] wav)
    {
        double peak = 0;
        for (int i = 44; i + 1 < wav.Length; i += 2)
        {
            double a = Math.Abs(BitConverter.ToInt16(wav, i) / (double)short.MaxValue);
            if (a > peak)
            {
                peak = a;
            }
        }
        return peak;
    }
}
