namespace PomodoroTimer;

public enum Phase
{
    Idle,
    Work,
    Rest
}

public sealed record PhaseSnapshot(
    Phase Phase,
    int RemainingSeconds,
    bool IsOvertime,
    int OvertimeSeconds,
    bool JustFiredWarn,
    bool JustFiredEnd);

/// <summary>
/// 番茄钟核心状态机（纯逻辑，无 UI）。
/// 工作与休息阶段交替；到时不自动切换，进入超时正计时，仅当用户主动结束时才切换到下一阶段。
/// </summary>
public sealed class PomodoroEngine
{
    private readonly Func<long> _clock;
    private long _phaseStartMs;
    private bool _warnFired;
    private bool _endFired;

    public PomodoroEngine(Func<long>? clock = null)
    {
        _clock = clock ?? (() => Environment.TickCount64);
    }

    public Phase CurrentPhase { get; private set; } = Phase.Idle;

    public int WorkSeconds { get; set; } = 25 * 60;

    public int RestSeconds { get; set; } = 5 * 60;

    public bool IsRunning => CurrentPhase != Phase.Idle;

    /// <summary>从空闲进入工作阶段。</summary>
    public void Start()
    {
        if (IsRunning)
        {
            return;
        }
        BeginPhase(Phase.Work);
    }

    /// <summary>结束当前阶段，自动开始下一阶段（工作↔休息）。</summary>
    public void EndCurrentPhase()
    {
        if (!IsRunning)
        {
            return;
        }
        BeginPhase(CurrentPhase == Phase.Work ? Phase.Rest : Phase.Work);
    }

    /// <summary>回到空闲状态。</summary>
    public void Reset()
    {
        CurrentPhase = Phase.Idle;
        _warnFired = false;
        _endFired = false;
    }

    /// <summary>
    /// 根据墙钟计算当前状态。elapsed 由时钟差值得出，与轮询频率无关，保证精度。
    /// </summary>
    public PhaseSnapshot Poll()
    {
        if (!IsRunning)
        {
            return new PhaseSnapshot(Phase.Idle, 0, false, 0, false, false);
        }

        long elapsedMs = _clock() - _phaseStartMs;
        int elapsed = (int)(elapsedMs / 1000);
        int target = CurrentPhase == Phase.Work ? WorkSeconds : RestSeconds;
        int remaining = target - elapsed;

        bool warn = false;
        bool end = false;

        // 前 1 分钟提醒：仅在阶段时长超过 1 分钟时触发，且只触发一次
        if (!_warnFired && target > 60 && remaining <= 60 && remaining > 0)
        {
            _warnFired = true;
            warn = true;
        }

        // 到时间提醒：只触发一次；之后进入超时正计时
        if (!_endFired && remaining <= 0)
        {
            _endFired = true;
            end = true;
        }

        bool overtime = elapsed >= target;
        int overtimeSeconds = overtime ? elapsed - target : 0;
        int displayRemaining = Math.Max(0, remaining);

        return new PhaseSnapshot(CurrentPhase, displayRemaining, overtime, overtimeSeconds, warn, end);
    }

    private void BeginPhase(Phase phase)
    {
        CurrentPhase = phase;
        _phaseStartMs = _clock();
        _warnFired = false;
        _endFired = false;
    }
}
