using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Weapon crates (implementation spec 2026-11-10, sections A and B). Day 0 has two on the way to the base: the first is the starting
    /// weapon (taught with the spotlight, never left; the pick goes into the main hand and the pistol stays in the other hand), the second
    /// may be left and its weapon replaces the pistol. From day 1 every afternoon puts one crate on the day's ore trail, about six tenths
    /// of the way from the base to the trail's farthest node (CampaignRules.crateTrailShare), so following the ore always meets it; a
    /// crate not opened is gone when the next afternoon brings a new one.
    /// E at a crate opens it at once (no lock, spec A): one of three new anomaly weapons the player does not own,
    /// rolled the way the open mode's armory rolls them (RunWeapons.RollArmory, new weapons only), on the crate's own panel
    /// (V2CratePanel) while the run holds still. The weapon goes where the base's research puts one (CrateSlots: a free hand, else the
    /// pistol's hand, else the hand not in use; an auto weapon only into a free auto slot), and its weapon card shows as after a purchase
    /// (GameManager.CampaignShowWeaponCard). A crate left gives nothing instead and is gone.
    /// </summary>
    public sealed partial class V2Campaign
    {
        enum CrateKind { TutorialFirst, TutorialSecond, Afternoon }

        sealed class Crate
        {
            public CrateKind kind; public Vector3 position; public Transform view;
        }

        readonly List<Crate> crates = new();
        readonly List<RunWeapon> crateOffers = new();
        // The crate whose panel is open, what to call once it is closed (and its weapon card dismissed), and the phase it came from.
        Crate crateOpen;
        System.Action<bool> crateDone;
        ArenaUiState crateReturnPhase;
        bool crateLoopRunning;
        // The hero's line about the first weapon taken from a crate (T10) was said in this run.
        bool changedWeaponSaid;

        /// <summary>For automated checks: a crate's panel is open; how many crates lie on the map.</summary>
        public bool CratePanelOpen => crateOpen != null;
        public int CratesOnMap => crates.Count;
        /// <summary>For automated checks: the weapons on the open crate's panel, in the panel's order; empty while no panel is open.</summary>
        public WeaponForm[] CrateOfferForms => crateOpen == null ? System.Array.Empty<WeaponForm>() : crateOffers.ConvertAll(w => w.form).ToArray();

        /// <summary>Called as each afternoon begins, after the day's ore is placed (BeginDusk): yesterday's crate goes, today's lies on the
        /// ore trail, and the hero says it is time to look for ore and for crates that survived (subtitles only, after the base's line).</summary>
        partial void AfternoonCrates()
        {
            RemoveCrates(CrateKind.Afternoon);
            if (Day >= 1 && TryAfternoonCrateSpot(out var at)) PlaceCrate(at, CrateKind.Afternoon);
            int day = Day;
            SayWhenQuiet(Line.D02, () => Day == day && Stage is CampaignStage.Dusk or CampaignStage.Base);
        }

        /// <summary>A point on the day's ore trail about crateTrailShare of the way out (from the base to the trail's farthest node), on open
        /// ground clear of the ore. Without a trail (no black hole on the map) there is no crate.</summary>
        bool TryAfternoonCrateSpot(out Vector3 at)
        {
            at = Vector3.zero;
            Vector3? goal = trailTarget ?? HoleCentre;
            if (goal is not Vector3 hole) return false;
            Vector3 toHole = Query.Flat(hole - BaseCentre);
            float distance = toHole.magnitude;
            if (distance < 1f) return false;
            Vector3 dir = toHole / distance, side = new(-dir.z, 0f, dir.x);
            var trail = new List<Vector3>();
            foreach (var node in nodes) if (node.trailIndex >= 0) trail.Add(node.position);
            float reach = SpotlightTutorial.Reach(trail, BaseCentre, dir);
            // No trail node could be placed: the trail's planned reach (V2Campaign.Night.cs PlanTrail) stands in for it.
            if (reach <= 0f) reach = Mathf.Min(rules.trailReach + rules.trailReachPerDay * Mathf.Max(0, Day - 1), distance - TrailHoleKeepOut);
            Vector3 p = BaseCentre + dir * Mathf.Max(rules.nodeBaseClearance, reach * rules.crateTrailShare);
            for (float r = 0f; r <= 10f; r += 1f)
                foreach (var candidate in new[] { p + side * r, p - side * r, p - dir * r, p + dir * r })
                    if (CrateSpotOk(candidate)) { at = candidate; return true; }
            return false;
        }

        bool CrateSpotOk(Vector3 at)
        {
            if (!OreSpotOk(at, 1.2f)) return false;
            foreach (var node in nodes) if (Query.Flat(node.position - at).magnitude < rules.crateNodeClearance) return false;
            foreach (var crate in crates) if (Query.Flat(crate.position - at).magnitude < rules.crateNodeClearance) return false;
            return true;
        }

        Crate PlaceCrate(Vector3 at, CrateKind kind)
        {
            var crate = new Crate { kind = kind, position = Query.Flat(at) };
            var root = new GameObject("WeaponCrate").transform;
            root.SetParent(worldRoot, false);
            root.position = crate.position;
            root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            // The whitebox table's crate carries a lock for a lock mechanic that was dropped (the crate just
            // opens), so no crate shows it.
            Whitebox.Build("crate", root).GetComponent<WhiteboxLook>().ShowParts("lock", false);
            crate.view = root;
            crates.Add(crate);
            return crate;
        }

        void RemoveCrate(Crate crate, bool opened)
        {
            if (crate == null) return;
            crates.Remove(crate);
            if (opened) Fx.Burst(crate.position + Vector3.up * .8f, Vector3.up, new Color(1f, .82f, .4f), 12, 6f, 360f, .3f);
            if (crate.view) Destroy(crate.view.gameObject);
            crate.view = null;
        }

        void RemoveCrates(CrateKind kind)
        {
            for (int i = crates.Count - 1; i >= 0; i--)
                if (crates[i].kind == kind) RemoveCrate(crates[i], false);
        }

        /// <summary>Near a crate: the prompt to show, and true when E opens it this frame. Not while a panel is open, while a question is
        /// asked or while an ore node is in mining reach (E there mines).</summary>
        bool CrateUse(Crate crate, out string promptText)
        {
            promptText = "";
            var p = gm.player;
            if (crate == null || !p || !p.IsAlive || crateOpen != null || knockedOut) return false;
            if (Query.Flat(p.Position - crate.position).magnitude > rules.crateReach || NearestNode(p.Position, rules.mineRange) != null) return false;
            promptText = ArenaText.Pair($"按 {ArenaKeys.InteractKey} 打开武器箱", $"Press {ArenaKeys.InteractKey} to open the weapon crate");
            return EPressed;
        }

        /// <summary>Opens a crate: rolls its weapons and shows them while the run holds still. done runs once the panel is closed and the
        /// weapon card (if one was taken) dismissed, with whether a weapon was taken; a crate with nothing new in it says so and is gone.</summary>
        void OpenCrate(Crate crate, System.Action<bool> done)
        {
            var weapons = gm ? gm.runWeapons : null;
            if (crate == null || crateOpen != null || !weapons) return;
            int wave = crate.kind switch
            {
                CrateKind.TutorialFirst => rules.tutorialFirstCrateWave,
                CrateKind.TutorialSecond => rules.tutorialSecondCrateWave,
                _ => Mathf.Max(1, gm.waves ? gm.waves.WaveIndex + 2 : 1) // as the Armament Bay's research (TryBuyNewWeapon)
            };
            // Day 0's crates hold hand weapons only: the first one's goes into the main hand, the second one's replaces the pistol.
            RollCrateOffers(wave, crate.kind != CrateKind.Afternoon);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair, crate.position, .8f);
            if (crateOffers.Count == 0)
            {
                RemoveCrate(crate, true);
                CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneCrate, crate.kind + ":" + CampaignEvents.CrateEmpty, 0f, Day);
                ui.Flash(ArenaText.Pair("箱子里没有你用得上的新武器", "There's nothing new for you in this crate"), 2.5f);
                done?.Invoke(false);
                return;
            }
            cratesOpened++;
            // Once the panel is closed (and a taken weapon's card dismissed): the caller's own step, then the right button's (the second crate).
            crateOpen = crate; crateDone = taken => { done?.Invoke(taken); MaybeTeachSwitch(); }; crateReturnPhase = gm.RunPhase;
            gm.CampaignSetPhase(ArenaUiState.Story); // the run holds still, as under a yes / no question
            var options = new List<V2CratePanel.Option>();
            foreach (var weapon in crateOffers) options.Add(Describe(weapon));
            string heading = ArenaText.Pair("武器箱", "WEAPON CRATE") + (options.Count >= 3 ? ArenaText.Pair(" · 三选一", " · PICK ONE OF THREE") : ArenaText.Pair(" · 选一件", " · PICK ONE"));
            // Since 2026-11-10 the panel says nothing about what the weapons are (no word of anomalies, attacks or traits) and carries
            // no line under its heading: a name and a Mk each, the rest unknown until one is taken.
            string sub = "";
            ui.ShowCrate(heading, sub, options, crate.kind != CrateKind.TutorialFirst, TakeFromCrate, LeaveCrate);
            // Each weapon on the panel is recorded as the panel opens, with its place: the crate's own record names only the weapon
            // taken, and how often a weapon is passed over can be counted only against how often it was shown. A panel is recorded
            // once: it closes through a pick or the leave button, both remove the crate, and every opening rolls its own weapons.
            for (int i = 0; i < crateOffers.Count; i++)
                CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneCrateOffer, crate.kind + ":" + crateOffers[i].form, i + 1, Day);
        }

        /// <summary>Up to three new anomaly weapons the player does not own and has room for, from the armory's own roll (new weapons only;
        /// a roll's stronger versions of held weapons are passed over), rolled again until three different ones are found.</summary>
        void RollCrateOffers(int wave, bool handOnly)
        {
            crateOffers.Clear();
            var weapons = gm.runWeapons;
            var mode = armoryMode;
            armoryMode = ArmoryMode.NewWeaponsOnly;
            try
            {
                for (int roll = 0; roll < 8 && crateOffers.Count < RunWeapons.ArmoryOfferCount; roll++)
                {
                    weapons.RollArmory(wave);
                    foreach (var weapon in weapons.Offers)
                    {
                        if (crateOffers.Count >= RunWeapons.ArmoryOfferCount) break;
                        if (weapon == null || weapon.upgradeOf != null || !weapon.IsAnomalous || handOnly && weapon.IsAuto) continue;
                        if (crateOffers.Exists(o => o.form == weapon.form)) continue;
                        if (CrateSlots.SlotFor(weapon, weapons.Hand, weapons.Auto, weapons.ActiveIndex) < 0) continue;
                        crateOffers.Add(weapon);
                    }
                }
            }
            finally
            {
                armoryMode = mode;
                weapons.Offers = new RunWeapon[RunWeapons.ArmoryOfferCount];
            }
        }

        V2CratePanel.Option Describe(RunWeapon weapon)
        {
            var weapons = gm.runWeapons;
            int slot = CrateSlots.SlotFor(weapon, weapons.Hand, weapons.Auto, weapons.ActiveIndex);
            var slots = weapon.IsAuto ? weapons.Auto : weapons.Hand;
            var there = slot >= 0 && slot < slots.Length ? slots[slot] : null;
            // Only what a pick would push out is said (a free hand or slot needs no word): losing a held weapon must not come as a surprise.
            string where = there == null ? ""
                : there.form == WeaponForm.Pistol ? ArenaText.Pair("换下小手枪", "Replaces the pistol")
                : ArenaText.Pair($"换下{there.FormName}", $"Replaces the {there.FormName}");
            return new V2CratePanel.Option
            {
                title = weapon.FormName,
                kind = weapon.MkLabel,
                // Its attack, its trait and its rarity show once it is taken (the weapon card), not here.
                body = ArenaText.Pair("？？？", "? ? ?"),
                badge = where,
                tint = Color.white,
                formIndex = (int)weapon.form
            };
        }

        void TakeFromCrate(int index)
        {
            var crate = crateOpen;
            var done = crateDone;
            crateOpen = null; crateDone = null;
            var weapons = gm.runWeapons;
            if (crate == null || index < 0 || index >= crateOffers.Count) { crateOffers.Clear(); gm.CampaignSetPhase(crateReturnPhase); done?.Invoke(false); return; }
            var weapon = crateOffers[index];
            crateOffers.Clear();
            int slot = CrateSlots.SlotFor(weapon, weapons.Hand, weapons.Auto, weapons.ActiveIndex);
            var slots = weapon.IsAuto ? weapons.Auto : weapons.Hand;
            var replaced = slot >= 0 && slot < slots.Length ? slots[slot] : null;
            // TakeOffer puts an offer where the armory would: into a free slot, else in place of the slot given.
            weapons.Offers = new RunWeapon[RunWeapons.ArmoryOfferCount];
            weapons.Offers[0] = weapon;
            bool taken = slot >= 0 && weapons.TakeOffer(0, slot);
            weapons.Offers = new RunWeapon[RunWeapons.ArmoryOfferCount];
            RemoveCrate(crate, true);
            if (!taken)
            {
                gm.CampaignSetPhase(crateReturnPhase);
                ui.Flash(ArenaText.Pair("这件武器放不下", "There is no room for this weapon"), 2.5f);
                CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneCrate, crate.kind + ":" + CampaignEvents.CrateNoRoom, index + 1, Day);
                done?.Invoke(false);
                return;
            }
            if (crate.kind == CrateKind.TutorialFirst) IntoMainHand(weapons, weapon);
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneCrate, crate.kind + ":" + weapon.form, index + 1, Day);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, PlayerPoint());
            string message = crate.kind == CrateKind.TutorialFirst
                ? ArenaText.Pair($"{weapon.FormName}拿在主手上，小手枪换到了另一只手", $"The {weapon.FormName} is in your main hand, and the pistol is in the other")
                : replaced == null ? ArenaText.Pair($"{weapon.FormName}放进了{(weapon.IsAuto ? "自动武器栏" : "空着的手")}", $"The {weapon.FormName} went into {(weapon.IsAuto ? "an auto slot" : "your free hand")}")
                : ArenaText.Pair($"{weapon.FormName}换下了{replaced.FormName}", $"The {weapon.FormName} replaces the {replaced.FormName}");
            gm.CampaignShowWeaponCard(weapon, () =>
            {
                if (!Running) return;
                gm.CampaignSetPhase(crateReturnPhase);
                ui.Flash(message, 3.5f);
                // The first weapon taken from a crate (2026-11-10): with its card read, the hero says it is not what it was.
                if (!changedWeaponSaid) { changedWeaponSaid = true; Say(Line.T10); }
                done?.Invoke(true);
            });
        }

        void LeaveCrate()
        {
            var crate = crateOpen;
            var done = crateDone;
            crateOpen = null; crateDone = null;
            crateOffers.Clear();
            RemoveCrate(crate, false);
            if (crate != null) CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneCrate, crate.kind + ":" + CampaignEvents.CrateLeft, 0f, Day);
            gm.CampaignSetPhase(crateReturnPhase);
            done?.Invoke(false);
        }

        /// <summary>Day 0's first crate: the weapon taken sits in the hand in use, the pistol moves to the other hand.</summary>
        static void IntoMainHand(RunWeapons weapons, RunWeapon weapon)
        {
            int at = System.Array.IndexOf(weapons.Hand, weapon), main = weapons.ActiveIndex;
            if (at < 0 || at == main) return;
            (weapons.Hand[at], weapons.Hand[main]) = (weapons.Hand[main], weapons.Hand[at]);
        }

        /// <summary>
        /// Runs after every Update of each frame (a coroutine resumes after them): the afternoon's and the night's crates, whose stages are
        /// ticked in V2Campaign.Night.cs. Its prompt goes straight onto the prompt line, after the campaign's own frame has set it. It
        /// also lets a crate glint now and then, and clears what is left of Day 0's tutorial once day 1 has begun.
        /// </summary>
        IEnumerator CrateLoop()
        {
            while (this)
            {
                yield return null;
                if (!Running || !gm) continue;
                if (Day > 0) ForgetTutorial();
                TickLateTeaching();
                var p = gm.player;
                if (!p) continue;
                foreach (var c in crates)
                    if (Random.value < Time.deltaTime * 2.5f && Query.Flat(c.position - p.Position).sqrMagnitude < 1600f)
                        Fx.Pop(c.position + new Vector3(Random.Range(-.6f, .6f), Random.Range(.8f, 1.6f), Random.Range(-.6f, .6f)), new Color(1f, .82f, .4f), Random.Range(.4f, .7f));
                if (Stage is not (CampaignStage.Dusk or CampaignStage.Night) || ui.ConfirmOpen || !gm.CanPlayerMove || mining != null) continue;
                if (WorldMap.Active && WorldMap.Active.TrialActive) continue;
                Crate near = null;
                float best = float.MaxValue;
                foreach (var c in crates)
                {
                    if (c.kind != CrateKind.Afternoon) continue;
                    float d = Query.Flat(c.position - p.Position).sqrMagnitude;
                    if (d < best) { best = d; near = c; }
                }
                if (near == null) continue;
                bool open = CrateUse(near, out string text);
                ui.OverridePrompt(text);
                if (open) OpenCrate(near, null);
            }
        }

        // ───── For automated checks ─────

        /// <summary>The weapon crate nearest to from, if any lies on the map.</summary>
        public bool TryNearestCrate(Vector3 from, out Vector3 at)
        {
            at = Vector3.zero;
            float best = float.MaxValue;
            foreach (var crate in crates)
            {
                float d = Query.Flat(crate.position - from).sqrMagnitude;
                if (d < best) { best = d; at = crate.position; }
            }
            return best < float.MaxValue;
        }

        /// <summary>Takes weapon index of the open crate's panel, as a click on it would.</summary>
        public void DebugTakeCrate(int index)
        {
            if (crateOpen == null) return;
            ui.CloseCrate();
            TakeFromCrate(index);
        }

        /// <summary>Leaves the open crate, as its leave button would.</summary>
        public void DebugLeaveCrate()
        {
            if (crateOpen == null) return;
            ui.CloseCrate();
            LeaveCrate();
        }
    }
}
