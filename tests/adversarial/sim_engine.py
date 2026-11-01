# tests/adversarial/sim_engine.py
"""
High-Precision Balance Simulation Engine for OpenMU Numerical Redesign.
Implements the exact mathematical models, combat pipelines, progression curves,
and boss encounters defined in OpenMU-数值重设计/design/balance.v2.json and
matching OpenMU-数值重设计/src/BalanceLab/Rules.cs and Simulation.cs.
"""

import os
import json
import math
import random
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple, Any

# Locate design file
DEFAULT_DESIGN_PATH = os.path.abspath(
    os.path.join(os.path.dirname(__file__), "..", "..", "OpenMU-数值重设计", "design", "balance.v2.json")
)


def load_design(json_path: Optional[str] = None) -> dict:
    path = json_path or DEFAULT_DESIGN_PATH
    if not os.path.exists(path):
        raise FileNotFoundError(f"Balance design JSON not found at {path}")
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


@dataclass
class PlayerStats:
    class_id: str
    build_id: str
    level: int
    master: int
    rank: float
    stats: List[float]  # [Str, Agi, Vit, Ene, Cmd]
    health: float
    mana: float
    power: float
    armor: float
    accuracy: float
    evasion: float
    haste: float
    crit: float
    excellent: float
    reduction: float
    life_steal: float
    ranged_uptime: float
    build: dict
    gear: dict


@dataclass
class MonsterStats:
    rank: float
    kind: str
    health: float
    damage: float
    armor: float
    accuracy: float
    evasion: float
    attack_seconds: float
    telegraph: bool
    experience: float
    zen: int


@dataclass
class FightResult:
    won: bool
    seconds: float
    kills: int
    remaining_health: float
    remaining_mana: float
    health_potions: int
    mana_potions: int
    potion_zen: int
    basic_fallback_casts: int
    recovery_seconds: float
    damage_taken: float
    damage_dealt: float
    telegraph_hits_taken: int
    telegraph_hits_dodged: int
    max_single_incoming_hit: float


@dataclass
class FarmResult:
    kills_per_hour: float
    xp_per_hour: float
    gross_zen_per_hour: float
    potion_zen_per_hour: float
    repair_zen_per_hour: float
    travel_zen_per_hour: float
    craft_budget_per_hour: float
    net_zen_per_hour: float
    combat_fraction: float
    recovery_fraction: float


@dataclass
class ProgressionResult:
    class_id: str
    build_id: str
    gear_id: str
    profile_id: str
    normal_hours: Optional[float]
    master_hours: Optional[float]
    max_normal_level_minutes: float
    max_master_level_minutes: float
    stalled_normal_level: Optional[int]
    stalled_master_level: Optional[int]
    level_hours: List[float] = field(default_factory=list)


