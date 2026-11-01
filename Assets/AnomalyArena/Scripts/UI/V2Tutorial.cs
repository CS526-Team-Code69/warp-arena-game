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
        public const int PageCount = 5;

        static (string title, string line, Func<Demo> demo)[] Pages() => new (string, string, Func<Demo>)[]
        {
            (ArenaText.Pair("MOVE","MOVE"), ArenaText.Pair("WASD to move.","WASD to move."), () => new MoveDemo()),
            (ArenaText.Pair("AIM & FIRE","AIM & FIRE"),
                ArenaText.Pair("Aim with the mouse. Left click uses the held weapon; hold it to charge or keep firing, depending on the weapon.","Aim with the mouse. Left click uses the held weapon; hold it to charge or keep firing, depending on the weapon."),
                () => new FireDemo()),
            (ArenaText.Pair("SPRINT","SPRINT"), ArenaText.Pair("Hold Shift while moving to sprint. It drains stamina.","Hold Shift while moving to sprint. It drains stamina."), () => new SprintDemo()),
            (ArenaText.Pair("SWAP & SKILLS","SWAP & SKILLS"), ArenaText.Pair("Right click swaps the two hand slots. 1, 2, 3 cast skills.","Right click swaps the two hand slots. 1, 2, 3 cast skills."), () => new SkillDemo()),
            (ArenaText.Pair("USE THE ARENA","USE THE ARENA"), ArenaText.Pair("Knock enemies into walls and black holes.","Knock enemies into walls and black holes."), () => new TerrainDemo()),
        };

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
        }

        public static void ShowFirstTime(Font font)
        {
            if (shownOnArrival) return;
            shownOnArrival = true;
            Show(font);
        }

        public static void ForgetShown() => shownOnArrival = false;

        public static void Show(Font font)
        {
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
            tutorial.footnote.text = ArenaText.Pair("Other keys are shown on screen when you need them. Esc pauses and lists them all.","Other keys are shown on screen when you need them. Esc pauses and lists them all.");
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

            protected Text Label(string value, int size, Color colour, Vector2 at, float width = 200f)
            {
                var text = NewText(window, "Label", size, colour, FontStyle.Bold, TextAnchor.MiddleCenter);
                text.font = font;
                text.text = value;
                text.rectTransform.sizeDelta = new Vector2(width, size + 10f);
                text.rectTransform.anchoredPosition = at;
                return text;
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
                Label(ArenaText.Pair("RIFLE","RIFLE"), 14, Soft, new Vector2(-30f, -120f), 110f);
                Label(ArenaText.Pair("KNIFE","KNIFE"), 14, Soft, new Vector2(90f, -120f), 110f);
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
