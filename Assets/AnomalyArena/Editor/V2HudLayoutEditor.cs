using UnityEditor;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>Chinese, annotated Inspector for V2HudLayout so the HUD can be tuned without reading the code.</summary>
    [CustomEditor(typeof(V2HudLayout))]
    internal sealed class V2HudLayoutEditor : Editor
    {
        static bool showAuto = true, showHand = true, showSkill = true, showInside, showAbove;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var layout = (V2HudLayout)target;

            EditorGUILayout.HelpBox("改任何数字都会立刻套用到场景里的 HUD。满意后 Ctrl+S 保存场景；别的场景（EnemyLab）要用同样的数值，可右键组件标题 Copy Component，再到那个场景 Paste Component Values。", MessageType.Info);

            EditorGUILayout.LabelField("整体", EditorStyles.boldLabel);
            Field("overallScale", "整体放大倍率", "1 = 现在的大小；1.2 = 整个界面放大 20%；0.9 = 缩小 10%。");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Vector2Field(new GUIContent("画布参考分辨率（自动）", "由整体放大倍率算出，不用手改。"), layout.referenceResolution);
            Field("matchWidthOrHeight", "宽高适配", "0 = 只按屏幕宽度缩放，1 = 只按高度，0.5 = 各一半。一般不动。");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("顶部经验条（等级 · 经验 · 待选卡组）", EditorStyles.boldLabel);
            Field("xpHeaderHeight", "整条高度", "");
            Field("xpFont", "文字字号", "");
            Field("xpBarHeight", "进度条粗细", "");
            Field("xpPipSize", "待选卡组小方块边长", "");
            Field("xpHeaderMargin", "左右留白总和", "太小会裁掉文字。");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("左上角资源条（生命 / 体力）", EditorStyles.boldLabel);
            Field("resourcePanelScale", "放大倍率", "1 = 原大小。");
            Field("resourcePanelLeft", "离屏幕左边", "");
            Field("resourcePanelTop", "离屏幕顶边", "顶部经验条高约 48，数值小于它会和经验条重叠。");
            Field("resourcePanelBackground", "显示深色底板", "关掉就只剩生命条和体力条。");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("顶部中间的波次文字", EditorStyles.boldLabel);
            Field("waveClockFont", "波次与倒计时字号", "“第 01 / 14 波 · 18 秒”。");
            Field("batchFont", "活敌/待生成字号", "“活敌 10 · 待生成 0”（活敌红色、待生成黄色）。");
            Field("bannerFont", "横幅字号", "波次开始、黑洞提示等大字。");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("底栏（单位是画布单位，画布宽 = 1440 ÷ 整体放大倍率）", EditorStyles.boldLabel);
            showAuto = Group(showAuto, "auto", "自动武器格（左下两个）", "离屏幕左边");
            showHand = Group(showHand, "hand", "手持武器格（中间两个）", "整组相对屏幕中线的偏移（正数向右）");
            showSkill = Group(showSkill, "skill", "技能格（右下三个）", "离屏幕右边");

            showInside = EditorGUILayout.Foldout(showInside, "格子内部的排版（三组共用，一般不用动）", true);
            if (showInside)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("运行时会重新排格子内部（中英文都一样），让名字和细节行完整显示：名称框、武器和技能图标、细节行的位置由程序算。所以“名称行离顶部”在运行时不起作用，自动格的元素图标边长和武器图标大小、技能格的元素图标边长上限也由程序定。编辑状态看到的还是重排之前的框，实际效果要进入运行才看得到。", MessageType.Info);
                Field("padding", "内边距", "文字离格子左右边框的距离，边框本身约 14。");
                Field("keyTop", "按键行离顶部", "“自动1 / 手持1 [左键] / 技能1 [1]” 这一行离格子顶部的距离。");
                Field("titleTop", "名称行离顶部", "");
                Field("detailBottom", "细节行离底部", "“普通 · 等级 1 · 无元素” 这一行离格子底边的距离，太小会压到边框。");
                Field("detailHeight", "细节行高度", "");
                Field("cooldownBottom", "冷却条离底部", "");
                Field("cooldownHeight", "冷却条粗细", "");
                Field("weaponIconSize", "武器图标 宽 × 高", "格子右侧的武器小图。");
                Field("skillIconSize", "技能图标边长", "");
                Field("elementIconSize", "元素图标边长", "");
                EditorGUI.indentLevel--;
            }

            showAbove = EditorGUILayout.Foldout(showAbove, "底栏上方的元素", true);
            if (showAbove)
            {
                EditorGUI.indentLevel++;
                Field("chargeBarAboveRow", "蓄力条离手持格顶部", "");
                Field("chargeBarOffsetX", "蓄力条左右偏移", "相对手持格整组的中心，正数向右；手持格整组本身的偏移在上面“手持武器格”里改。");
                Field("chargeBarWidth", "蓄力条宽度", "0 = 保持场景里的宽度（目前 610）。");
                Field("hintAboveRow", "提示文字离手持格顶部", "底部中间那行“步枪 · 反向射击”之类的提示。");
                Field("hintOffsetX", "提示文字左右偏移", "相对手持格整组的中心，正数向右。");
                Field("hintFontSize", "提示文字字号", "");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("套用布局")) { serializedObject.ApplyModifiedProperties(); layout.Apply(); }
                if (GUILayout.Button("恢复默认值"))
                {
                    Undo.RecordObject(layout, "HUD layout defaults");
                    var hud = layout.hud;
                    var fresh = new GameObject("tmp").AddComponent<V2HudLayout>();
                    EditorUtility.CopySerialized(fresh, layout);
                    DestroyImmediate(fresh.gameObject);
                    layout.hud = hud;
                    EditorUtility.SetDirty(layout);
                    layout.Apply();
                }
            }
            if (serializedObject.ApplyModifiedProperties()) layout.Apply();
        }

        bool Group(bool open, string property, string title, string edgeLabel)
        {
            open = EditorGUILayout.Foldout(open, title, true);
            if (!open) return false;
            EditorGUI.indentLevel++;
            Field(property + ".size", "宽 × 高", "格子的宽和高。");
            Field(property + ".bottom", "离屏幕底边", "");
            Field(property + ".edge", edgeLabel, "");
            Field(property + ".gap", "格子之间的间距", "");
            Field(property + ".keyFont", "按键行字号（最小 – 最大）", "文字会在区间内自动缩放以塞进格子。");
            Field(property + ".titleFont", "名称字号（最小 – 最大）", "");
            Field(property + ".detailFont", "细节行字号（最小 – 最大）", "");
            Field(property + ".keyColor", "按键行颜色", "");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
            return true;
        }

        void Field(string property, string label, string tooltip)
        {
            var prop = serializedObject.FindProperty(property);
            if (prop != null) EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip), true);
        }
    }
}