class SimulationEngine:
    def __init__(self, design_data: Optional[dict] = None):
        self.D = design_data or load_design()
        self.skills = {s["id"]: s for s in self.D["skills"]}
        self.classes = {c["id"]: c for c in self.D["classes"]}
        self.gear = {g["id"]: g for g in self.D["gear"]}
        self.potions = self.D["economy"]["potions"]
        self.profiles = {p["id"]: p for p in self.D["profiles"]}
        self._normal_xp_table = self.generate_experience_table(master=False)
        self._master_xp_table = self.generate_experience_table(master=True)

    @staticmethod
    def interpolate_curve(anchors: List[List[float]], x: float) -> float:
        if x <= anchors[0][0]:
            return anchors[0][1]
        for i in range(1, len(anchors)):
            if x > anchors[i][0]:
                continue
            t = (x - anchors[i - 1][0]) / (anchors[i][0] - anchors[i - 1][0])
            return anchors[i - 1][1] + (anchors[i][1] - anchors[i - 1][1]) * t
        return anchors[-1][1]

    def base_experience(self, rank: float) -> float:
        m = self.D["monsters"]
        return m["xpBase"] + m["xpLinear"] * rank + m["xpQuadratic"] * rank * rank

    def base_zen(self, rank: float) -> float:
        m = self.D["monsters"]
        return m["zenBase"] + m["zenLinear"] * rank + m["zenQuadratic"] * rank * rank

    def generate_experience_table(self, master: bool) -> List[int]:
        cap = self.D["masterCap"] if master else self.D["normalCap"]
        table = [0] * (cap + 1)
        anchors = (
            self.D["progression"]["masterSeconds"]
            if master
            else self.D["progression"]["normalSeconds"]
        )
        ref_seconds = self.D["combat"]["referenceKillCycleSeconds"]
        start = 1 if master else 2
        for attained in range(start, cap + 1):
            source = attained if master else attained - 1
            rank = (
                self.D["normalCap"] + (attained - 1) * self.D["masterRankPerLevel"]
                if master
                else source
            )
            seconds = self.interpolate_curve(anchors, source)
            required = math.ceil(self.base_experience(rank) * seconds / ref_seconds)
            table[attained] = table[attained - 1] + max(1, required)
        return table

    @property
    def normal_xp_table(self) -> List[int]:
        return self._normal_xp_table

    @property
    def master_xp_table(self) -> List[int]:
        return self._master_xp_table

    def get_player(
        self,
        class_id: str,
        build_id: str,
        level: int,
        master: int = 0,
        gear_id: str = "progression",
        custom_allocation: Optional[List[float]] = None,
        power_weights_override: Optional[List[float]] = None,
    ) -> PlayerStats:
        if level < 1 or level > self.D["normalCap"]:
            raise ValueError(f"Level {level} out of range [1, {self.D['normalCap']}]")
        if master < 0 or master > self.D["masterCap"]:
            raise ValueError(f"Master level {master} out of range [0, {self.D['masterCap']}]")
        if level < self.D["normalCap"] and master != 0:
            raise ValueError("Master level before normal cap rejected")

        cls = self.classes[class_id]
        build = next((b for b in cls["builds"] if b["id"] == build_id), None)
        if not build:
            raise ValueError(f"Unknown build {build_id} for class {class_id}")

        allocation = custom_allocation if custom_allocation is not None else build["allocation"]
        power_weights = power_weights_override or build["powerWeights"]

        points = (level - 1) * self.D["pointsPerLevel"]
        allocated = [int(math.floor(x * points)) for x in allocation]
        remainders = sorted(
            range(5), key=lambda i: allocation[i] * points - allocated[i], reverse=True
        )
        for i in remainders[: points - sum(allocated)]:
            allocated[i] += 1

        stats = [float(allocated[i] + (0 if i == 4 else 20)) for i in range(5)]
        gear = self.gear[gear_id]
        g = self.D["growth"]
        master_fraction = master / float(self.D["masterCap"])
        rank = level + master * self.D["masterRankPerLevel"]
        power_budget = g["basePower"] + g["powerPerRank"] * rank

        health = (
            (g["baseHealth"] + g["healthPerLevel"] * level + g["healthPerVitality"] * stats[2])
            * cls["health"]
            * (1.0 + g["masterHealthCap"] * master_fraction)
        )
        mana = (
            (g["baseMana"] + g["manaPerLevel"] * level + g["manaPerEnergy"] * stats[3])
            * cls["mana"]
            * (1.0 + g["masterManaCap"] * master_fraction)
        )
        power = (
            (
                20.0
                + sum(stats[i] * power_weights[i] for i in range(5))
                + power_budget * g["gearPowerShare"] * gear["factor"]
            )
            * cls["damage"]
            * (1.0 + g["masterDamageCap"] * master_fraction)
            * (1.0 + min(g["partyDamageBuffCap"], build["support"]))
        )
        armor = (
            (g["baseArmor"] + g["armorPerAgility"] * stats[1] + power_budget * g["gearArmorShare"] * gear["factor"])
            * cls["armor"]
            * (1.0 + g["masterArmorCap"] * master_fraction)
        )
        haste = 1.0 + self.D["combat"]["hasteCap"] * (1.0 - math.exp(-stats[1] / (80.0 + rank * 1.8)))

        accuracy = 100.0 + 3.0 * rank + 0.5 * stats[1]
        evasion = 95.0 + 2.6 * rank + 0.22 * stats[1]
        crit = min(self.D["combat"]["critCap"], self.D["combat"]["critBase"] + gear["crit"])
        excellent = min(self.D["combat"]["excellentCap"], gear["excellent"])
        reduction = min(0.25, gear["reduction"] + build["bonusReduction"])
        life_steal = min(g["lifeStealCapPerSecond"], build["lifeSteal"])
        ranged_uptime = cls["rangedUptime"]

        return PlayerStats(
            class_id=class_id,
            build_id=build_id,
            level=level,
            master=master,
            rank=rank,
            stats=stats,
            health=health,
            mana=mana,
            power=power,
            armor=armor,
            accuracy=accuracy,
            evasion=evasion,
            haste=haste,
            crit=crit,
            excellent=excellent,
            reduction=reduction,
            life_steal=life_steal,
            ranged_uptime=ranged_uptime,
            build=build,
            gear=gear,
        )

    def get_monster(self, rank: float, kind: str = "normal", party_size: int = 1) -> MonsterStats:
        if not math.isfinite(rank) or rank < 1:
            raise ValueError(f"Invalid rank: {rank}")
        m = self.D["monsters"]
        spec = next((r for r in m["ranks"] if r["id"] == kind), None)
        if not spec:
            raise ValueError(f"Unknown monster rank {kind}")

        party_hp = 1.0 + self.D["progression"]["partyMonsterHpPerExtra"] * (party_size - 1)
        party_dmg = 1.0 + self.D["progression"]["partyMonsterDamagePerExtra"] * (party_size - 1)
        party_xp = 1.0 + self.D["progression"]["partyXpPerExtra"] * (party_size - 1)

        hp = (m["hpBase"] + m["hpLinear"] * rank + m["hpQuadratic"] * rank * rank) * spec["hp"] * party_hp
        damage = (m["damageBase"] + m["damageLinear"] * rank) * spec["damage"] * party_dmg
        armor = (m["armorBase"] + m["armorLinear"] * rank) * spec["armor"]
        exp = self.base_experience(rank) * spec["xp"] * party_xp
        zen = int(round(self.base_zen(rank) * spec["zen"]))

        return MonsterStats(
            rank=rank,
            kind=kind,
            health=hp,
            damage=damage,
            armor=armor,
            accuracy=100.0 + 3.0 * rank,
            evasion=100.0 + 3.0 * rank,
            attack_seconds=spec["attackSeconds"],
            telegraph=spec["telegraph"],
            experience=exp,
            zen=zen,
        )

    def hit_chance(self, accuracy: float, evasion: float) -> float:
        val = self.D["combat"]["hitBase"] + self.D["combat"]["hitSlope"] * math.log2(
            max(1.0, accuracy) / max(1.0, evasion)
        )
        return min(max(val, self.D["combat"]["hitMin"]), self.D["combat"]["hitMax"])

    def reduction(
        self,
        armor: float,
        attacker_rank: float,
        extra: float = 0.0,
        penetration: float = 0.0,
    ) -> float:
        c = self.D["combat"]
        effective_armor = max(0.0, armor) * (1.0 - min(max(penetration, 0.0), c["penetrationCap"]))
        armor_reduction = min(
            c["armorCap"],
            effective_armor / (effective_armor + c["armorConstant"] + c["armorPerRank"] * attacker_rank),
        )
        return min(
            c["totalReductionCap"],
            1.0 - (1.0 - armor_reduction) * (1.0 - min(max(extra, 0.0), 1.0)),
        )

    @staticmethod
    def minimum_damage(attacker_level: int) -> float:
        return max(1.0, max(4.0, max(1, attacker_level) / 10.0))

    def cast_damage(
        self,
        player: PlayerStats,
        target: MonsterStats,
        skill: dict,
        rng: Optional[random.Random] = None,
    ) -> float:
        base_dmg = max(
            player.power * skill["coefficient"] * (1.0 - self.reduction(target.armor, player.rank)),
            self.minimum_damage(player.level),
        )
        chance = self.hit_chance(player.accuracy, target.evasion)
        c = self.D["combat"]

        if rng is None:
            expected_special = (
                1.0
                + player.crit * (c["critMultiplier"] - 1.0)
                + player.excellent * (c["excellentMultiplier"] - 1.0)
            )
            return base_dmg * chance * expected_special

        result = 0.0
        for weight in skill["hitWeights"]:
            if rng.random() >= chance:
                continue
            roll = rng.random()
            if roll < player.excellent:
                multiplier = c["excellentMultiplier"]
            elif roll < player.excellent + player.crit:
                multiplier = c["critMultiplier"]
            else:
                multiplier = 1.0
            variance = 0.95 + rng.random() * 0.10
            result += base_dmg * weight * multiplier * variance
        return result

    def receive_damage(
        self,
        player: PlayerStats,
        target: MonsterStats,
        multiplier: float = 1.0,
        rng: Optional[random.Random] = None,
        telegraph: bool = False,
    ) -> float:
        hit = self.hit_chance(target.accuracy, player.evasion) * (1.0 if telegraph else player.ranged_uptime)
        damage = target.damage * multiplier * (1.0 - self.reduction(player.armor, target.rank, player.reduction))
        if rng is None:
            return damage * hit
        return damage * (0.9 + 0.2 * rng.random()) if rng.random() < hit else 0.0

    def action_seconds(self, player: PlayerStats, skill: dict) -> float:
        return max(self.D["combat"]["minimumActionSeconds"], skill["actionSeconds"] / player.haste)

    def skill_for(self, player: PlayerStats, area: bool = False) -> dict:
        desired_id = player.build["area"] if area else player.build["single"]
        desired = self.skills[desired_id]
        return desired if player.level >= desired["unlock"] else self.skills["basic"]

    def get_potion(self, player: PlayerStats, kind: str) -> dict:
        max_stat = player.health if kind == "health" else player.mana
        candidates = [p for p in self.potions if p["kind"] == kind and p["unlock"] <= player.level]
        candidates.sort(key=lambda p: (-min(p["cap"], p["fraction"] * max_stat), p["price"]))
        return candidates[0]

    def experience_multiplier(self, player_rank: float, target_rank: float) -> float:
        p = self.D["progression"]
        excess = player_rank - target_rank - p["overlevelGraceRanks"]
        if excess <= 0:
            return min(p["underlevelRewardCap"], 1.0 + max(0.0, target_rank - player_rank) / 200.0)
        return max(p["overlevelFloor"], math.exp(-excess / p["overlevelDecayRanks"]))

    def fight(
        self,
        player: PlayerStats,
        target: MonsterStats,
        count: int = 1,
        seed: Optional[int] = None,
        dodge_chance: Optional[float] = None,
        potions: bool = True,
        max_seconds: float = 600.0,
    ) -> FightResult:
        if count < 1 or count > 30 or not math.isfinite(max_seconds) or max_seconds <= 0:
            raise ValueError(f"Invalid count {count} or max_seconds {max_seconds}")

        dodge = dodge_chance if dodge_chance is not None else self.D["combat"]["trainedDodgeChance"]
        if dodge < 0 or dodge > 1 or not math.isfinite(dodge):
            raise ValueError(f"Invalid dodge_chance {dodge}")

        rng = random.Random(seed) if seed is not None else None
        enemy_health = [target.health] * count
        enemy_next = [target.attack_seconds + i * 0.12 for i in range(count)]

        hp = player.health
        mana = player.mana
        h_pot = self.get_potion(player, "health")
        m_pot = self.get_potion(player, "mana")

        time = 0.0
        next_cast = self.action_seconds(player, self.skill_for(player, count > 1))
        next_telegraph = self.D["combat"]["bossTelegraphSeconds"]
        next_h_pot = 0.0
        next_m_pot = 0.0

        total_damage_taken = 0.0
        total_damage_dealt = 0.0
        health_pots = 0
        mana_pots = 0
        basic_fallback = 0
        telegraph_hits_taken = 0
        telegraph_hits_dodged = 0
        max_single_incoming_hit = 0.0

        c = self.D["combat"]

        while time < max_seconds and hp > 0 and any(h > 0 for h in enemy_health):
            alive = [i for i in range(count) if enemy_health[i] > 0]
            next_enemy = min(enemy_next[i] for i in alive)
            next_tick = min(next_cast, next_enemy)
            if target.telegraph:
                next_tick = min(next_tick, next_telegraph)
            next_tick = min(next_tick, max_seconds)

            elapsed = next_tick - time
            hp = min(player.health, hp + elapsed * c["inCombatRegen"] * player.health)
            mana = min(player.mana, mana + elapsed * c["manaRegen"] * player.mana)
            time = next_tick

            # Boss Telegraph Attack
            if target.telegraph and time + 1e-8 >= next_telegraph:
                incoming = self.receive_damage(
                    player, target, c["bossTelegraphMultiplier"], rng, telegraph=True
                )
                if rng is None:
                    actual_incoming = incoming * (1.0 - dodge)
                    telegraph_hits_taken += 1
                else:
                    is_dodged = rng.random() < dodge
                    if is_dodged:
                        actual_incoming = 0.0
                        telegraph_hits_dodged += 1
                    else:
                        actual_incoming = incoming
                        telegraph_hits_taken += 1

                hp -= actual_incoming
                total_damage_taken += actual_incoming
                if actual_incoming > max_single_incoming_hit:
                    max_single_incoming_hit = actual_incoming
                next_telegraph += c["bossTelegraphSeconds"]

            # Enemy Normal Attacks
            for i in alive:
                if time + 1e-8 >= enemy_next[i]:
                    incoming = self.receive_damage(player, target, rng=rng)
                    hp -= incoming
                    total_damage_taken += incoming
                    if incoming > max_single_incoming_hit:
                        max_single_incoming_hit = incoming
                    enemy_next[i] += target.attack_seconds

            if hp <= 0:
                break

            # Potion Consumption
            if potions and hp < player.health * 0.70 and time >= next_h_pot:
                heal = min(h_pot["cap"], player.health * h_pot["fraction"])
                hp = min(player.health, hp + heal)
                next_h_pot = time + self.D["economy"]["healthPotionCooldown"]
                health_pots += 1

            if potions and mana < player.mana * 0.30 and time >= next_m_pot:
                restore = min(m_pot["cap"], player.mana * m_pot["fraction"])
                mana = min(player.mana, mana + restore)
                next_m_pot = time + self.D["economy"]["manaPotionCooldown"]
                mana_pots += 1

            if time + 1e-8 < next_cast:
                continue

            # Player Skill Cast
            skill = self.skill_for(player, len(alive) > 1)
            cost = skill["manaFlat"] + skill["manaFraction"] * player.mana
            if mana < cost:
                skill = self.skills["basic"]
                cost = 0.0
                basic_fallback += 1
            mana -= cost

            dealt = 0.0
            tw = skill["targetWeights"]
            for order in range(min(len(alive), len(tw))):
                dmg = self.cast_damage(player, target, skill, rng) * tw[order]
                idx = alive[order]
                dealt += min(enemy_health[idx], dmg)
                enemy_health[idx] -= dmg

            total_damage_dealt += dealt
            action = self.action_seconds(player, skill)
            hp = min(
                player.health,
                hp + min(player.health * player.life_steal * action, dealt * 0.03),
            )
            next_cast = time + action

        won = hp > 0 and all(h <= 0 for h in enemy_health)
        recovery = max(
            (player.health - max(0.0, hp)) / (player.health * c["outOfCombatRegen"]),
            (player.mana - mana) / (player.mana * c["outOfCombatRegen"]),
        )
        recovery = max(recovery, max(next_h_pot, next_m_pot) - time)

        potion_zen = health_pots * h_pot["price"] + mana_pots * m_pot["price"]

        return FightResult(
            won=won,
            seconds=time,
            kills=sum(1 for h in enemy_health if h <= 0),
            remaining_health=max(0.0, hp),
            remaining_mana=mana,
            health_potions=health_pots,
            mana_potions=mana_pots,
            potion_zen=potion_zen,
            basic_fallback_casts=basic_fallback,
            recovery_seconds=recovery,
            damage_taken=total_damage_taken,
            damage_dealt=total_damage_dealt,
            telegraph_hits_taken=telegraph_hits_taken,
            telegraph_hits_dodged=telegraph_hits_dodged,
            max_single_incoming_hit=max_single_incoming_hit,
        )

    def farm(
        self,
        player: PlayerStats,
        content_rank: Optional[float] = None,
        count: Optional[int] = None,
    ) -> FarmResult:
        target = self.get_monster(content_rank if content_rank is not None else player.rank)
        pack = count if count is not None else (1 if player.level < 20 else self.D["combat"]["packSize"])
        result = self.fight(player, target, pack)
        if not result.won:
            return FarmResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0)

        cycle = result.seconds + max(result.recovery_seconds, self.D["combat"]["travelSeconds"])
        cycles = 3600.0 / cycle
        kills = cycles * pack

        common_loot = next(e for e in self.D["loot"]["equipment"] if e["id"] == "common")
        gross = kills * (
            target.zen * self.D["loot"]["moneyChance"]
            + common_loot["chance"]
            * self.D["economy"]["saleValuePerNormalMonsterZen"]
            * self.base_zen(target.rank)
        )
        potion_cost = cycles * result.potion_zen
        repair_cost = (
            cycles
            * result.seconds
            / 60.0
            * target.rank
            * self.D["economy"]["repairZenPerCombatMinuteRank"]
        )
        travel_cost = target.rank * self.D["economy"]["travelZenPerRank"] * 4.0
        craft = gross * self.D["economy"]["plannedCraftSpendFraction"]
        net = gross - potion_cost - repair_cost - travel_cost - craft

        xp = kills * target.experience * self.experience_multiplier(player.rank, target.rank)

        return FarmResult(
            kills_per_hour=kills,
            xp_per_hour=xp,
            gross_zen_per_hour=gross,
            potion_zen_per_hour=potion_cost,
            repair_zen_per_hour=repair_cost,
            travel_zen_per_hour=travel_cost,
            craft_budget_per_hour=craft,
            net_zen_per_hour=net,
            combat_fraction=result.seconds / cycle,
            recovery_fraction=result.recovery_seconds / cycle,
        )

    def progression(
        self,
        class_id: str,
        build_id: str,
        gear_id: str = "progression",
        profile_id: str = "standard",
        dk_guard_vit_bonus: float = 0.0,
    ) -> ProgressionResult:
        normal_table = self.normal_xp_table
        master_table = self.master_xp_table
        profile = self.profiles[profile_id]

        power_override = None
        if class_id == "DK" and build_id == "guard" and dk_guard_vit_bonus > 0:
            power_override = [0.95, 0.20, dk_guard_vit_bonus, 0.15, 0.0]

        normal_hours = 0.0
        master_hours = 0.0
        max_normal = 0.0
        max_master = 0.0
        stalled_normal = None
        stalled_master = None
        level_hours = []

        for level in range(1, self.D["normalCap"]):
            p = self.get_player(
                class_id,
                build_id,
                level,
                gear_id=gear_id,
                power_weights_override=power_override,
            )
            farm_res = self.farm(p)
            if farm_res.xp_per_hour <= 0:
                stalled_normal = level
                break
            hours = (normal_table[level + 1] - normal_table[level]) / (
                farm_res.xp_per_hour * profile["normalXp"]
            )
            normal_hours += hours
            level_hours.append(hours)
            max_normal = max(max_normal, hours * 60.0)

        for ml in range(0, self.D["masterCap"]):
            p = self.get_player(
                class_id,
                build_id,
                self.D["normalCap"],
                ml,
                gear_id=gear_id,
                power_weights_override=power_override,
            )
            farm_res = self.farm(p)
            if farm_res.xp_per_hour <= 0:
                stalled_master = ml
                break
            hours = (master_table[ml + 1] - master_table[ml]) / (
                farm_res.xp_per_hour * profile["masterXp"]
            )
            master_hours += hours
            max_master = max(max_master, hours * 60.0)

        return ProgressionResult(
            class_id=class_id,
            build_id=build_id,
            gear_id=gear_id,
            profile_id=profile_id,
            normal_hours=None if stalled_normal is not None else normal_hours,
            master_hours=None if stalled_master is not None else master_hours,
            max_normal_level_minutes=max_normal,
            max_master_level_minutes=max_master,
            stalled_normal_level=stalled_normal,
            stalled_master_level=stalled_master,
            level_hours=level_hours,
        )


