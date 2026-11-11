// <copyright file="SkillHitValidator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.Network;

/// <summary>
/// Validator for skill hits, which were done by skills with type <see cref="SkillType.AreaSkillExplicitHits"/>.
/// </summary>
public class SkillHitValidator
{
    private const int MaximumCounterValue = 0x32;

    private const byte TwisterSkillId = 8;
    private const byte EvilSpiritSkillId = 9;
    private const byte MultishotSkillId = 235;

    private static readonly TimeSpan MaxAnimationToHitDelay = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger;

    /// <summary>
    /// The counter which keeps the expected count of the next animation and hit.
    /// </summary>
    private readonly Counter _counter = new(1, MaximumCounterValue);

    private readonly HitEntry[] _hits = new HitEntry[MaximumCounterValue + 1];

    private byte _lastTwisterIndex;

    private byte _lastMultishotIndex;

    /// <summary>
    /// The game client doesn't reset its counter when it connects with a new account.
    /// So, the simplest way for us is, to accept the first counter we get with
    /// an animation.
    /// </summary>
    private bool _isFirstAfterConnectionEstablished = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillHitValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public SkillHitValidator(ILogger logger)
    {
        this._logger = logger;
        this._counter.Reset();
    }

    /// <summary>
    /// Gets the last registered skill identifier.
    /// </summary>
    public ushort LastRegisteredSkillId { get; private set; }

    /// <summary>
    /// Tries to register an animation with the specified counter.
    /// </summary>
    /// <param name="skillId">The skill identifier.</param>
    /// <param name="animationCounter">The animation counter.</param>
    /// <returns>
    /// True, if successful.
    /// </returns>
    /// <remarks>Basically, only skills with overlapping animations and hits send an animation counter greater than 0.</remarks>
    public bool TryRegisterAnimation(ushort skillId, byte animationCounter)
    {
        // The _hits buffer is only indexed 0..MaximumCounterValue (inclusive). The counter value
        // comes from the client as a raw byte (0..255) and on the first animation it is copied
        // verbatim into _counter. A value above the maximum would throw IndexOutOfRangeException
        // at the array access below, so reject it up front with a runtime branch (not a Debug.Assert,
        // which is compiled out in Release).
        if (animationCounter > MaximumCounterValue)
        {
            this._logger.LogWarning($"Animation counter out of range: {animationCounter} (max {MaximumCounterValue}); rejecting.");
            return false;
        }
        if (skillId == TwisterSkillId)
        {
            // Twister is a implemented wrong at the client side. It sends a counter of the animations here, but not in the hit packets.
            this._lastTwisterIndex = animationCounter;
        }

        if (skillId == MultishotSkillId)
        {
            // Multishot is a implemented wrong at the client side. It sends a counter of the animations here, but not in the hit packets.
            this._lastMultishotIndex = animationCounter;
        }

        if (skillId == EvilSpiritSkillId
            || skillId == TwisterSkillId
            || animationCounter > 0)
        {
            if (this._isFirstAfterConnectionEstablished)
            {
                this._counter.Count = animationCounter;
                this._isFirstAfterConnectionEstablished = false;
            }

            if (this._counter.Count != animationCounter)
            {
                this._logger.LogWarning($"Animation count out of sync - hacker? Expected: {this._counter.Count}, Actual: {animationCounter}.");
                return false;
            }

            this._counter.Increase();
        }

        this.LastRegisteredSkillId = skillId;
        this._hits[animationCounter] = new HitEntry(skillId, DateTime.UtcNow, true, 0);
        return true;
    }

    /// <summary>
    /// Increases the counter by one, after a hit was done.
    /// It must just be called once after a combined hit.
    /// </summary>
    public void IncreaseCounterAfterHit()
    {
        this._counter.Increase();
    }

