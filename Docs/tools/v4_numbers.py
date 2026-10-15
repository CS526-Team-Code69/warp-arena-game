"""V4 numeric model: weapon Mk tiers, stat cards, elements and skills.

Run: python Docs/tools/v4_numbers.py
Units: HP, raw damage per hit, seconds, stacks. Every target below is a design
choice; every "reference build" is an assumption, not telemetry.
"""
import json
import math
from pathlib import Path

OUT = Path(__file__).with_name("v4_numbers.json")

# ---------- A. Enemy budget (already in EncounterBalance.asset) ----------
DPS = [7.5, 8.5, 10, 12, 14, 16, 18, 20, 23, 26, 28, 31, 34, 37, 40]
FODDER_SECONDS = .65


def dps(w): return DPS[min(max(w, 1), 15) - 1]


def fodder_hp(w): return max(1, round(dps(w) * FODDER_SECONDS))


def wave_scale(w): return dps(w) / dps(1)  # R(w): fodder EHP ratio vs wave 1


# ---------- B. Reference build (assumption) ----------
# Share of the reference DPS delivered by the held weapon; the rest is auto weapons and skills.
def hand_share(w): return 1 - .30 * (w - 1) / 14


# Expected common-tier stat cards a normal (not optimal) build holds by wave w: continuous
# expected values, 2 Damage and 1 Attack Speed card by wave 15 (about 5 stat picks spread over
# 7 card types in ~10 drafts). Builds that take more cards simply run ahead of the target.
def damage_cards(w): return 2 * (w - 1) / 14


def speed_cards(w): return 1 * (w - 1) / 14


# ---------- C. Stat cards (common / rare / exceptional; legendary = exceptional x1.2) ----------
CRIT_MULTIPLIER = 2.0
CARDS = {
    # multiplicative chain: DamageMultiplier = product(1 + d_i); crit chance adds, capped
    "Damage": dict(main=(.12, .18, .25), crit=(.08, .12, .16)),
    # division: cooldown = base / (1 + sum s_i)  -> attack rate grows linearly, cooldown cut diminishes
    "AttackSpeed": dict(main=(.15, .22, .32)),
    # division: damage taken = raw * K / (K + armor)  -> EHP = HP * (1 + armor / K), linear in EHP
    "Protection": dict(main=(25, 40, 60), K=100),
    # additive: max HP and regen per 5 s
    "MaxHp": dict(main=(15, 25, 40), regen=(1, 1.5, 2.5)),
    # additive: move speed fraction and extra sprint seconds
    "MoveSpeed": dict(main=(.08, .12, .16), stamina=(.2, .3, .45)),
    # additive: knockback fraction and wall-slam bonus (scaled by wave HP multiplier in code)
    "Knockback": dict(main=(.2, .35, .5), impact=(3, 5, 8)),
    # additive: max energy and energy per kill
    "MaxEnergy": dict(main=(25, 40, 60), siphon=(.4, .7, 1.0)),
    # conditional (boss immunity waves): fraction of immunity ignored
    "ElementPenetration": dict(main=(.2, .35, .5)),
}
REMOVED = ["Regeneration", "Stamina", "Impact", "PickupRange", "Luck", "EnergySiphon"]


def stat_multiplier(w, tier=0):
    d, c, s = CARDS["Damage"]["main"][tier], CARDS["Damage"]["crit"][tier], CARDS["AttackSpeed"]["main"][tier]
    nd, ns = damage_cards(w), speed_cards(w)
    return (1 + d) ** nd * (1 + nd * c * (CRIT_MULTIPLIER - 1)) * (1 + ns * s)


# ---------- D. Weapon Mk tiers derived from fodder EHP -> required EDPS ----------
MK_SPACING = 3  # a new tier every 3 waves
MK_MAX = 6  # one tier above the last window


def mk_window_start(k): return 1 + MK_SPACING * (k - 1)


