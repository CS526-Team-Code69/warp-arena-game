using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    public sealed partial class V5Title
    {
        // ───── The tutorial's lessons: the story pages after the first (坠落异星 is the story, which belongs to the story mode), each with its
        // keys as they are now (ArenaKeys) and one short line; the go button opens the story pages (V2Tutorial.ShowStory) from the start,
        // and a lesson clicked opens them at its own page. ─────

        const int LessonCount = V2Tutorial.PageCount - 1;
        const float LessonGap = 6f, LessonText = 14f * Css + 42f * Css + 16f * Css;
        static readonly Color KeyInk = V5TitleKit.Rgb(26, 29, 36), HexFill = V5TitleKit.Rgb(233, 227, 214);
        // A keycap in whitebox: the flat keycap of the title's key rows, lighter than the lesson's flat plate.
        static readonly Color LessonKeyFlat = new(.2f, .24f, .31f, 1f);

        sealed class LessonView
        {
            public V5Block block;
            public Image plate, hex;
            public Text number, title, line;
            public readonly System.Collections.Generic.List<(Image cap, Text label)> keys = new();
        }

        readonly LessonView[] lessons = new LessonView[LessonCount];

        void BuildLessons()
        {
            var page = pageRoots[TutorialTab];
            float height = (BodyH - (LessonCount - 1) * LessonGap) / LessonCount;
            for (int i = 0; i < LessonCount; i++)
            {
                // Each lesson is a plate of the title that opens the pages at its own page. The keyboard's navigation passes it by: nothing
                // submits a selected plate (SourceKeysTest), and the go button below opens the pages from the start.
                int lesson = i;
                var plate = V5Plate.Make(page, "Lesson" + (i + 1), V5Plate.Look.Plate, 10f, () => OpenLesson(lesson));
                plate.Place(0f, i * (height + LessonGap), InnerW, height);
                plate.HoverPitch = 1.08f - i * .03f;
                plate.Button.navigation = new Navigation { mode = Navigation.Mode.None };
                var view = new LessonView { block = plate, plate = plate.GetComponent<Image>() };
                var root = view.block.transform;
                view.hex = V5TitleKit.Picture(root, "Number", V5TitleKit.Piece("hex-mask"), HexFill);
                V5TitleKit.Place(view.hex.rectTransform, 14f * Css, (height - 38f * Css) * .5f, 42f * Css, 38f * Css);
                view.number = V5TitleKit.Label(view.hex.transform, "Digit", 13, KeyInk, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(view.number.rectTransform);
                view.number.text = (i + 1).ToString();
                view.title = V5TitleKit.Label(root, "Title", 14, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                view.line = V5TitleKit.Label(root, "Line", 10, V5TitleKit.Ink2, TextAnchor.UpperLeft);
                V5TitleKit.Wrap(view.line);
                V5TitleKit.Place(view.line.rectTransform, LessonText, 27f, InnerW - LessonText - 18f * Css, height - 28f);
                lessons[i] = view;
            }
        }

        void RenderLessons()
        {
            bool on = Art.Enabled;
            for (int i = 0; i < LessonCount; i++)
            {
                var view = lessons[i];
                // The lesson's plate dresses itself for the art switch (V5Plate).
                view.title.text = V2Tutorial.StoryPageTitle(i + 1);
                V5TitleKit.Place(view.title.rectTransform, LessonText, 6f, 170f, 18f);
                V5TitleKit.FitWidth(view.title, 14, 10);
                float x = LessonText + Mathf.Min(170f, view.title.preferredWidth) + 9f;
                var keys = LessonKeys(i);
                while (view.keys.Count < keys.Length)
                {
                    var cap = V5TitleKit.Picture(view.block.transform, "Key" + (view.keys.Count + 1), V5TitleKit.Piece("keycap"), Color.white, V5TitleKit.PieceDensity);
                    var label = V5TitleKit.Label(cap.transform, "Label", 10, KeyInk, TextAnchor.MiddleCenter, true);
                    V5TitleKit.Stretch(label.rectTransform);
                    view.keys.Add((cap, label));
                }
                for (int k = 0; k < view.keys.Count; k++)
                {
                    var (cap, label) = view.keys[k];
                    bool shown = k < keys.Length;
                    Show(cap, shown);
                    if (!shown) continue;
                    bool dark = keys[k].Length > 2;
                    label.text = keys[k];
                    label.color = on && !dark ? KeyInk : V5TitleKit.Ink;
                    V5TitleKit.Dress(cap, on ? V5TitleKit.Piece(dark ? "keycap-dark" : "keycap") : null, on ? Color.white : LessonKeyFlat, V5TitleKit.PieceDensity);
                    float w = Mathf.Max(26f * Css, label.preferredWidth + 12f * Css);
                    V5TitleKit.Place(cap.rectTransform, x, 6f, w, 24f * Css);
                    x += w + 5f * Css;
                }
                view.line.text = LessonLine(i);
                ArenaTextFit.Refit(view.line, 10, 8);
            }
            ShowFoot(FootUse.GoAndOpenMode);
            SetGo(ArenaText.Pair("开始教学", "Start Tutorial"), ArenaText.Pair($"共 {V2Tutorial.PageCount} 页", $"{V2Tutorial.PageCount} pages"), true);
            openModeLabel.text = ArenaText.Pair("开放模式教学", "Open Mode Tutorial");
            V5TitleKit.FitWidth(openModeLabel, 13, 9);
        }

        /// <summary>A lesson's keys as they are now; the mouse's buttons and Esc are fixed.</summary>
        static string[] LessonKeys(int lesson) => lesson switch
        {
            0 => new[] { ArenaKeys.InteractKey, ArenaText.Pair("左键", "LMB"), ArenaText.Pair("右键", "RMB") },
            1 => new[] { ArenaKeys.InteractKey, ArenaKeys.Name(GameAction.Skill1) },
            2 => new[] { ArenaKeys.InteractKey, ArenaKeys.LampKey },
            3 => new[] { ArenaText.Pair("左键", "LMB") },
            4 => new[] { ArenaKeys.SprintKey, ArenaKeys.InteractKey },
            _ => new[] { ArenaKeys.InteractKey },
        };

        public static string LessonLine(int lesson) => V2Tutorial.StoryPageLine(lesson + 1);

        void OpenTutorial()
        {
            V5UiSound.Play(V5UiSound.Ui.Start);
            V2Tutorial.ShowStory(ArenaText.Font);
        }

        /// <summary>The foot's second button: the open mode's pages (moving, aiming, swapping, the arena, the card panel and the pause),
        /// which the lessons above do not cover.</summary>
        static void OpenOpenModeTutorial()
        {
            V5UiSound.Play(V5UiSound.Ui.Start);
            V2Tutorial.ShowClassic(ArenaText.Font);
        }

        /// <summary>A lesson clicked: the story pages open at its own page (the first lesson is the second page, after the story's
        /// background).</summary>
        static void OpenLesson(int lesson)
        {
            V5UiSound.Play(V5UiSound.Ui.Start);
            V2Tutorial.ShowStory(ArenaText.Font, lesson + 1);
        }
    }
}
