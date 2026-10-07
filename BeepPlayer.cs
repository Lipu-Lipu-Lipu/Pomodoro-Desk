using System.IO;
using System.Media;
using System.Text;

namespace PomodoroTimer;

/// <summary>
/// 提示音播放器：在内存中生成带泛音的 WAV（无资源文件），通过 SoundPlayer 播放。
/// 基音叠加 2/3 次泛音并做峰值归一化，比纯正弦在笔记本扬声器上响得多。
/// </summary>
public static class BeepPlayer
{
    /// <summary>开始/切换阶段：轻快上行双音。</summary>
    internal static readonly Lazy<byte[]> StartWav =
        new(() => BuildWav(0.30, (523, 90, 40), (784, 150, 0)));

    /// <summary>提前 1 分钟提醒：两声清脆短音。</summary>
    internal static readonly Lazy<byte[]> WarnWav =
        new(() => BuildWav(0.55, (988, 180, 130), (988, 180, 0)));

    /// <summary>到时提醒：门铃三连 × 3 轮，约 3.3 秒。</summary>
    internal static readonly Lazy<byte[]> EndWav =
        new(() => BuildWav(
            0.62,
            (880, 150, 60), (1175, 180, 60), (1568, 420, 350),
            (880, 150, 60), (1175, 180, 60), (1568, 420, 350),
            (880, 150, 60), (1175, 180, 60), (1568, 420, 0)));

    public static void PlayStart()
    {
        Play(StartWav.Value, fallbackFreq: 700);
    }

    public static void PlayWarn()
    {
        Play(WarnWav.Value, fallbackFreq: 900);
    }

    public static void PlayEnd()
    {
        Play(EndWav.Value, fallbackFreq: 900);
    }

    /// <summary>生成 16-bit 单声道 PCM WAV；每个音可带独立的后置静音间隔，带 10ms 淡入淡出防爆音。</summary>
    internal static byte[] BuildWav(double volume, params (double Freq, int Ms, int GapAfterMs)[] tones)
    {
        const int rate = 44100;
        const int fadeMs = 10;

        var raw = new List<double>();
        foreach ((double freq, int ms, int gapMs) in tones)
        {
            int count = rate * ms / 1000;
            int fade = rate * fadeMs / 1000;
            for (int i = 0; i < count; i++)
            {
                double env = 1.0;
                if (i < fade)
                {
                    env = (double)i / fade;
                }
                else if (i > count - fade)
                {
                    env = (double)(count - i) / fade;
                }

                double t = (double)i / rate;
                double wave =
                    Math.Sin(2.0 * Math.PI * freq * t) +
                    0.40 * Math.Sin(4.0 * Math.PI * freq * t) +
                    0.18 * Math.Sin(6.0 * Math.PI * freq * t);
                raw.Add(wave * env);
            }

            int gapCount = rate * gapMs / 1000;
            for (int i = 0; i < gapCount; i++)
            {
                raw.Add(0);
            }
        }

        double peak = 0;
        for (int i = 0; i < raw.Count; i++)
        {
            double a = Math.Abs(raw[i]);
            if (a > peak)
            {
                peak = a;
            }
        }
        if (peak < 1e-9)
        {
            peak = 1;
        }

        var samples = new short[raw.Count];
        for (int i = 0; i < raw.Count; i++)
        {
            samples[i] = (short)Math.Round(raw[i] / peak * volume * short.MaxValue);
        }

        int dataSize = samples.Length * 2;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);            // fmt 块大小
        writer.Write((short)1);      // PCM
        writer.Write((short)1);      // 单声道
        writer.Write(rate);          // 采样率
        writer.Write(rate * 2);      // 字节率 = 采样率 * 块对齐
        writer.Write((short)2);      // 块对齐 = 声道 * 位数/8
        writer.Write((short)16);     // 位深
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        foreach (short s in samples)
        {
            writer.Write(s);
        }
        writer.Flush();

        return stream.ToArray();
    }

    private static void Play(byte[] wav, int fallbackFreq)
    {
        try
        {
            var stream = new MemoryStream(wav);
            var player = new SoundPlayer(stream);
            Task.Run(() =>
            {
                try
                {
                    player.PlaySync();
                }
                catch
                {
                    TryConsoleBeep(fallbackFreq);
                }
                finally
                {
                    player.Dispose();
                    stream.Dispose();
                }
            });
        }
        catch
        {
            TryConsoleBeep(fallbackFreq);
        }
    }

    private static void TryConsoleBeep(int freq)
    {
        try
        {
            Console.Beep(freq, 250);
        }
        catch
        {
            // 无音频环境时静默失败，仍有气泡通知与置顶卡片兜底
        }
    }
}