def mk_par_for_wave(w): return min(MK_MAX - 1, 1 + (w - 1) // MK_SPACING)


def mk_multiplier(k):
    """Damage multiplier that puts a Mk-k weapon on target at the centre of its window.
    M(k) = R(c) * h(c) / (h(1) * S(c)), c = window centre (clamped to 15)."""
    c = min(15, mk_window_start(k) + (MK_SPACING - 1) / 2)
    lo, hi = math.floor(c), math.ceil(c)

    def at(w): return wave_scale(w) * hand_share(w) / (hand_share(1) * stat_multiplier(w))

    raw = at(lo) if lo == hi else (at(lo) + at(hi)) / 2
    return raw


MK_TABLE = []
for k in range(1, MK_MAX + 1):
    raw = mk_multiplier(k) if k < MK_MAX else None
    MK_TABLE.append(raw)
# Extrapolate the last tier with the previous step ratio.
MK_TABLE[-1] = MK_TABLE[-2] * MK_TABLE[-2] / MK_TABLE[-3]
MK_TABLE = [1.0] + [round(m / MK_TABLE[0] * 20) / 20 for m in MK_TABLE[1:]]  # normalise Mk1 = 1, round to .05

# Representative per-hit damage at Mk 1, wave 1, after the 0.5 starting-form factor.
FORMS = {
    "Rifle A (full charge)": 4.5, "Rifle B (reverse)": 1.0, "Knife A": 3.05, "Knife B hook": 6,
    "Rocket A": 5, "Phase Dagger A": 5, "Plasma A": 12, "Cryo A": 18, "Arc Coil A": 6,
    "Railgun A": 36, "Minigun": 3, "Flamethrower A": 2, "Swarm A (per missile)": 4,
}


def hits_to_kill(hit, hp): return math.ceil(hp / max(1, hit))


def one_hit_chance(hit, hp, crit):
    if hit >= hp: return 1.0
    if hit * CRIT_MULTIPLIER >= hp: return crit
    return 0.0


# ---------- E. Elements ----------
DAMAGE_PER_STACK = 5  # stacks = base hit / (5 * R(w)); built-in weapons x2
FIRE = dict(interval=.5, half_life=1.5, damage_per_stack=1.5, cap=20)  # exponential burn-out
POISON = dict(interval=1.0, damage_per_stack=.12, cap=20)  # additive, no decay
FROST = dict(threshold=6, decay=1.0, chill=1.5, freeze=1.5, immunity=3)  # division chill
LIGHTNING = dict(threshold=6, duration=4, slow=.55, twitch_interval=1.0, twitch=.3, frost_bonus=1.5, chain_falloff=.7)
EARTH, LIGHT, VOID = .10, .05, .10
LIGHT_MISSING_BONUS = 2.0  # heal x (1 + 2 * missing HP fraction)


def fire_q(): return 1 - .5 ** (FIRE["interval"] / FIRE["half_life"])


def fire_share(): return FIRE["damage_per_stack"] / DAMAGE_PER_STACK  # burn DPS / weapon DPS at steady state


def poison_share(t): return POISON["damage_per_stack"] * t / DAMAGE_PER_STACK  # poison DPS / weapon DPS after t s


def poison_cap_dps(w): return POISON["cap"] * POISON["damage_per_stack"] * wave_scale(w) / POISON["interval"]


def chill(frost_stacks, threshold): return 1 / (1 + FROST["chill"] * frost_stacks / threshold)


# ---------- F. Skills ----------
SKILL_LEVEL_GROWTH = 1.35  # exponential: damage x 1.35^(level-1)
SKILLS = {"StormCall bolt": 6, "GravityWell end": 10, "Singularity base": 10, "Singularity per enemy": 4}
BULWARK_SHARE = (.20, .30, .30)  # shield = max HP x share (proportional)


def skill_damage(base, w, level, damage_mult=1.0):
    return base * wave_scale(w) * SKILL_LEVEL_GROWTH ** (level - 1) * damage_mult


def main():
    report = {}
    print("A. Wave scale and fodder")
    rows = []
    for w in range(1, 16):
        rows.append(dict(wave=w, dps=dps(w), fodder_hp=fodder_hp(w), R=round(wave_scale(w), 3),
                         hand_share=round(hand_share(w), 3), stat_mult=round(stat_multiplier(w), 3),
                         mk_par=mk_par_for_wave(w)))
        r = rows[-1]
        print(
            f"  w{w:2} DPS {r['dps']:>4} HP {r['fodder_hp']:>2} R {r['R']:.2f} hand {r['hand_share']:.2f} S {r['stat_mult']:.2f} MkPar {r['mk_par']}")
    report["waves"] = rows

    print("\nB. Mk table (normalised, rounded to .05)")
    steps = []
    for k, m in enumerate(MK_TABLE, 1):
        step = m / MK_TABLE[k - 2] if k > 1 else 1
        steps.append(dict(mk=k, window_start=mk_window_start(k), multiplier=m, step=round(step, 3)))
        print(f"  Mk{k} from wave {mk_window_start(k):2}: x{m:.2f}  (+{(step - 1) * 100:4.1f}% over previous)")
    report["mk"] = steps

    print("\nC. Hand EDPS check: base 7.5 x Mk(par) x S(w) vs required hand EDPS = DPS x hand share")
    check = []
    for w in range(1, 16):
        k = mk_par_for_wave(w)
        got = 7.5 * MK_TABLE[k - 1] * stat_multiplier(w)
        need = dps(w) * hand_share(w)
        ttk = fodder_hp(w) / (got / hand_share(w))
        check.append(dict(wave=w, mk=k, hand_edps=round(got, 2), required=round(need, 2), ratio=round(got / need, 3),
                          fodder_ttk=round(ttk, 3)))
        print(
            f"  w{w:2} Mk{k} hand {got:5.2f} need {need:5.2f} ratio {got / need:.2f}  fodder TTK {ttk:.2f}s (all sources)")
    report["edps_check"] = check

    print("\nD. Fodder hits-to-kill at par Mk (no cards / + one common Damage card) and with owned upgrade (Mk+1)")
    bp = []
    for w in (1, 4, 7, 10, 13, 15):
        k = mk_par_for_wave(w);
        hp = fodder_hp(w);
        S = stat_multiplier(w)
        for name, base in FORMS.items():
            hit = base * MK_TABLE[k - 1] * S
            hit_card = hit * (1 + CARDS["Damage"]["main"][0])
            up = base * MK_TABLE[min(MK_MAX, k + 1) - 1] * S
            crit = damage_cards(w) * CARDS["Damage"]["crit"][0]
            bp.append(dict(wave=w, form=name, hp=hp, hit=round(hit, 2), htk=hits_to_kill(hit, hp),
                           htk_card=hits_to_kill(hit_card, hp), htk_upgrade=hits_to_kill(up, hp),
                           one_hit_crit=round(one_hit_chance(hit, hp, crit + .04), 2)))
        if w in (1, 7, 15):
            print(f"  wave {w} (HP {hp}, Mk{k}, S {S:.2f})")
            for r in bp[-len(FORMS):]:
                print(
                    f"    {r['form']:<24} hit {r['hit']:6.2f} hits {r['htk']} | +1 dmg card {r['htk_card']} | Mk+1 {r['htk_upgrade']}")
    report["breakpoints"] = bp

    print("\nE. Stat cards (common tier) — what one card does")
    K = CARDS["Protection"]["K"]
    for a in CARDS["Protection"]["main"]:
        print(f"  Protection +{a}: damage taken x{K / (K + a):.3f}  EHP +{a / K * 100:.0f}%")
    two = K / (K + 50)
    print(f"  Two common Protection cards: taken x{two:.3f} (EHP +50%)")
    d, c = CARDS["Damage"]["main"][0], CARDS["Damage"]["crit"][0]
    print(
        f"  Damage common: x{1 + d:.2f} damage and +{c * 100:.0f}% crit (x{CRIT_MULTIPLIER}) -> expected x{(1 + d) * (1 + c):.3f}")
    print(f"  Three common Damage cards: x{(1 + d) ** 3 * (1 + 3 * c):.3f} expected")
    s = CARDS["AttackSpeed"]["main"][0]
    print(f"  AttackSpeed common: cooldown x{1 / (1 + s):.3f} (rate +{s * 100:.0f}%); two cards x{1 / (1 + 2 * s):.3f}")
    report["cards"] = CARDS;
    report["removed_cards"] = REMOVED

    print("\nF. Elements")
    q = fire_q()
    print(
        f"  Fire: tick every {FIRE['interval']}s burns {q * 100:.1f}% of stacks; each stack deals {FIRE['damage_per_stack']} x R in total")
    print(
        f"        mean stack life {FIRE['interval'] / q:.2f}s; steady burn = {fire_share() * 100:.0f}% of the weapon's DPS (built-in x2 = {2 * fire_share() * 100:.0f}%)")
    for t in (2, 4, 8, 30, 60):
        print(f"  Poison after {t:2}s of sustained hits: +{poison_share(t) * 100:5.1f}% of weapon DPS (built-in x2)")
    print(f"  Poison cap DPS at wave 1 / 15: {poison_cap_dps(1):.1f} / {poison_cap_dps(15):.1f}")
    for f in (0, 2, 4, 5.9):
        print(f"  Frost chill at {f} / {FROST['threshold']} stacks: speed x{chill(f, FROST['threshold']):.2f}")
    for f in (0, 3):
        print(
            f"  Lightning paralysis with chill {f} stacks: speed x{LIGHTNING['slow'] * chill(f, FROST['threshold']):.2f} (multiplicative)")
    print(f"  Chain falloff: {[round(LIGHTNING['chain_falloff'] ** i, 2) for i in range(5)]}")
    for miss in (0, .5, .8):
        print(
            f"  Light heal at {miss * 100:.0f}% missing HP: {LIGHT * (1 + LIGHT_MISSING_BONUS * miss) * 100:.1f}% of damage")
    report["elements"] = dict(fire=FIRE, poison=POISON, frost=FROST, lightning=LIGHTNING,
                              earth=EARTH, light=LIGHT, void=VOID, light_missing_bonus=LIGHT_MISSING_BONUS,
                              fire_tick_fraction=round(q, 4))

    print("\nG. Skills (damage x R(w) x 1.35^(L-1) x player damage multiplier)")
    for name, base in SKILLS.items():
        vals = [round(skill_damage(base, w, L), 1) for w in (1, 7, 15) for L in (1, 3)]
        print(f"  {name:<22} w1 L1/L3 {vals[0]}/{vals[1]}  w7 {vals[2]}/{vals[3]}  w15 {vals[4]}/{vals[5]}")
    for w in (1, 15):
        bolt = skill_damage(6, w, 1)
        print(f"  StormCall L1 at wave {w}: 3 bolts, one random target each: {bolt:.1f} per bolt vs fodder HP {fodder_hp(w)} -> {hits_to_kill(bolt, fodder_hp(w))} bolt(s) per kill")
    report["skills"] = dict(level_growth=SKILL_LEVEL_GROWTH, bases=SKILLS, bulwark_share=BULWARK_SHARE)

    OUT.write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding="utf-8")
    print(f"\nWrote {OUT}")


if __name__ == "__main__":
    main()
