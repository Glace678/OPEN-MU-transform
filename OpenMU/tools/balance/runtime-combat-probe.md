# Runtime Combat Calibration Probe

Historical pre-monster-budget probe: see `runtime-normal-monster-budget.md`
and `runtime-combat-measurements.v1.monster-budget.json` for the current opt-in
normal-route HP/physical-source correction. The old results below remain as
the before baseline and the client-cadence limits still apply.

Status: explicit full-S6 pipeline probe completed twice; no monster or character budget installed.
This probe uses the current opt-in balance-v1 profile with the normal-route rank adapter.
Those two measurements are the preserved pre-AP baseline. The current harness also measures
the four reviewed routine AP overrides; see runtime-skill-resources.md for exact scope.

## Existing Attribute Boundary

- All 18 Season 6 character stages retain their original StatAttributes, base values and
  attribute relationships. Only the already installed five-points-per-level rule is applied.
- Original equipment power-up networks, skill damage/consume requirements, HP/MP/AP/SD
  growth and interval regeneration remain active. Candidate class/gear/power formulas are not active.
  The reviewed 43/52/55/262 AP consumption helper is the explicit exception; it does not
  mutate the original definitions or MP requirements. RF265 is separate tactical burst.
- Accuracy and armor use the installed balance-v1 formulas. Subsequent original damage
  modifiers, skill multipliers, multi-hit semantics and Soul Barrier are not replaced.
- Candidate player action intervals, boss telegraphs and in-combat regeneration are not active.
- The 135 normal bindings contain four candidate boss IDs but none of the eight candidate
  elite IDs. Elite measurements must be identified as unbound event-definition references,
  not validated normal-route encounters.

## Inputs And Scenarios

- Complete Season 6 in-memory initialization, no database or network listener.
- Character stages: all 18, at levels 1, 11, 50, 51, 100, 200, 300 and 400, master level zero.
- Five earned stat points per level; use each candidate lineage's first build allocation only.
  Allocation weights are inputs, not installed player-growth formulas. Initial stats remain original.
- Low-level evolved stages are structural probes only. Master stages below 400 and second
  stages below 150 are not legal progression samples. Advanced character creation unlocks
  and quest completion are not proven by constructing the character in memory.
- Real common-drop +0..3 or merchant gear, no excellent/ancient/socket/luck buffs, wings,
  pets or paid equipment. The generator uses its actual skill-option roll for weapon skills.
  An item must have an actual merchant listing or a legal drop binding at/below the tested route,
  and must pass Player.CompliesRequirements and the item's class/slot restrictions.
- A learned skill must have a reachable scroll/orb and satisfy actual item/class/stat requirements,
  or come from an equipped skill-bearing weapon. No free master skill or pet skill.
- Actual CalculateDamageAsync samples for both directions; real TryConsumeForSkillAsync
  and recovery-handler deltas. NPC AI/network views/optional feature plug-ins are outside the probe.
- Enemy attack cadence is MonsterDefinition.AttackDelay. Player action cadence is a clearly
  labeled sensitivity input, because there is no installed unified action/animation budget.
- Normal/boss scenes use the nearest mapped encounter. Early levels also measure the character's
  home-map route. Rank gaps are explicit. None of the eight proposed elite IDs has a configured
  map spawn in the initialized S6 maps: reference scenes use original elite definitions with
  synthetic placement, not appropriate-level or reachable route content.

## Equations And Gaps

- Mean damage includes actual misses, criticals and the current damage pipeline.
- Estimated TTK = original monster maximum HP / mean player damage per action * assumed
  player action seconds. Report 0.75/1.00/1.25 seconds as sensitivity, not observed animation time.
- No-potion survival = actual player HP / mean incoming damage per attack * original enemy
  attack seconds. Walk/dodge/AI uptime is not assumed to be free mitigation.
- HP/MP recovery is measured through the actual RecoverAsync implementation. Its per-second
  ceiling uses the installed shared resource cooldown (8/12/15 seconds), not a model refill.
- Resource costs are actual before/after TryConsumeForSkillAsync deltas. Direct-hit applications
  repeat full payloads through NumberOfHitsPerAttack, matching TargetedSkillDefaultPlugin.
  Area budgets are one successful target application, not an observed complete cast. Settings
  are recorded. Drain Life healing calls its actual strategy; other status/effect logic is excluded.
- Candidate target: at 400, ordinary progression gear single-monster TTK about 4..6 seconds;
  sustained mana and health must include actual cooldown and recovery, not reset between kills.
- Missing unified action cadence, missing normal-route elite bindings, unverified availability
  costs, multi-hit/buff semantics and class spread can block a universal monster-only budget.

Do not install the candidate Boss HP/damage/telegraph values from these inputs. Save measured
results first, then decide whether a small ordinary-monster-only adjustment can be justified.

## Reproduction

Run the explicit BalanceV1CombatProbeTests filter after setting OPENMU_COMBAT_PROBE_REPORT
to an absolute local JSON output path. The normal test suite skips this instrumentation test.
The test initializes 144 actors, records 504 encounters and runs separate 60-second resource
traces for 144 normal scenes. It opens neither a database nor a game/web listener.
See runtime-combat-calibration.md for measured findings and the required next decision.
