using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{

    public sealed class V2Tutorial : MonoBehaviour
    {
        const float PanelW = 920f, PanelH = 620f, WindowW = 760f, WindowH = 320f;
        static readonly Color Soft = new(.86f, .90f, .96f), Faint = new(.62f, .72f, .85f), Cyan = new(.50f, .86f, 1f);
        static readonly Color Player = new(.35f, .78f, 1f), Enemy = new(.95f, .38f, .36f), Bullet = new(1f, .84f, .29f), Void = new(.62f, .38f, 1f);
        static readonly Color KeyIdle = new(.55f, .65f, .80f, .55f), Floor = new(.03f, .06f, .12f, .96f), GridLine = new(.35f, .55f, .8f, .07f);

        static V2Tutorial instance;
        static Sprite circle, ring, pill, panel, button, buttonPrimary;
        static bool shownOnArrival;
        static int closedFrame = -1;

        public static bool IsOpen => instance && instance.gameObject.activeSelf;

        public static bool HoldsInput => IsOpen || Time.frameCount == closedFrame;
        public static int Page => instance ? instance.page : -1;
        public const int PageCount = 7;

        static (string title, string line, Func<Demo> demo)[] Pages() => InCampaign ? StoryPages() : ClassicPages();

        static (string title, string line, Func<Demo> demo)[] ClassicPages() => new (string, string, Func<Demo>)[]
        {
            (ArenaText.Pair("MOVE","MOVE"), ArenaText.Pair("WASD to move.","WASD to move."), () => new MoveDemo()),
            AimPage(),
            (ArenaText.Pair("SPRINT","SPRINT"), ArenaText.Pair("Hold Shift while moving to sprint. It drains stamina.","Hold Shift while moving to sprint. It drains stamina."), () => new SprintDemo()),
            (ArenaText.Pair("SWAP & SKILLS","SWAP & SKILLS"), ArenaText.Pair("Right click swaps the two hand slots. 1, 2, 3 cast skills.","Right click swaps the two hand slots. 1, 2, 3 cast skills."), () => new SkillDemo()),
            (ArenaText.Pair("USE THE ARENA","USE THE ARENA"), ArenaText.Pair("Knock enemies into walls and black holes.","Knock enemies into walls and black holes."), () => new TerrainDemo()),
            (ArenaText.Pair("PUT THE PANEL AWAY","PUT THE PANEL AWAY"),
                ArenaText.Pair("Between waves, Tab puts the card panel away so you can walk and reach a portal. Tab again brings it back.","Between waves, Tab puts the card panel away so you can walk and reach a portal. Tab again brings it back."),
                () => new BuildWalkDemo()),
            PausePage(),
        };

        static (string title, string line, Func<Demo> demo)[] StoryPages()
        {
            int days = PlannedDays;
            return new (string, string, Func<Demo>)[]
            {
                (ArenaText.Pair("STRANDED","STRANDED"),
                    ArenaText.Pair($"Your ship crashed on an alien planet. Spend {days} days repairing it and filling its tank, then escape.",$"Your ship crashed on an alien planet. Spend {days} days repairing it and filling its tank, then escape."),
                    () => new StrandedDemo(days)),
                (ArenaText.Pair("MOVE & SPRINT","MOVE & SPRINT"),
                    ArenaText.Pair("WASD to move. Hold Shift while moving to sprint; it drains stamina.","WASD to move. Hold Shift while moving to sprint; it drains stamina."),
                    () => new MoveSprintDemo()),
                AimPage(),
                (ArenaText.Pair("SWAP & SKILLS","SWAP & SKILLS"),
                    ArenaText.Pair("Right click swaps the two hand slots. Skills learned at the Energy Lab are cast with 1, 2, 3.","Right click swaps the two hand slots. Skills learned at the Energy Lab are cast with 1, 2, 3."),
                    () => new SkillDemo()),
                (ArenaText.Pair("GATHER","GATHER"),
                    ArenaText.Pair("Afternoon: hold E on alloy ore, no lamp needed. Night: F sets a lamp; hold E in its light on dark matter.","Afternoon: hold E on alloy ore, no lamp needed. Night: F sets a lamp; hold E in its light on dark matter."),
                    () => new GatherDemo()),
                (ArenaText.Pair("BASE & UPGRADES","BASE & UPGRADES"),
                    ArenaText.Pair("Afternoon or night, E at the base door: build rooms, buy upgrades, repair the ship. Each takes effect at once.","Afternoon or night, E at the base door: build rooms, buy upgrades, repair the ship. Each takes effect at once."),
                    () => new BaseDemo()),
                PausePage(),
            };
        }

        static (string title, string line, Func<Demo> demo) AimPage() => (ArenaText.Pair("AIM & FIRE","AIM & FIRE"),
            ArenaText.Pair("Aim with the mouse. Left click uses the held weapon; hold it to charge or keep firing, depending on the weapon.","Aim with the mouse. Left click uses the held weapon; hold it to charge or keep firing, depending on the weapon."),
            () => new FireDemo());

        static (string title, string line, Func<Demo> demo) PausePage() => (ArenaText.Pair("PAUSE","PAUSE"),
            ArenaText.Pair("Esc pauses. The pause screen lists the keys and has How to Play, Restart Run and Quit to Title.","Esc pauses. The pause screen lists the keys and has How to Play, Restart Run and Quit to Title."),
            () => new PauseDemo());

        static bool InCampaign => storyOnly || V2Campaign.Active && V2Campaign.Active.Running;
        static bool storyOnly;

        static int PlannedDays
        {
            get
            {
                var campaign = V2Campaign.Active ? V2Campaign.Active : GameManager.Instance ? GameManager.Instance.GetComponent<V2Campaign>() : null;
                return campaign && campaign.rules ? Mathf.Max(1, campaign.rules.days) : 7;
            }
        }

        static string Footnote() => InCampaign
            ? ArenaText.Pair("Read the goal at the top left and follow the blue marker. Other keys show on screen when needed.","Read the goal at the top left and follow the blue marker. Other keys show on screen when needed.")
            : ArenaText.Pair("On a mid-wave card panel, Esc picks later and T opens it again. Other keys are shown on screen when needed.","On a mid-wave card panel, Esc picks later and T opens it again. Other keys are shown on screen when needed.");

        Font font;
        float heldScale = 1f;
        int page;
        float pageTime;
        Demo demo;
        RectTransform window;
        Text heading, line, counter, nextLabel, footnote;
        Image[] dots;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot()
        {
            instance = null;
            shownOnArrival = false;
            closedFrame = -1;
            storyOnly = false;
        }

        public static void ShowFirstTime(Font font)
        {
            if (shownOnArrival) return;
            shownOnArrival = true;
            Show(font);
        }

        public static void ForgetShown() => shownOnArrival = false;

        public static void Show(Font font) => ShowPages(font, false);

        public static void ShowStory(Font font) => ShowPages(font, true);

        static void ShowPages(Font font, bool story)
        {
            storyOnly = story;
            if (!instance) instance = Build();
            instance.font = font ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var label in instance.GetComponentsInChildren<Text>(true)) label.font = instance.font;
            if (!IsOpen) instance.heldScale = Time.timeScale;
            Time.timeScale = 0f;
            instance.gameObject.SetActive(true);
            instance.Open(0);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            closedFrame = Time.frameCount;
            Time.timeScale = instance.heldScale;
            instance.gameObject.SetActive(false);
        }

        public static void Next()
        {
            if (!IsOpen) return;
            if (instance.page >= PageCount - 1) Close();
            else instance.Open(instance.page + 1);
        }

        static void Skip()
        {
            V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
            Close();
        }

        static void Turn()
        {
            V2Audio.Instance?.PlayUI(ArenaUiSound.Select);
            Next();
        }

        void Update()
        {
            if (Keyboard.current is { } keyboard && keyboard.escapeKey.wasPressedThisFrame) { Skip(); return; }
            pageTime += Time.unscaledDeltaTime;
            demo?.Tick(pageTime);
        }

        void Open(int index)
        {
            page = index;
            pageTime = 0f;
            for (int i = window.childCount - 1; i >= 0; i--) Destroy(window.GetChild(i).gameObject);
            Grid(window);
            var entry = Pages()[index];
            demo = entry.demo();
            demo.Build(window, font);
            demo.Tick(0f);
            heading.text = entry.title;
            line.text = entry.line;
            counter.text = $"{index + 1} / {PageCount}";
            bool last = index == PageCount - 1;
            nextLabel.text = last ? ArenaText.Pair("GOT IT","GOT IT") : ArenaText.Pair("NEXT","NEXT");
            footnote.text = Footnote();
            footnote.gameObject.SetActive(last);
            for (int i = 0; i < dots.Length; i++) dots[i].color = i == index ? Cyan : KeyIdle;
        }

        static V2Tutorial Build()
        {
            circle ??= Disc(64, false);
            ring ??= Disc(64, true);
            pill ??= Load("V5/UI/ui-pill", 16f);
            panel ??= Load("V5/UI/ui-panel", 112f);
            button ??= Load("V5/UI/ui-button", 40f);
            buttonPrimary ??= Load("V5/UI/ui-button-primary", 40f);

            var root = new GameObject("HowToPlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 810f);
            scaler.matchWidthOrHeight = .5f;
            var tutorial = root.AddComponent<V2Tutorial>();

            var dim = NewImage(root.transform, "Dim", null, new Color(0f, 0f, 0f, .74f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            var backing = NewImage(root.transform, "Backing", null, new Color(.03f, .05f, .10f, .96f)).rectTransform;
            backing.sizeDelta = new Vector2(PanelW - 56f, PanelH - 56f);
            var body = NewImage(root.transform, "Panel", panel, panel ? Color.white : new Color(.05f, .08f, .16f, .97f), Image.Type.Sliced, 2f).rectTransform;
            body.sizeDelta = new Vector2(PanelW, PanelH);

            var title = NewText(body, "Title", 30, Soft, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.text = ArenaText.Pair("HOW TO PLAY","HOW TO PLAY");
            Place(title.rectTransform, 48f, 26f, 400f, 44f);
            tutorial.counter = NewText(body, "Counter", 18, Faint, FontStyle.Normal, TextAnchor.MiddleRight);
            Place(tutorial.counter.rectTransform, PanelW - 48f - 200f, 26f, 200f, 44f);

            var frame = NewImage(body, "Window", null, Floor).rectTransform;
            Place(frame, (PanelW - WindowW) * .5f, 84f, WindowW, WindowH);
            frame.gameObject.AddComponent<RectMask2D>();
            tutorial.window = frame;

            tutorial.heading = NewText(body, "Heading", 26, Cyan, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(tutorial.heading.rectTransform, 48f, 418f, PanelW - 96f, 36f);
            tutorial.line = NewText(body, "Line", 21, Soft, FontStyle.Normal, TextAnchor.MiddleCenter);
            Place(tutorial.line.rectTransform, 48f, 454f, PanelW - 96f, 32f);

            tutorial.line.verticalOverflow = VerticalWrapMode.Truncate;
            tutorial.line.resizeTextForBestFit = true;
            tutorial.line.resizeTextMinSize = 14;
            tutorial.line.resizeTextMaxSize = 21;
            tutorial.footnote = NewText(body, "Footnote", 15, Faint, FontStyle.Normal, TextAnchor.MiddleCenter);
            tutorial.footnote.text = Footnote();
            Place(tutorial.footnote.rectTransform, 48f, 486f, PanelW - 96f, 24f);

            tutorial.dots = new Image[PageCount];
            for (int i = 0; i < PageCount; i++)
            {
                tutorial.dots[i] = NewImage(body, "Dot" + i, circle, KeyIdle);
                Place(tutorial.dots[i].rectTransform, PanelW * .5f - PageCount * 11f + i * 22f + 4f, 520f, 12f, 12f);
            }

            var skip = MakeButton(body, "Skip", button, ArenaText.Pair("SKIP  [Esc]","SKIP  [Esc]"), Soft, Skip);
            Place(skip, 48f, PanelH - 48f - 52f, 220f, 52f);
            var next = MakeButton(body, "Next", buttonPrimary, ArenaText.Pair("NEXT","NEXT"), Color.white, Turn);
            Place(next, PanelW - 48f - 280f, PanelH - 48f - 52f, 280f, 52f);
            tutorial.nextLabel = next.GetComponentInChildren<Text>();

            root.SetActive(false);
            return tutorial;
        }

        static void Grid(RectTransform window)
        {
            for (int i = 1; i < 8; i++)
            {
                var v = NewImage(window, "Grid", null, GridLine).rectTransform;
                v.sizeDelta = new Vector2(1f, WindowH);
                v.anchoredPosition = new Vector2(-WindowW * .5f + i * WindowW / 8f, 0f);
            }
            for (int i = 1; i < 4; i++)
            {
                var h = NewImage(window, "Grid", null, GridLine).rectTransform;
                h.sizeDelta = new Vector2(WindowW, 1f);
                h.anchoredPosition = new Vector2(0f, -WindowH * .5f + i * WindowH / 4f);
            }
        }

        abstract class Demo
        {
            protected RectTransform window;
            protected Font font;
            public void Build(RectTransform into, Font with) { window = into; font = with; Build(); }
            protected abstract void Build();
            public abstract void Tick(float time);

            protected Image Dot(Color colour, float size, Vector2 at) => Shape(circle, colour, size, at);
            protected Image Ring(Color colour, float size, Vector2 at) => Shape(ring, colour, size, at);
            protected Image Shape(Sprite sprite, Color colour, float size, Vector2 at)
            {
                var image = NewImage(window, "Shape", sprite, colour);
                image.rectTransform.sizeDelta = Vector2.one * size;
                image.rectTransform.anchoredPosition = at;
                return image;
            }

            protected Image Bar(Color colour, Vector2 size, Vector2 at)
            {
                var image = NewImage(window, "Bar", null, colour);
                image.rectTransform.sizeDelta = size;
                image.rectTransform.anchoredPosition = at;
                return image;
            }

            protected Key Cap(string label, Vector2 at, float width = 44f)
            {
                var cap = NewImage(window, "Key " + label, pill, KeyIdle, Image.Type.Sliced, 2f);
                cap.rectTransform.sizeDelta = new Vector2(width, 40f);
                cap.rectTransform.anchoredPosition = at;
                var text = NewText(cap.rectTransform, "Label", 18, Soft, FontStyle.Bold, TextAnchor.MiddleCenter);
                text.font = font;
                text.text = label;
                Stretch(text.rectTransform);
                return new Key { cap = cap, text = text };
            }

            protected Text Label(string value, int size, Color colour, Vector2 at, float width = 200f, RectTransform parent = null, TextAnchor anchor = TextAnchor.MiddleCenter)
            {
                var text = NewText(parent ? parent : window, "Label", size, colour, FontStyle.Bold, anchor);
                text.font = font;
                text.text = value;
                text.rectTransform.sizeDelta = new Vector2(width, size + 10f);
                text.rectTransform.anchoredPosition = at;
                return text;
            }

            protected (RectTransform rect, CanvasGroup fade) Sheet(Vector2 size, Vector2 at)
            {
                var frame = NewImage(window, "Sheet", button, button ? Color.white : new Color(.12f, .22f, .34f), Image.Type.Sliced, 2f);
                frame.rectTransform.sizeDelta = size;
                frame.rectTransform.anchoredPosition = at;
                Part(frame.rectTransform, null, new Color(.03f, .05f, .10f, .96f), size - Vector2.one * 12f, Vector2.zero);
                return (frame.rectTransform, frame.gameObject.AddComponent<CanvasGroup>());
            }

            protected static Image Part(RectTransform parent, Sprite sprite, Color colour, Vector2 size, Vector2 at)
            {
                var image = NewImage(parent, "Part", sprite, colour, Image.Type.Sliced, 2f);
                image.rectTransform.sizeDelta = size;
                image.rectTransform.anchoredPosition = at;
                return image;
            }

            protected (Image left, Image right) Mouse(Vector2 at)
            {
                var body = NewImage(window, "Mouse", pill, KeyIdle, Image.Type.Sliced, 1f);
                body.rectTransform.sizeDelta = new Vector2(38f, 56f);
                body.rectTransform.anchoredPosition = at;
                var left = Bar(KeyIdle, new Vector2(15f, 20f), at + new Vector2(-9f, 14f));
                var right = Bar(KeyIdle, new Vector2(15f, 20f), at + new Vector2(9f, 14f));
                return (left, right);
            }

            protected (RectTransform rect, CanvasGroup fade) Group(Vector2 at)
            {
                var group = new GameObject("Group", typeof(RectTransform), typeof(CanvasGroup));
                var rect = (RectTransform)group.transform;
                rect.SetParent(window, false);
                rect.anchoredPosition = at;
                return (rect, group.GetComponent<CanvasGroup>());
            }

            protected Text Caption(string value, int size, Color colour, float y) =>
                Label(value, size, colour, new Vector2(-10f, y), 700f, null, TextAnchor.MiddleLeft);

            protected static float Loop(float time, float period) => Mathf.Repeat(time, period);
            protected static float Ease(float from, float to, float time) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, time));
            protected static void Light(Image part, bool on) => part.color = on ? Cyan : KeyIdle;
        }

        sealed class Key
        {
            public Image cap;
            public Text text;
            public void Light(bool on)
            {

                cap.color = on ? new Color(.6f, .95f, 1f, 1f) : KeyIdle;
                text.color = on ? Cyan : Soft;
                cap.rectTransform.localScale = Vector3.one * (on ? .9f : 1f);
            }
        }

        sealed class MoveDemo : Demo
        {
            Image player;
            Key w, a, s, d;
            protected override void Build()
            {
                w = Cap("W", new Vector2(-280f, -40f));
                a = Cap("A", new Vector2(-328f, -88f));
                s = Cap("S", new Vector2(-280f, -88f));
                d = Cap("D", new Vector2(-232f, -88f));
                player = Dot(Player, 34f, Vector2.zero);
            }

            public override void Tick(float time)
            {
                float t = Loop(time, 4f);
                int side = Mathf.FloorToInt(t);
                float along = Ease(0f, 1f, t - side);
                Vector2[] corners = { new(40f, -80f), new(40f, 80f), new(240f, 80f), new(240f, -80f) };
                player.rectTransform.anchoredPosition = Vector2.Lerp(corners[side], corners[(side + 1) % 4], along);
                w.Light(side == 0); d.Light(side == 1); s.Light(side == 2); a.Light(side == 3);
            }
        }

        sealed class FireDemo : Demo
        {
            static readonly Vector2 From = new(-160f, -30f);
            static readonly Vector2[] Targets = { new(120f, 90f), new(250f, -20f), new(110f, -110f) };
            Image gun, cursor, cross;
            Image[] enemies, bullets;
            Image left, right;
            Text mouseLabel;
            string aimLabel, fireLabel;
            protected override void Build()
            {
                enemies = new Image[Targets.Length];
                for (int i = 0; i < Targets.Length; i++) enemies[i] = Dot(Enemy, 30f, Targets[i]);
                bullets = new Image[6];
                for (int i = 0; i < bullets.Length; i++) bullets[i] = Dot(Bullet, 9f, From);
                gun = Bar(Player, new Vector2(34f, 7f), From);
                gun.rectTransform.pivot = new Vector2(0f, .5f);
                Dot(Player, 34f, From);
                cursor = Ring(Soft, 30f, Targets[0]);
                cross = Dot(Soft, 6f, Targets[0]);
                (left, right) = Mouse(new Vector2(-320f, -100f));
                aimLabel = ArenaText.Pair("move to aim","move to aim");
                fireLabel = ArenaText.Pair("hold left click","hold left click");
                mouseLabel = Label(aimLabel, 14, Faint, new Vector2(-300f, -146f), 130f);
            }

            public override void Tick(float time)
            {
                const float each = 1.6f;
                float t = Loop(time, each * Targets.Length);
                int target = Mathf.FloorToInt(t / each);
                float local = t - target * each;
                Vector2 previous = Targets[(target + Targets.Length - 1) % Targets.Length];
                Vector2 aim = Vector2.Lerp(previous, Targets[target], Ease(0f, .45f, local));
                cursor.rectTransform.anchoredPosition = cross.rectTransform.anchoredPosition = aim;
                Vector2 toward = aim - From;
                gun.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg);
                bool firing = local is > .5f and < 1.25f;
                Light(left, firing); Light(right, false);
                mouseLabel.text = firing ? fireLabel : aimLabel;
                for (int i = 0; i < bullets.Length; i++)
                {
                    float fired = .5f + i * .13f, progress = (local - fired) / .22f;
                    bool flying = fired < 1.25f && progress is > 0f and < 1f;
                    bullets[i].enabled = flying;
                    if (flying) bullets[i].rectTransform.anchoredPosition = Vector2.Lerp(From + toward.normalized * 30f, Targets[target], progress);
                }
                for (int i = 0; i < enemies.Length; i++)
                {
                    float hit = i == target ? Mathf.InverseLerp(1.2f, 1.5f, local) : 0f;
                    bool flash = i == target && local is > .7f and < 1.2f && Mathf.Repeat(local, .13f) < .05f;
                    enemies[i].color = flash ? Color.white : Enemy;
                    enemies[i].rectTransform.localScale = Vector3.one * (1f - hit);
                }
            }
        }

        sealed class SprintDemo : Demo
        {
            Image player, fill;
            Image[] trail;
            Key shift;
            protected override void Build()
            {
                trail = new Image[4];
                for (int i = 0; i < trail.Length; i++) trail[i] = Dot(Player, 34f - i * 3f, Vector2.zero);
                player = Dot(Player, 34f, Vector2.zero);
                shift = Cap("Shift", new Vector2(-300f, -100f), 84f);
                Bar(new Color(1f, 1f, 1f, .08f), new Vector2(220f, 10f), new Vector2(60f, -120f));
                fill = Bar(new Color(1f, .82f, .3f), new Vector2(220f, 10f), new Vector2(60f, -120f));
                fill.rectTransform.pivot = new Vector2(0f, .5f);
                fill.rectTransform.anchoredPosition = new Vector2(-50f, -120f);
                Label(ArenaText.Pair("STAMINA","STAMINA"), 13, Faint, new Vector2(60f, -100f));
            }

            public override void Tick(float time)
            {
                float t = Loop(time, 4f);
                bool sprint = t is > 1.2f and < 2.4f;
                float distance = Mathf.Min(t, 1.2f) * 110f + Mathf.Clamp(t - 1.2f, 0f, 1.2f) * 220f + Mathf.Max(0f, t - 2.4f) * 110f;
                float x = -260f + distance * .9f;
                player.rectTransform.anchoredPosition = new Vector2(x, 30f);
                for (int i = 0; i < trail.Length; i++)
                {
                    trail[i].rectTransform.anchoredPosition = new Vector2(x - (i + 1) * 24f, 30f);
                    trail[i].color = new Color(Player.r, Player.g, Player.b, sprint ? .32f / (i + 1) : 0f);
                }
                shift.Light(sprint);
                float stamina = t < 1.2f ? 1f : t < 2.4f ? Mathf.Lerp(1f, .12f, (t - 1.2f) / 1.2f) : Mathf.Lerp(.12f, 1f, (t - 2.4f) / 1.6f);
                fill.rectTransform.sizeDelta = new Vector2(220f * stamina, 10f);

                fill.color = !sprint && stamina < .3f ? new Color(.6f, .6f, .65f) : new Color(1f, .82f, .3f);
            }
        }

        sealed class SkillDemo : Demo
        {
            static readonly Vector2 Centre = new(60f, 30f);
            Image burst, slot1, slot2, left, right;
            Image[] enemies;
            Key one, two, three;
            protected override void Build()
            {
                enemies = new Image[6];
                for (int i = 0; i < enemies.Length; i++) enemies[i] = Dot(Enemy, 26f, Centre);
                burst = Ring(Void, 10f, Centre);
                Dot(Player, 34f, Centre);
                slot1 = NewImage(window, "Hand 1", pill, KeyIdle, Image.Type.Sliced, 2f);
                slot1.rectTransform.sizeDelta = new Vector2(110f, 34f);
                slot1.rectTransform.anchoredPosition = new Vector2(-30f, -120f);
                slot2 = NewImage(window, "Hand 2", pill, KeyIdle, Image.Type.Sliced, 2f);
                slot2.rectTransform.sizeDelta = new Vector2(110f, 34f);
                slot2.rectTransform.anchoredPosition = new Vector2(90f, -120f);

                Label(InCampaign ? ArenaText.Pair("PISTOL","PISTOL") : ArenaText.Pair("RIFLE","RIFLE"), 14, Soft, new Vector2(-30f, -120f), 110f);
                Label(InCampaign ? ArenaText.Pair("ANOMALY","ANOMALY") : ArenaText.Pair("KNIFE","KNIFE"), 14, Soft, new Vector2(90f, -120f), 110f);
                (left, right) = Mouse(new Vector2(-320f, -100f));
                Label(ArenaText.Pair("right click: swap","right click: swap"), 14, Faint, new Vector2(-300f, -146f), 130f);
                Label(ArenaText.Pair("skill keys","skill keys"), 14, Faint, new Vector2(298f, -146f), 130f);
                one = Cap("1", new Vector2(250f, -110f));
                two = Cap("2", new Vector2(298f, -110f));
                three = Cap("3", new Vector2(346f, -110f));
            }

            public override void Tick(float time)
            {
                float t = Loop(time, 4.4f);
                bool clicking = t is > .3f and < .55f || t is > 1.3f and < 1.55f;
                Light(left, false); Light(right, clicking);
                bool second = t is > .4f and < 1.4f;
                slot1.color = second ? KeyIdle : Cyan;
                slot2.color = second ? Cyan : KeyIdle;
                one.Light(t is > 2f and < 2.3f); two.Light(false); three.Light(false);
                float wave = Mathf.InverseLerp(2.05f, 2.7f, t);
                burst.enabled = wave is > 0f and < 1f;
                burst.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(20f, 320f, wave);
                burst.color = new Color(Void.r, Void.g, Void.b, 1f - wave);
                float push = Ease(2.1f, 2.6f, t) * (1f - Ease(3.2f, 4.3f, t));
                for (int i = 0; i < enemies.Length; i++)
                {
                    float angle = i * Mathf.PI * 2f / enemies.Length + .3f;
                    enemies[i].rectTransform.anchoredPosition = Centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .7f) * Mathf.Lerp(78f, 160f, push);
                }
            }
        }

        sealed class TerrainDemo : Demo
        {
            static readonly Vector2 Shooter = new(-60f, -100f), Hole = new(220f, 20f);
            static readonly Vector2 HoleStart = new(60f, 20f), WallStart = new(-160f, 70f);
            const float Wall = -330f;
            Image intoHole, intoWall, wall, gun, bullet, core;
            Image[] swirl;
            Text slam;
            protected override void Build()
            {
                wall = Bar(new Color(.55f, .65f, .8f, .6f), new Vector2(16f, 260f), new Vector2(Wall - 8f, 0f));
                swirl = new Image[3];
                for (int i = 0; i < swirl.Length; i++) swirl[i] = Ring(Void, 60f, Hole);
                core = Dot(new Color(.04f, .0f, .1f), 56f, Hole);
                intoHole = Dot(Enemy, 30f, HoleStart);
                intoWall = Dot(Enemy, 30f, WallStart);
                bullet = Dot(Bullet, 10f, Shooter);
                gun = Bar(Player, new Vector2(34f, 7f), Shooter);
                gun.rectTransform.pivot = new Vector2(0f, .5f);
                Dot(Player, 34f, Shooter);
                slam = Label(ArenaText.Pair("SLAM!","SLAM!"), 18, Bullet, new Vector2(Wall + 50f, 70f), 120f);
            }

            public override void Tick(float time)
            {
                float t = Loop(time, 4.8f);
                for (int i = 0; i < swirl.Length; i++)
                {
                    float phase = Mathf.Repeat(time * .6f + i / 3f, 1f);
                    swirl[i].rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(150f, 56f, phase);
                    swirl[i].color = new Color(Void.r, Void.g, Void.b, .7f * phase);
                }

                bool first = t < 2.2f;
                Vector2 target = first ? HoleStart : WallStart;
                Vector2 toward = target - Shooter;
                gun.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg);
                float shot = first ? Mathf.InverseLerp(.3f, .5f, t) : Mathf.InverseLerp(2.5f, 2.7f, t);
                bullet.enabled = shot is > 0f and < 1f;
                bullet.rectTransform.anchoredPosition = Vector2.Lerp(Shooter, target, shot);

                float slide = Ease(.5f, 1.5f, t);
                intoHole.rectTransform.anchoredPosition = Vector2.Lerp(HoleStart, Hole, slide);
                intoHole.rectTransform.localScale = Vector3.one * (1f - Mathf.InverseLerp(1.2f, 1.6f, t));
                core.rectTransform.localScale = Vector3.one * (1f + .25f * Mathf.Sin(Mathf.InverseLerp(1.4f, 1.9f, t) * Mathf.PI));

                float knock = Ease(2.7f, 3.05f, t), rebound = Ease(3.05f, 3.4f, t);
                float x = Mathf.Lerp(WallStart.x, Wall + 15f, knock) + rebound * 50f;
                intoWall.rectTransform.anchoredPosition = new Vector2(x, WallStart.y);
                bool hit = t is > 3.05f and < 3.6f;
                intoWall.color = hit && Mathf.Repeat(t, .12f) < .06f ? Color.white : Enemy;
                intoWall.rectTransform.localScale = Vector3.one * (1f - Mathf.InverseLerp(3.6f, 3.9f, t));
                wall.color = hit ? new Color(1f, .9f, .6f, .9f) : new Color(.55f, .65f, .8f, .6f);
                slam.enabled = hit;
            }
        }

        sealed class BuildWalkDemo : Demo
        {
            static readonly Vector2 At = new(20f, 10f), From = new(20f, -20f), Portal = new(310f, 40f);
            Image player;
            Image[] swirl;
            RectTransform sheet;
            CanvasGroup fade;
            Key tab;
            Text tabLabel;
            string awayLabel, backLabel;
            protected override void Build()
            {
                swirl = new Image[3];
                for (int i = 0; i < swirl.Length; i++) swirl[i] = Ring(Void, 60f, Portal);
                Label(ArenaText.Pair("portal","portal"), 14, Faint, Portal + new Vector2(0f, -70f), 120f);
                player = Dot(Player, 30f, From);

                (sheet, fade) = Sheet(new Vector2(420f, 270f), At);
                Label(ArenaText.Pair("PICK A CARD","PICK A CARD"), 18, Soft, new Vector2(0f, 104f), 300f, sheet);
                Color[] marks = { Cyan, Bullet, Void };
                for (int i = 0; i < marks.Length; i++)
                {
                    var card = Part(sheet, pill, KeyIdle, new Vector2(116f, 170f), new Vector2(-130f + i * 130f, -20f));
                    Part(card.rectTransform, circle, marks[i], Vector2.one * 44f, new Vector2(0f, 34f));
                    Part(card.rectTransform, null, Faint, new Vector2(76f, 6f), new Vector2(0f, -22f));
                    Part(card.rectTransform, null, Faint, new Vector2(56f, 6f), new Vector2(0f, -40f));
                }

                tab = Cap("Tab", new Vector2(-300f, -100f), 64f);
                awayLabel = ArenaText.Pair("put it away","put it away");
                backLabel = ArenaText.Pair("bring it back","bring it back");
                tabLabel = Label(awayLabel, 14, Faint, new Vector2(-300f, -146f), 130f);
            }

            public override void Tick(float time)
            {
                float t = Loop(time, 6f);
                tab.Light(t is > 1f and < 1.3f || t is > 4.2f and < 4.5f);
                tabLabel.text = t is > 1.4f and < 4.5f ? backLabel : awayLabel;
                float away = Ease(1.05f, 1.4f, t) * (1f - Ease(4.25f, 4.6f, t));
                fade.alpha = 1f - away;
                sheet.anchoredPosition = At + Vector2.down * 40f * away;

                player.rectTransform.anchoredPosition = Vector2.Lerp(From, Portal, Ease(1.5f, 3.2f, t));
                player.color = new Color(Player.r, Player.g, Player.b, 1f - Ease(5.6f, 6f, t));
                for (int i = 0; i < swirl.Length; i++)
                {
                    float phase = Mathf.Repeat(time * .6f + i / 3f, 1f);
                    swirl[i].rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(110f, 44f, phase);
                    swirl[i].color = new Color(Void.r, Void.g, Void.b, .7f * phase);
                }
            }
        }

        sealed class PauseDemo : Demo
        {
            static readonly Vector2 Centre = new(60f, 0f);
            Image[] enemies;
            RectTransform sheet;
            CanvasGroup fade;
            Key esc;
            protected override void Build()
            {
                enemies = new Image[5];
                for (int i = 0; i < enemies.Length; i++) enemies[i] = Dot(Enemy, 26f, Centre);
                Dot(Player, 30f, Centre);

                (sheet, fade) = Sheet(new Vector2(560f, 300f), Centre);
                Label(ArenaText.Pair("PAUSED","PAUSED"), 20, Soft, new Vector2(0f, 118f), 300f, sheet);

                var rows = InCampaign ? V2Hud.CampaignControlRows() : V2Hud.ClassicControlRows();
                for (int i = 0; i < 5; i++)
                {
                    float y = 84f - i * 28f;
                    Part(sheet, pill, new Color(Cyan.r, Cyan.g, Cyan.b, .7f), new Vector2(150f, 24f), new Vector2(-170f, y));
                    Label(rows[i].input, 13, Cyan, new Vector2(-170f, y), 150f, sheet);
                    Label(rows[i].action, 14, Soft, new Vector2(70f, y), 300f, sheet, TextAnchor.MiddleLeft);
                }
                string[] names =
                {
                    ArenaText.Pair("RESUME","RESUME"), ArenaText.Pair("RESTART RUN","RESTART RUN"),
                    ArenaText.Pair("HOW TO PLAY","HOW TO PLAY"), ArenaText.Pair("QUIT TO TITLE","QUIT TO TITLE"),
                };
                for (int i = 0; i < names.Length; i++)
                {
                    var at = new Vector2(-198f + i * 132f, -100f);
                    var plate = i == 0 ? buttonPrimary : button;
                    Part(sheet, plate, plate ? Color.white : new Color(.12f, .22f, .34f), new Vector2(124f, 46f), at);
                    Label(names[i], 13, Soft, at, 120f, sheet);
                }

                esc = Cap("Esc", new Vector2(-300f, -100f), 60f);
                Label(ArenaText.Pair("pause / resume","pause / resume"), 14, Faint, new Vector2(-300f, -146f), 130f);
            }

            public override void Tick(float time)
            {
                float t = Loop(time, 6f);
                esc.Light(t is > 1f and < 1.3f || t is > 4.6f and < 4.9f);
                float shown = Ease(1.05f, 1.3f, t) * (1f - Ease(4.65f, 4.9f, t));
                fade.alpha = shown;
                sheet.localScale = Vector3.one * Mathf.Lerp(.96f, 1f, shown);

                float fight = t < 1.05f ? t : t < 4.65f ? 1.05f : t - 3.6f;
                for (int i = 0; i < enemies.Length; i++)
                {
                    float angle = (i / (float)enemies.Length + fight / 2.4f) * Mathf.PI * 2f;
                    enemies[i].rectTransform.anchoredPosition = Centre + new Vector2(Mathf.Cos(angle) * 180f, Mathf.Sin(angle) * 100f);
                }
            }
        }

        sealed class StrandedDemo : Demo
        {
            static readonly Vector2 Wreck = new(-150f, -86f), Away = new(560f, 250f);

            static readonly Color Broken = new(.3f, .3f, .32f), EngineColour = new(.45f, .8f, 1f), ArmorColour = new(.92f, .92f, .95f), FuelColour = new(.78f, .3f, 1f);

            const float EngineAt = .28f, ArmorAt = .57f, FuelFrom = .43f;

            const float Mended = 3.85f, Righted = 4.3f, Gone = 6.1f, Length = 7.4f;
            readonly int days;
            RectTransform ship;
            CanvasGroup shipFade, statusFade;
            Image engine, armor, engineMark, armorMark, fuelFill;
            Image[] smoke, flames;
            Text dayLabel, escape, engineDone, armorDone;
            string[] dayTexts;

            public StrandedDemo(int days) => this.days = Mathf.Max(1, days);

            protected override void Build()
            {

                Bar(new Color(.2f, .14f, .28f, .9f), new Vector2(WindowW, 40f), new Vector2(0f, 20f - WindowH * .5f));
                smoke = new Image[3];
                for (int i = 0; i < smoke.Length; i++) smoke[i] = Dot(Color.clear, 26f, Wreck);

                (ship, shipFade) = Group(Wreck);
                flames = new Image[3];
                for (int i = 0; i < flames.Length; i++) flames[i] = Part(ship, circle, Bullet, Vector2.one * (22f - i * 5f), new Vector2(-152f - i * 17f, 0f));
                Part(ship, null, new Color(.82f, .84f, .86f), new Vector2(220f, 44f), Vector2.zero);
                Part(ship, null, new Color(.9f, .55f, .2f), new Vector2(40f, 32f), new Vector2(128f, -2f));
                engine = Part(ship, null, Broken, new Vector2(26f, 40f), new Vector2(-123f, 0f));
                armor = Part(ship, null, Broken, new Vector2(150f, 8f), new Vector2(0f, 26f));

                RectTransform status;
                (status, statusFade) = Group(new Vector2(255f, 30f));
                Label(ArenaText.Pair("SHIP","SHIP"), 15, Faint, new Vector2(0f, 58f), 200f, status);
                (engineMark, engineDone) = Row(status, ArenaText.Pair("ENGINE","ENGINE"), 26f);
                (armorMark, armorDone) = Row(status, ArenaText.Pair("ARMOR","ARMOR"), -6f);
                Part(status, circle, FuelColour, Vector2.one * 16f, new Vector2(-90f, -38f));
                Label(ArenaText.Pair("FUEL","FUEL"), 16, Soft, new Vector2(-20f, -38f), 110f, status, TextAnchor.MiddleLeft);
                Part(status, null, new Color(1f, 1f, 1f, .08f), new Vector2(110f, 10f), new Vector2(50f, -38f));
                fuelFill = Part(status, null, FuelColour, new Vector2(110f, 10f), new Vector2(-5f, -38f));
                fuelFill.rectTransform.pivot = new Vector2(0f, .5f);

                dayTexts = new string[days];
                for (int d = 0; d < days; d++) dayTexts[d] = ArenaText.Pair($"DAY {d + 1} / {days}",$"DAY {d + 1} / {days}");
                dayLabel = Label(dayTexts[0], 22, Soft, new Vector2(-150f, 118f), 260f);
                escape = Label(ArenaText.Pair("ESCAPE!","ESCAPE!"), 24, Bullet, new Vector2(-150f, 118f), 260f);
            }

            (Image mark, Text done) Row(RectTransform status, string name, float y)
            {
                var mark = Part(status, circle, Broken, Vector2.one * 16f, new Vector2(-90f, y));
                Label(name, 16, Soft, new Vector2(-20f, y), 110f, status, TextAnchor.MiddleLeft);
                return (mark, Label("✓", 18, Cyan, new Vector2(8f, y), 40f, status));
            }

            public override void Tick(float time)
            {
                float t = Loop(time, Length);
                float mending = Mathf.Clamp01(t / Mended);
                bool engineOn = mending >= EngineAt, armorOn = mending >= ArmorAt;
                engine.color = engineMark.color = engineOn ? EngineColour : Broken;
                armor.color = armorMark.color = armorOn ? ArmorColour : Broken;
                engineDone.enabled = engineOn;
                armorDone.enabled = armorOn;
                fuelFill.rectTransform.sizeDelta = new Vector2(110f * Mathf.InverseLerp(FuelFrom, 1f, mending), 10f);
                dayLabel.text = dayTexts[Mathf.Min(days, 1 + Mathf.FloorToInt(mending * days)) - 1];

                float smoking = 1f - Ease(ArmorAt * Mended, ArmorAt * Mended + .4f, t);
                for (int i = 0; i < smoke.Length; i++)
                {
                    float rise = Mathf.Repeat(time * .7f + i / 3f, 1f);
                    smoke[i].rectTransform.anchoredPosition = Wreck + new Vector2(-30f + i * 24f + rise * 18f, 20f + rise * 90f);
                    smoke[i].rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(18f, 44f, rise);
                    smoke[i].color = new Color(.55f, .55f, .6f, .45f * (1f - rise) * smoking);
                }

                float lift = Mathf.InverseLerp(Righted, Gone, t);
                lift *= lift;
                ship.anchoredPosition = Vector2.Lerp(Wreck, Away, lift);
                ship.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-9f, 0f, Ease(Mended, Righted, t)) + 14f * lift);
                shipFade.alpha = Ease(0f, .3f, t);
                for (int i = 0; i < flames.Length; i++)
                {
                    flames[i].enabled = t > Righted && t < Gone;
                    flames[i].rectTransform.localScale = Vector3.one * (.85f + .25f * Mathf.Sin(time * 40f + i * 2f));
                }
                statusFade.alpha = Ease(0f, .3f, t) * (1f - Ease(Righted - .3f, Righted, t));
                dayLabel.enabled = t < Righted;
                escape.enabled = !dayLabel.enabled;
                escape.color = new Color(Bullet.r, Bullet.g, Bullet.b, Ease(Righted, Righted + .3f, t) * (1f - Ease(Length - .5f, Length - .1f, t)));
            }
        }

        sealed class MoveSprintDemo : Demo
        {
            static readonly Vector2[] Corners = { new(-110f, -10f), new(-110f, 100f), new(250f, 100f), new(250f, -10f) };

            static readonly float[] Sides = { .75f, 1.2f, .75f, 2.4f };
            const float Rest = .3f;
            Image player, fill;
            Image[] trail;
            Key w, a, s, d, shift;

            protected override void Build()
            {
                trail = new Image[4];
                for (int i = 0; i < trail.Length; i++) trail[i] = Dot(Player, 34f - i * 3f, Corners[0]);
                player = Dot(Player, 34f, Corners[0]);
                w = Cap("W", new Vector2(-300f, -52f));
                a = Cap("A", new Vector2(-348f, -100f));
                s = Cap("S", new Vector2(-300f, -100f));
                d = Cap("D", new Vector2(-252f, -100f));
                Label(ArenaText.Pair("move","move"), 14, Faint, new Vector2(-300f, -146f), 130f);
                shift = Cap("Shift", new Vector2(-150f, -100f), 84f);
                Label(ArenaText.Pair("hold to sprint","hold to sprint"), 14, Faint, new Vector2(-150f, -146f), 130f);
                Bar(new Color(1f, 1f, 1f, .08f), new Vector2(220f, 10f), new Vector2(150f, -120f));
                fill = Bar(new Color(1f, .82f, .3f), new Vector2(220f, 10f), new Vector2(40f, -120f));
                fill.rectTransform.pivot = new Vector2(0f, .5f);
                Label(ArenaText.Pair("STAMINA","STAMINA"), 13, Faint, new Vector2(150f, -100f));
            }

            public override void Tick(float time)
            {
                float length = Rest;
                foreach (float seconds in Sides) length += seconds;
                float clock = Loop(time, length) - Rest;
                int side = -1;
                float along = 0f;
                for (int i = 0; i < Sides.Length && clock >= 0f; i++)
                {
                    if (clock < Sides[i]) { side = i; along = clock / Sides[i]; break; }
                    clock -= Sides[i];
                }
                Vector2 at = side < 0 ? Corners[0] : Vector2.Lerp(Corners[side], Corners[(side + 1) % 4], along);
                player.rectTransform.anchoredPosition = at;
                bool sprint = side == 1;
                w.Light(side == 0); d.Light(side == 1); s.Light(side == 2); a.Light(side == 3);
                shift.Light(sprint);
                for (int i = 0; i < trail.Length; i++)
                {
                    trail[i].rectTransform.anchoredPosition = at - new Vector2((i + 1) * 24f, 0f);
                    trail[i].color = new Color(Player.r, Player.g, Player.b, sprint ? .32f / (i + 1) : 0f);
                }

                float refill = side == 2 ? along * Sides[2] : side == 3 ? Sides[2] + along * Sides[3] : 0f;
                float stamina = sprint ? Mathf.Lerp(1f, .12f, along) : side >= 2 ? Mathf.Lerp(.12f, 1f, refill / (Sides[2] + Sides[3])) : 1f;
                fill.rectTransform.sizeDelta = new Vector2(220f * stamina, 10f);

                fill.color = !sprint && stamina < .3f ? new Color(.6f, .6f, .65f) : new Color(1f, .82f, .3f);
            }
        }

        sealed class GatherDemo : Demo
        {
            static readonly Vector2 At = new(-70f, -10f), Fallen = new(150f, -10f), Ore = new(40f, -10f), Lamp = new(0f, 26f), Glow = new(0f, 4f);

            static readonly Vector2[] Dark = { new(40f, -10f), new(108f, -4f), new(92f, -50f) };

            static readonly Color EnergyColour = new(.78f, .45f, 1f), AlloyColour = new(.6f, .75f, .9f), DarkColour = new(.55f, .15f, .85f);
            static readonly Color LampColour = new(1f, .9f, .6f), DarkText = new(.85f, .6f, 1f);

            const float Afternoon = 3.4f, Night = 6.6f, Length = 10f;
            Image overlay, glow, player, enemy, shard, barBack, barFill;
            Image[] bullets;
            RectTransform ore, lamp, shadow;
            CanvasGroup oreFade, lampFade, shadowFade;
            RectTransform[] darkNodes;
            CanvasGroup[] darkFade;
            Text[] phases, captions, gains;
            Key e, f;

            protected override void Build()
            {

                overlay = Bar(Color.clear, new Vector2(WindowW, WindowH), Vector2.zero);
                glow = Dot(Color.clear, 280f, Glow);

                Bar(new Color(.36f, .42f, .5f), new Vector2(70f, 130f), new Vector2(-352f, 0f));
                Bar(new Color(.55f, .62f, .7f), new Vector2(6f, 130f), new Vector2(-320f, 0f));
                Label(ArenaText.Pair("BASE","BASE"), 13, Soft, new Vector2(-350f, 0f), 60f);

                (ore, oreFade) = Group(Ore);
                Node(ore, new Color(.45f, .4f, .36f), AlloyColour);
                darkNodes = new RectTransform[Dark.Length];
                darkFade = new CanvasGroup[Dark.Length];
                for (int i = 0; i < Dark.Length; i++)
                {
                    (darkNodes[i], darkFade[i]) = Group(Dark[i]);
                    Node(darkNodes[i], new Color(.16f, .1f, .22f), DarkColour);
                }
                (lamp, lampFade) = Group(Lamp);
                Part(lamp, null, new Color(.3f, .3f, .32f), new Vector2(6f, 36f), Vector2.zero);
                Part(lamp, null, new Color(1f, .9f, .55f), new Vector2(18f, 14f), new Vector2(0f, 22f));

                (shadow, shadowFade) = Group(new Vector2(255f, 64f));
                Part(shadow, circle, new Color(.06f, .03f, .1f), Vector2.one * 34f, Vector2.zero);
                Part(shadow, circle, new Color(.75f, .3f, 1f), Vector2.one * 6f, new Vector2(-6f, 5f));
                Part(shadow, circle, new Color(.75f, .3f, 1f), Vector2.one * 6f, new Vector2(6f, 5f));

                enemy = Dot(Enemy, 30f, new Vector2(400f, At.y));
                shard = Bar(EnergyColour, Vector2.one * 14f, Fallen);
                shard.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
                bullets = new Image[4];
                for (int i = 0; i < bullets.Length; i++) bullets[i] = Dot(Bullet, 9f, At);
                player = Dot(Player, 32f, At);
                barBack = Bar(new Color(1f, 1f, 1f, .12f), new Vector2(64f, 8f), Vector2.zero);
                barFill = Bar(AlloyColour, new Vector2(64f, 8f), Vector2.zero);
                barFill.rectTransform.pivot = new Vector2(0f, .5f);

                phases = new[]
                {
                    Caption(ArenaText.Pair("MORNING","MORNING"), 20, Soft, 128f),
                    Caption(ArenaText.Pair("AFTERNOON","AFTERNOON"), 20, Soft, 128f),
                    Caption(ArenaText.Pair("NIGHT","NIGHT"), 20, Soft, 128f),
                };
                captions = new[]
                {
                    Caption(ArenaText.Pair("Hold the base; kills drop alien energy","Hold the base; kills drop alien energy"), 14, Faint, 102f),
                    Caption(ArenaText.Pair("Alloy ore surfaces; it needs no lamp","Alloy ore surfaces; it needs no lamp"), 14, Faint, 102f),
                    Caption(ArenaText.Pair("Shadows fear the light: mine dark matter in it","Shadows fear the light: mine dark matter in it"), 14, Faint, 102f),
                };
                gains = new[]
                {
                    Label(ArenaText.Pair("alien energy +1","alien energy +1"), 16, EnergyColour, At, 200f),
                    Label(ArenaText.Pair("alien alloy +3","alien alloy +3"), 16, AlloyColour, Ore, 200f),
                    Label(ArenaText.Pair("dark matter +1","dark matter +1"), 16, DarkText, Dark[0], 200f),
                };
                e = Cap("E", new Vector2(-300f, -110f));
                f = Cap("F", new Vector2(-200f, -110f));

                Label(ArenaText.Pair("hold to mine","hold to mine"), 14, Faint, new Vector2(-300f, -146f), 120f);
                Label(ArenaText.Pair("set a lamp","set a lamp"), 14, Faint, new Vector2(-200f, -146f), 100f);
            }

            static void Node(RectTransform group, Color rock, Color crystal)
            {
                Part(group, null, rock, new Vector2(52f, 24f), new Vector2(0f, -4f));
                Part(group, null, crystal, new Vector2(14f, 30f), new Vector2(8f, 14f));
            }

            public override void Tick(float time)
            {
                float t = Loop(time, Length);
                int phase = t < Afternoon ? 0 : t < Night ? 1 : 2;
                for (int i = 0; i < phases.Length; i++) phases[i].enabled = captions[i].enabled = i == phase;

                float end = 1f - Ease(Length - .3f, Length, t);
                float nightfall = Ease(Night - .2f, Night + .3f, t) * end;
                overlay.color = new Color(.01f, .02f, .06f, .15f * Ease(Afternoon - .2f, Afternoon + .2f, t) * end + .57f * nightfall);
                player.color = new Color(Player.r, Player.g, Player.b, Ease(0f, .25f, t) * end);

                Vector2 foe = Vector2.Lerp(new Vector2(400f, At.y), Fallen, Mathf.InverseLerp(.2f, 1.5f, t));
                enemy.enabled = t < 1.7f;
                enemy.rectTransform.anchoredPosition = foe;
                enemy.rectTransform.localScale = Vector3.one * (1f - Mathf.InverseLerp(1.5f, 1.7f, t));
                enemy.color = t is > 1.1f and < 1.5f && Mathf.Repeat(t, .12f) < .06f ? Color.white : Enemy;
                for (int i = 0; i < bullets.Length; i++)
                {
                    float flight = (t - .9f - i * .15f) / .2f;
                    bullets[i].enabled = flight is > 0f and < 1f;
                    bullets[i].rectTransform.anchoredPosition = Vector2.Lerp(At + new Vector2(20f, 0f), foe, flight);
                }
                shard.enabled = t is > 1.6f and < 2.45f;
                shard.rectTransform.anchoredPosition = Vector2.Lerp(Fallen + new Vector2(0f, 4f * Mathf.Sin(t * 8f)), At, Ease(2.1f, 2.45f, t));
                Gain(gains[0], At, 2.45f, t);

                bool mineOre = t is > 4.1f and < 5.4f, mineDark = t is > 7.9f and < 9f;
                oreFade.alpha = phase == 1 ? Ease(Afternoon + .1f, Afternoon + .4f, t) * (1f - Ease(5.4f, 5.6f, t)) : 0f;
                ore.localScale = Vector3.one * (1f + .3f * Ease(5.4f, 5.6f, t));
                Gain(gains[1], Ore, 5.5f, t);

                float placed = Ease(7.2f, 7.35f, t), lit = Ease(7.25f, 7.6f, t) * nightfall;
                lampFade.alpha = placed * nightfall;
                lamp.anchoredPosition = Lamp + new Vector2(0f, 10f * (1f - placed));
                glow.color = new Color(LampColour.r, LampColour.g, LampColour.b, .2f * lit);
                for (int i = 0; i < darkNodes.Length; i++)
                {
                    float mined = i == 0 ? Ease(9f, 9.2f, t) : 0f;
                    darkFade[i].alpha = nightfall * Mathf.Lerp(.35f, 1f, lit) * (1f - mined);
                    darkNodes[i].localScale = Vector3.one * ((i == 0 ? 1f : .8f) + .3f * mined);
                }
                shadowFade.alpha = .9f * nightfall;
                shadow.anchoredPosition = new Vector2(255f + 34f * Mathf.Sin(time * .9f), 64f + 12f * Mathf.Sin(time * 1.7f));
                Gain(gains[2], Dark[0] + new Vector2(30f, 0f), 9.1f, t);

                e.Light(mineOre || mineDark);
                f.Light(t is > 7.1f and < 7.35f);
                Vector2 node = mineOre ? Ore : Dark[0];
                barBack.enabled = barFill.enabled = mineOre || mineDark;
                barBack.rectTransform.anchoredPosition = node + new Vector2(0f, 40f);
                barFill.rectTransform.anchoredPosition = node + new Vector2(-32f, 40f);
                barFill.rectTransform.sizeDelta = new Vector2(64f * (mineOre ? Mathf.InverseLerp(4.1f, 5.4f, t) : Mathf.InverseLerp(7.9f, 9f, t)), 8f);
                barFill.color = mineOre ? AlloyColour : DarkText;
            }

            static void Gain(Text label, Vector2 from, float at, float t)
            {
                float rise = Mathf.InverseLerp(at, at + .8f, t);
                label.enabled = t > at && rise < 1f;
                label.rectTransform.anchoredPosition = from + new Vector2(0f, 44f + 22f * rise);
                var colour = label.color;
                colour.a = 1f - rise * rise;
                label.color = colour;
            }
        }

        sealed class BaseDemo : Demo
        {
            static readonly Vector2 Centre = new(-250f, 10f), Approach = new(-250f, -135f), Door = new(-250f, -74f), SheetAt = new(130f, -2f);

            static readonly CampaignRoom[] Rooms = { CampaignRoom.Control, CampaignRoom.Armament, CampaignRoom.Energy, CampaignRoom.Ship };

            const float Buying = 1.9f, Each = .95f, Morning = 6f, Length = 9f;
            const float RowTop = 58f, RowStep = 42f;
            Image overlay, player, shield, cursor, cursorDot;
            Image[] rows;
            RectTransform sheet;
            CanvasGroup fade;
            Text flash;
            Text[] phases, captions;
            Key e;

            protected override void Build()
            {
                overlay = Bar(Color.clear, new Vector2(WindowW, WindowH), Vector2.zero);
                shield = Ring(Color.clear, 160f, Centre);

                Bar(new Color(.36f, .42f, .5f), new Vector2(120f, 120f), Centre);
                for (int side = 0; side < 4; side++)
                {
                    var normal = new Vector2(V2Campaign.SideNormal[side].x, V2Campaign.SideNormal[side].z);
                    Bar(V2Campaign.RoomColour(V2Campaign.SideRoom[side]), side % 2 == 0 ? new Vector2(120f, 6f) : new Vector2(6f, 120f), Centre + normal * 63f);
                }
                Bar(new Color(.15f, .85f, 1f), new Vector2(34f, 14f), Centre + new Vector2(0f, -60f));
                Label(ArenaText.Pair("BASE","BASE"), 16, Soft, Centre, 100f);
                player = Dot(Player, 30f, Approach);
                e = Cap("E", new Vector2(-335f, -100f));
                Label(ArenaText.Pair("at the door","at the door"), 14, Faint, new Vector2(-335f, -146f), 110f);
                phases = new[] { Caption(ArenaText.Pair("AFTERNOON OR NIGHT","AFTERNOON OR NIGHT"), 20, Soft, 128f), Caption(ArenaText.Pair("MORNING","MORNING"), 20, Soft, 128f) };
                captions = new[]
                {
                    Caption(ArenaText.Pair("E at the door: enter the base","E at the door: enter the base"), 14, Faint, 102f),
                    Caption(ArenaText.Pair("Once the shield is unlocked, E at the door tops it up with alien energy","Once the shield is unlocked, E at the door tops it up with alien energy"), 14, Faint, 102f),
                };

                (sheet, fade) = Sheet(new Vector2(480f, 290f), SheetAt);
                Label(ArenaText.Pair("BASE","BASE"), 18, Soft, new Vector2(0f, 118f), 300f, sheet);
                Label(ArenaText.Pair("Paid with alien energy, alien alloy and dark matter","Paid with alien energy, alien alloy and dark matter"), 12, Faint, new Vector2(0f, 94f), 440f, sheet);

                string[] sells =
                {
                    ArenaText.Pair("base hull and shield","base hull and shield"),
                    ArenaText.Pair("armor, weapon boosts, anomaly weapons","armor, weapon boosts, anomaly weapons"),
                    ArenaText.Pair("body, alien skills, infusions","body, alien skills, infusions"),
                    ArenaText.Pair("ship repairs, fuel from dark matter, lamps","ship repairs, fuel from dark matter, lamps"),
                };
                rows = new Image[Rooms.Length];
                for (int i = 0; i < Rooms.Length; i++)
                {
                    float y = RowTop - i * RowStep;
                    rows[i] = Part(sheet, pill, KeyIdle, new Vector2(452f, 34f), new Vector2(0f, y));
                    Part(sheet, circle, V2Campaign.RoomColour(Rooms[i]), Vector2.one * 14f, new Vector2(-206f, y));
                    Label(V2Campaign.RoomName(Rooms[i]), 14, Soft, new Vector2(-122f, y), 140f, sheet, TextAnchor.MiddleLeft);

                    var sold = Label(sells[i], 13, Faint, new Vector2(78f, y), 290f, sheet, TextAnchor.MiddleLeft);
                    sold.verticalOverflow = VerticalWrapMode.Truncate;
                    sold.resizeTextForBestFit = true;
                    sold.resizeTextMinSize = 10;
                    sold.resizeTextMaxSize = 13;
                }
                flash = Label(ArenaText.Pair("Takes effect at once","Takes effect at once"), 15, Bullet, new Vector2(0f, -112f), 300f, sheet);
                cursor = Ring(Soft, 24f, SheetAt);
                cursorDot = Dot(Soft, 6f, SheetAt);
            }

            public override void Tick(float time)
            {
                float t = Loop(time, Length);
                bool morning = t >= Morning;
                for (int i = 0; i < phases.Length; i++) phases[i].enabled = captions[i].enabled = (i == 1) == morning;
                float end = 1f - Ease(Length - .3f, Length, t);

                overlay.color = new Color(.01f, .02f, .06f, .35f * (1f - Ease(Morning - .3f, Morning, t)));
                player.rectTransform.anchoredPosition = Vector2.Lerp(Approach, Door, Ease(.15f, 1f, t));
                player.color = new Color(Player.r, Player.g, Player.b, Ease(0f, .15f, t) * end);
                e.Light(t is > 1.05f and < 1.3f || t is > 6.6f and < 6.85f);

                float closing = Buying + Rooms.Length * Each;
                float shown = Ease(1.3f, 1.55f, t) * (1f - Ease(closing, closing + .25f, t));
                fade.alpha = shown;
                int row = Mathf.FloorToInt((t - Buying) / Each);
                float local = t - Buying - row * Each;
                bool buying = row >= 0 && row < Rooms.Length;
                for (int i = 0; i < rows.Length; i++) rows[i].color = buying && i == row && local < .8f ? Cyan : KeyIdle;
                flash.color = new Color(Bullet.r, Bullet.g, Bullet.b, buying && local is > .3f and < .85f ? 1f : 0f);

                Vector2 point = row < 0 ? RowPoint(0) : row >= Rooms.Length ? RowPoint(Rooms.Length - 1)
                    : Vector2.Lerp(RowPoint(Mathf.Max(0, row - 1)), RowPoint(row), Ease(0f, .25f, local));
                cursor.rectTransform.anchoredPosition = cursorDot.rectTransform.anchoredPosition = point;
                cursor.rectTransform.localScale = Vector3.one * (buying && local is > .25f and < .4f ? .75f : 1f);
                cursor.color = cursorDot.color = new Color(Soft.r, Soft.g, Soft.b, shown);

                float ring = Ease(6.7f, 7f, t) * (1f - Ease(8.3f, 8.6f, t));
                shield.color = new Color(.5f, .9f, 1f, .85f * ring);
                shield.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(130f, 160f, ring) * (1f + .03f * Mathf.Sin(time * 6f));
            }

            static Vector2 RowPoint(int row) => SheetAt + new Vector2(214f, RowTop - row * RowStep);
        }

        static RectTransform MakeButton(Transform parent, string name, Sprite sprite, string label, Color colour, Action click)
        {
            var image = NewImage(parent, name, sprite, sprite ? Color.white : new Color(.12f, .22f, .34f), Image.Type.Sliced, 2f);
            image.raycastTarget = true;
            var control = image.gameObject.AddComponent<Button>();
            control.targetGraphic = image;
            control.onClick.AddListener(() => click());
            var text = NewText(image.rectTransform, "Label", 19, colour, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            Stretch(text.rectTransform);
            return image.rectTransform;
        }

        static Image NewImage(Transform parent, string name, Sprite sprite, Color colour, Image.Type type = Image.Type.Simple, float density = 1f)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.type = sprite ? type : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = density;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        static Text NewText(Transform parent, string name, int size, Color colour, FontStyle style, TextAnchor anchor)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = colour;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Sprite Load(string path, float border)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (!texture) return null;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * border);
        }

        static Sprite Disc(int size, bool hollow)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * .5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(r - d);
                    if (hollow) a *= Mathf.Clamp01(d - (r - 4f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        }
    }
}
