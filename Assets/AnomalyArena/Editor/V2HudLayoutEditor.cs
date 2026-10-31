using UnityEditor;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
                                                                                                                         
    [CustomEditor(typeof(V2HudLayout))]
    internal sealed class V2HudLayoutEditor : Editor
    {
        static bool showAuto = true, showHand = true, showSkill = true, showInside, showAbove;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var layout = (V2HudLayout)target;

            EditorGUILayout.HelpBox("Any number you change is applied to the HUD in the scene at once. Save the scene with Ctrl+S when you are happy; for the other scenes, right-click the component title, Copy Component, then Paste Component Values in that scene.", MessageType.Info);

            EditorGUILayout.LabelField("Overall", EditorStyles.boldLabel);
            Field("overallScale", "Overall scale", "1 = current size; 1.2 = whole interface 20% larger; 0.9 = 10% smaller.");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Vector2Field(new GUIContent("Canvas reference resolution (automatic)", "Derived from the overall scale; no need to edit."), layout.referenceResolution);
            Field("matchWidthOrHeight", "Width / height match", "0 = scale by screen width only, 1 = by height only, 0.5 = half each. Usually left alone.");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Top XP header (level · XP · pending cards)", EditorStyles.boldLabel);
            Field("xpHeaderHeight", "Header height", "");
            Field("xpFont", "Text font size", "");
            Field("xpBarHeight", "Progress bar thickness", "");
            Field("xpPipSize", "Pending-card pip size", "");
            Field("xpHeaderMargin", "Total side margin", "Too small clips the text.");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Top-left resource bars (health / stamina)", EditorStyles.boldLabel);
            Field("resourcePanelScale", "Scale", "1 = original size.");
            Field("resourcePanelLeft", "From screen left", "");
            Field("resourcePanelTop", "From screen top", "The top XP header is about 48 tall; smaller values overlap it.");
            Field("resourcePanelBackground", "Show dark backdrop", "Off leaves only the health and stamina bars.");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Wave texts at the top centre", EditorStyles.boldLabel);
            Field("waveClockFont", "Wave and countdown font size", "\"Wave 01 / 14 · 18 s\".");
            Field("batchFont", "Alive / pending font size", "\"10 alive · 0 pending\" (alive in red, pending in yellow).");
            Field("bannerFont", "Banner font size", "Large text such as wave start and black-hole notices.");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Bottom row (canvas units; canvas width = 1440 ÷ overall scale)", EditorStyles.boldLabel);
            showAuto = Group(showAuto, "auto", "Auto weapon slots (the two at bottom left)", "From screen left");
            showHand = Group(showHand, "hand", "Hand weapon slots (the two in the centre)", "Offset of the whole group from the screen's centre line (positive is right)");
            showSkill = Group(showSkill, "skill", "Skill slots (the three at bottom right)", "From screen right");

            showInside = EditorGUILayout.Foldout(showInside, "Layout inside a slot (shared by the three groups; usually left alone)", true);
            if (showInside)
            {
                EditorGUI.indentLevel++;
                if (layout.gameObject.scene.name == V2Hud.ChineseEditionScene)
                    EditorGUILayout.HelpBox("At run time the Chinese edition rearranges the inside of the slots so names and detail lines show in full: the program places the name box, the weapon and skill icons and the detail row. So \"Name row from top\" has no effect at run time, and the program also decides the element icon size and weapon icon size on auto slots and the largest element icon on skill slots. The edit view still shows the boxes from before this rearrangement; see the real result in Play mode.", MessageType.Info);
                Field("padding", "Padding", "Distance between the text and the slot's side frame, which is about 14 wide itself.");
                Field("keyTop", "Key row from top", "Distance of the \"Auto 1 / Hand 1 [LMB] / Skill 1 [1]\" line from the slot's top edge.");
                Field("titleTop", "Name row from top", "");
                Field("detailBottom", "Detail row from bottom", "Distance of the \"Common · Mk 1 · No element\" line from the slot's bottom edge; too small and it overlaps the frame.");
                Field("detailHeight", "Detail row height", "");
                Field("cooldownBottom", "Cooldown bar from bottom", "");
                Field("cooldownHeight", "Cooldown bar thickness", "");
                Field("weaponIconSize", "Weapon icon width × height", "The small weapon picture at the right of the slot.");
                Field("skillIconSize", "Skill icon size", "");
                Field("elementIconSize", "Element icon size", "");
                EditorGUI.indentLevel--;
            }

            showAbove = EditorGUILayout.Foldout(showAbove, "Elements above the bottom row", true);
            if (showAbove)
            {
                EditorGUI.indentLevel++;
                Field("chargeBarAboveRow", "Charge bar from the hand slots' top", "");
                Field("chargeBarOffsetX", "Charge bar horizontal offset", "Relative to the centre of the hand slot group; positive is right. The group's own offset is under \"Hand weapon slots\" above.");
                Field("chargeBarWidth", "Charge bar width", "0 = keep the width saved in the scene (610 at the moment).");
                Field("hintAboveRow", "Hint from the hand slots' top", "The hint line at the bottom centre, such as \"Rifle · Reverse Shot\".");
                Field("hintOffsetX", "Hint horizontal offset", "Relative to the centre of the hand slot group; positive is right.");
                Field("hintFontSize", "Hint font size", "");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply layout")) { serializedObject.ApplyModifiedProperties(); layout.Apply(); }
                if (GUILayout.Button("Restore defaults"))
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
            Field(property + ".size", "Width × height", "Slot width and height.");
            Field(property + ".bottom", "From screen bottom", "");
            Field(property + ".edge", edgeLabel, "");
            Field(property + ".gap", "Gap between slots", "");
            Field(property + ".keyFont", "Key row font size (min – max)", "Text scales within the range to fit the slot.");
            Field(property + ".titleFont", "Name font size (min – max)", "");
            Field(property + ".detailFont", "Detail row font size (min – max)", "");
            Field(property + ".keyColor", "Key row colour", "");
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