    /// <summary>
    /// Determines whether a skill hit request is valid with respect to previous animation records and anti-cheat timing invariants.
    /// 根据历史动画注册记录与反作弊时序契约，判定技能伤害命中请求是否合法。
    /// </summary>
    /// <param name="skillId">The skill identifier being executed / 施展的技能标识符.</param>
    /// <param name="animationCounter">The animation counter provided by the client / 客户端传入的动画序列计数器.</param>
    /// <param name="hitCounter">The hit counter provided by the client / 客户端传入的伤害命中计数器.</param>
    /// <returns>
    /// A tuple where:
    /// - IsValid: <c>true</c> if the hit satisfies all security and synchronization checks; otherwise, <c>false</c>.
    /// - IncreaseCounter: <c>true</c> if internal sequence counters should advance to stay in sync with the client.
    /// 返回元组：IsValid 表示该次命中判定是否合法生效；IncreaseCounter 表示是否推进内部期望计数器以保持同步。
    /// </returns>
    /// <remarks>
    /// Multi-Hit Anti-Cheat Contract (GL-10 / R2-GL02) / 多段技能反作弊时序契约:
    /// 1. Client Quirk Compensation: Twister and Multishot omit animation counters in hit packets; falls back to cached indices.
    ///    协议缺陷补偿：针对客户端在旋风斩 (Twister) 与多重箭 (Multishot) 命中包中丢失动画序号的历史缺陷，采用缓存的最近动画索引补偿。
    /// 2. Array Boundary Guard: Strict bounds check (<c>MaximumCounterValue</c> = 0x32) prevents untrusted client integers from triggering <see cref="IndexOutOfRangeException"/>.
    ///    数组越界防护：对客户端上报的计数值实施严格上界裁定，防止恶意构造的数据包引发索引越界异常。
    /// 3. Animation Precedence: Rejects hits if no matching registered animation exists or if the registered skill ID mismatches.
    ///    前置动画验证：严禁无前置动画发起的凭空命中，严格比对动画记录与当前命中技能标识符的一致性。
    /// 4. Time Window Clamp: Enforces <see cref="MaxAnimationToHitDelay"/> (10s) window to block replayed or stale hits.
    ///    时效窗口校验：命中与前置动画的时间差超过 10 秒即视作非法重放或挂起作弊，拒绝该次伤害。
    /// </remarks>
    public (bool IsValid, bool IncreaseCounter) IsHitValid(ushort skillId, byte animationCounter, byte hitCounter)
    {
        if (animationCounter == 0 && skillId == TwisterSkillId)
        {
            // Twister is implemented wrong at the client side. It doesn't send an animation counter in hit packets.
            animationCounter = this._lastTwisterIndex;
        }

        if (animationCounter == 0 && skillId == MultishotSkillId)
        {
            // Multishot is implemented wrong at the client side. It doesn't send an animation counter in hit packets.
            animationCounter = this._lastMultishotIndex;
        }

        // After the twister/multishot fallback above, animationCounter may still exceed the
        // buffered range (it originated from an untrusted client byte). Guard the array access
        // with a runtime check so a malicious counter can't trigger IndexOutOfRangeException.
        if (animationCounter > MaximumCounterValue)
        {
            this._logger.LogWarning($"Hit animation counter out of range: {animationCounter} (max {MaximumCounterValue}).");
            return (false, false);
        }

        if (hitCounter > MaximumCounterValue)
        {
            this._logger.LogWarning($"Hit counter out of range: {hitCounter} (max {MaximumCounterValue}).");
            return (false, false);
        }

        var animationEntry = this._hits[animationCounter];
        if (!animationEntry.IsAnimation)
        {
            this._logger.LogWarning("Possible Hacker - Skill Hit Invalid because of missing previous animation.");
            return (false, false);
        }

        if (animationEntry.Skill != skillId)
        {
            this._logger.LogWarning($"Wrong skill in referenced animation - hacker? Expected skill: {animationEntry.Skill}, actual: {skillId}.");
            return (false, false);
        }

        var expectedCount = this._counter.Count;
        if (expectedCount != hitCounter)
        {
            this._logger.LogWarning($"Hit count out of sync - hacker? Expected: {this._counter.Count}, Actual: {hitCounter}.");
            return (false, false);
        }

        var now = DateTime.UtcNow;
        var timestampDiff = now - animationEntry.TimeStamp;
        if (timestampDiff > MaxAnimationToHitDelay)
        {
            this._logger.LogWarning("Possible Hacker - Skill Hit Invalid because of too high time difference between animation and hit.");
            return (false, true);
        }

        var currentHitEntry = this._hits[hitCounter];
        if (!currentHitEntry.IsAnimation && currentHitEntry.TimeStamp != default && currentHitEntry.TimeStamp >= now.AddSeconds(-1))
        {
            // probably speed too high to allow any check
            this._hits[animationCounter] = animationEntry with
            {
                HitCount = animationEntry.HitCount + 1,
            };
            this._logger.LogDebug($"Hits are too fast, skipping validity check.");
            this._hits[hitCounter] = new HitEntry(skillId, now, false, 0);
            return (true, true);
        }

        var newCount = animationEntry.HitCount + 1;
        this._hits[animationCounter] = animationEntry with { HitCount = newCount };
        this._hits[hitCounter] = new HitEntry(skillId, now, false, 0);

        return (true, true);
    }

    private record struct HitEntry(ushort Skill, DateTime TimeStamp, bool IsAnimation, int HitCount);
}