# Key Boss Definitions per Requirement R3
KEY_BOSSES = {
    "Balrog": {"id": 38, "rank": 150.0, "map": "Lost Tower 7", "gear": "progression", "player_level": 150, "player_ml": 0},
    "Hydra": {"id": 49, "rank": 190.0, "map": "Atlans 3", "gear": "progression", "player_level": 190, "player_ml": 0},
    "Phoenix": {"id": 77, "rank": 265.0, "map": "Icarus", "gear": "progression", "player_level": 265, "player_ml": 0},
    "Kundun": {"id": 275, "rank": 400.0, "map": "Kalima 7", "gear": "excellent", "player_level": 400, "player_ml": 0},
    "Selupan": {"id": 459, "rank": 450.0, "map": "Raklion Hatchery", "gear": "excellent", "player_level": 400, "player_ml": 143},
}

ALL_CLASSES = ["DK", "DW", "FE", "MG", "DL", "SU", "RF"]
ALL_BUILDS = [
    ("DK", "assault"),
    ("DK", "guard"),
    ("DW", "elemental"),
    ("DW", "barrier"),
    ("FE", "marksman"),
    ("FE", "support"),
    ("MG", "blade"),
    ("MG", "spell"),
    ("DL", "command"),
    ("DL", "force"),
    ("SU", "curse"),
    ("SU", "sustain"),
    ("RF", "brawler"),
    ("RF", "vitality"),
]
