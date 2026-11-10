using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The campaign screen layer in whitebox (the interface's pictures become plain shapes and colours too, and
    /// switching back brings the pictures back). The currencies' icons and the study tiles' icons come from WhiteboxIcons; the room's
    /// picture in the base panel becomes a floor band, the room's house in its state's colour and its level marks; the memory CG's
    /// pictures become a few flat blocks per shot, with the pictures' push-in and fade. Each follows Art.Enabled at its own refresh.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // ───── Icons ─────

        /// <summary>A currency's icon in the HUD's order (energy, alloy, dark matter): the art's, or the whitebox's with the art off.</summary>
        Sprite CurrencySprite(int which)
        {
            if (!Art.Enabled) return WhiteboxIcons.CurrencyIcon((WhiteboxIcons.Currency)which);
            var art = campaign ? campaign.art : null;
            if (!art) return null;
            return which switch { 0 => art.energy, 1 => art.alloy, _ => art.dark };
        }

        /// <summary>A study tile's icon. With the art off it is the whitebox's for the tile's kind (WhiteboxIcons.ForStudy reads its id); a
        /// tile of a kind it does not know shows a plain grey square where it had a picture, so no picture is left in whitebox.</summary>
        static Sprite StudyIconOf(StudyEntry entry)
        {
            if (entry == null) return null;
            if (Art.Enabled) return entry.icon;
            var sprite = WhiteboxIcons.ForStudy(entry.id);
            return sprite ? sprite : entry.icon ? WhiteboxIcons.Unknown() : null;
        }

        // ───── The room's picture ─────

        static readonly Color RoomFloor = new(.17f, .21f, .28f), RoomPipOff = new(.22f, .26f, .32f);
        // Not built, broken: the house's tint (the colours the base bar's side marks use).
        static readonly Color RoomUnbuilt = new(.45f, .45f, .5f), RoomBroken = new(1f, .35f, .3f);
        const float RoomHouse = 150f, RoomPip = 18f, RoomPipStep = 30f;

        RectTransform roomBlocks;
        Image roomHouse;
        readonly Image[] roomPips = new Image[3];

        /// <summary>In whitebox the room's picture is a floor band, the room's house (grey before it is built, red while broken) and one
        /// mark per level, filled in the room's colour up to its level; hidden otherwise.</summary>
        void ShowRoomBlocks(CampaignRoom room, bool show)
        {
            if (!roomBlocks)
            {
                if (!show) return;
                roomBlocks = MakeRect("WhiteboxRoom", roomPicture.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var floor = Box("Floor", roomBlocks, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, RoomFloor);
                floor.anchorMin = Vector2.zero; floor.anchorMax = new Vector2(1f, .3f);
                floor.offsetMin = floor.offsetMax = Vector2.zero;
                roomHouse = Icon(roomBlocks, new Vector2(.5f, .5f), new Vector2(0f, 24f), RoomHouse);
                roomHouse.rectTransform.pivot = new Vector2(.5f, .5f);
                for (int i = 0; i < roomPips.Length; i++)
                    roomPips[i] = Box("Level" + (i + 1), roomBlocks, new Vector2(.5f, 0f), new Vector2((i - 1) * RoomPipStep, 30f),
                        new Vector2(RoomPip, RoomPip), new Vector2(.5f, .5f), RoomPipOff).GetComponent<Image>();
            }
            if (roomBlocks.gameObject.activeSelf != show) roomBlocks.gameObject.SetActive(show);
            if (!show) return;
            var state = campaign.Room(room);
            roomHouse.sprite = WhiteboxIcons.Room(room);
            roomHouse.color = state.damaged ? RoomBroken : state.level <= 0 ? RoomUnbuilt : Color.white;
            // Central Control has no levels of its own (its hull and shield show in the tab): no marks.
            for (int i = 0; i < roomPips.Length; i++)
            {
                roomPips[i].gameObject.SetActive(room != CampaignRoom.Control);
                roomPips[i].color = i < state.level ? V2Campaign.RoomColour(room) : RoomPipOff;
            }
        }

        // ───── The memory CG ─────

        // The area the blocks are drawn in: the CG picture's place as Build lays it (1080 × 720, 30 above the middle).
        static readonly Vector2 CgFrame = new(1080f, 720f);
        static readonly Color CgGround = new(.2f, .14f, .28f), CgSky = new(.08f, .09f, .13f);

        RectTransform cgBlocksFrame, cgBlocks;
        CanvasGroup cgBlocksFade;
        int cgBlocksShot = -1;
        readonly List<(RectTransform part, float degreesPerSecond)> cgSpinning = new();

        /// <summary>Called from TickCg each frame: with the art off the pictures hide and the shot's blocks show, pushed in, drifting and
        /// fading as the pictures would; with the art on the blocks hide.</summary>
        void TickWhiteboxCg(float t, Vector2 drift, float fade)
        {
            bool blocks = !Art.Enabled;
            if (cgPicture.enabled == blocks) cgPicture.enabled = !blocks;
            if (cgForeground.enabled == blocks) cgForeground.enabled = !blocks;
            if (!blocks) { if (cgBlocksFrame) cgBlocksFrame.gameObject.SetActive(false); return; }
            if (!cgBlocksFrame) BuildCgFrame();
            if (!cgBlocksFrame.gameObject.activeSelf) cgBlocksFrame.gameObject.SetActive(true);
            if (cgBlocksShot != cgShot) LayCgShot(cgShot);
            cgBlocks.localScale = Vector3.one * Mathf.Lerp(1.02f, 1.14f, t);
            cgBlocks.anchoredPosition = (new Vector2(.5f, .5f) - drift) * 90f;
            cgBlocksFade.alpha = fade;
            foreach (var (part, speed) in cgSpinning) part.localRotation = Quaternion.Euler(0f, 0f, 45f + speed * cgClock);
        }

        void BuildCgFrame()
        {
            cgBlocksFrame = MakeRect("WhiteboxCg", cgScreen, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 30f), CgFrame);
            cgBlocksFrame.gameObject.AddComponent<RectMask2D>();
            cgBlocksFade = cgBlocksFrame.gameObject.AddComponent<CanvasGroup>();
            cgBlocksFade.blocksRaycasts = false; // the whole CG screen keeps the skip click
            // Over the pictures, under the dark edges and the skip line.
            cgBlocksFrame.SetSiblingIndex(cgForeground.transform.GetSiblingIndex() + 1);
            cgBlocks = MakeRect("Shot", cgBlocksFrame, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, CgFrame);
        }

        /// <summary>
        /// The five shots of CG01 (V2Campaign.Story.cs) in flat blocks, in the whitebox's colours, measured from the frame's centre: the
        /// soldier (the player's cyan block with its white front mark) in the dark cabin by a window; the wormhole (violet and black squares
        /// turning on their corners, the small ship falling in); the crash (the tilted grey hull and orange nose on the violet ground, a
        /// red flash); the base deploying (grey body, roof and cyan door); and the pull-back (the small base beside the wreck).
        /// </summary>
        void LayCgShot(int shot)
        {
            cgBlocksShot = shot;
            cgSpinning.Clear();
            foreach (Transform child in cgBlocks) Destroy(child.gameObject);
            Color ship = Whitebox.Colour("ship"), nose = Whitebox.Colour("nose"), baseBody = Whitebox.Colour("base"), roof = Whitebox.Colour("roof"), door = Whitebox.Colour("door");
            switch (shot)
            {
                case 0:
                    CgBlock(new Color(.1f, .12f, .16f), Vector2.zero, CgFrame);
                    CgBlock(new Color(.03f, .05f, .11f), new Vector2(190f, 70f), new Vector2(560f, 320f));
                    CgBlock(Color.white, new Vector2(120f, 120f), new Vector2(6f, 6f));
                    CgBlock(Color.white, new Vector2(330f, 30f), new Vector2(5f, 5f));
                    CgBlock(new Color(.07f, .08f, .11f), new Vector2(0f, -300f), new Vector2(CgFrame.x, 120f));
                    CgBlock(Whitebox.Colour("player"), new Vector2(-290f, -110f), new Vector2(120f, 250f));
                    CgBlock(Whitebox.Colour("white"), new Vector2(-218f, -40f), new Vector2(40f, 28f));
                    break;
                case 1:
                    CgBlock(new Color(.04f, .02f, .08f), Vector2.zero, CgFrame);
                    cgSpinning.Add((CgBlock(Whitebox.Colour("void"), Vector2.zero, new Vector2(520f, 520f), 45f), 14f));
                    cgSpinning.Add((CgBlock(new Color(.62f, .34f, 1f), Vector2.zero, new Vector2(330f, 330f), 45f), -22f));
                    CgBlock(Whitebox.Colour("black"), Vector2.zero, new Vector2(200f, 200f), 45f);
                    CgBlock(ship, new Vector2(-60f, -20f), new Vector2(150f, 40f), -20f);
                    CgBlock(nose, new Vector2(16f, -48f), new Vector2(32f, 30f), -20f);
                    break;
                case 2:
                    CgBlock(CgSky, Vector2.zero, CgFrame);
                    CgBlock(CgGround, new Vector2(0f, -250f), new Vector2(CgFrame.x, 220f));
                    CgBlock(Whitebox.Colour("damaged"), new Vector2(232f, -150f), new Vector2(80f, 80f), 45f);
                    CgBlock(ship, new Vector2(-40f, -90f), new Vector2(360f, 90f), -14f);
                    CgBlock(nose, new Vector2(178f, -144f), new Vector2(90f, 70f), -14f);
                    break;
                case 3:
                    CgBlock(CgSky, Vector2.zero, CgFrame);
                    CgBlock(CgGround, new Vector2(0f, -250f), new Vector2(CgFrame.x, 220f));
                    CgBlock(baseBody, new Vector2(0f, -100f), new Vector2(380f, 200f));
                    CgBlock(roof, new Vector2(0f, 15f), new Vector2(190f, 30f));
                    CgBlock(door, new Vector2(0f, -155f), new Vector2(80f, 90f));
                    break;
                default:
                    CgBlock(new Color(.06f, .07f, .1f), Vector2.zero, CgFrame);
                    CgBlock(CgGround, new Vector2(0f, -230f), new Vector2(CgFrame.x, 260f));
                    CgBlock(baseBody, new Vector2(-250f, -130f), new Vector2(150f, 80f));
                    CgBlock(roof, new Vector2(-250f, -84f), new Vector2(76f, 12f));
                    CgBlock(door, new Vector2(-250f, -158f), new Vector2(30f, 24f));
                    CgBlock(ship, new Vector2(220f, -150f), new Vector2(200f, 50f), 10f);
                    CgBlock(nose, new Vector2(322f, -133f), new Vector2(46f, 40f), 10f);
                    CgBlock(Whitebox.Colour("alloy"), new Vector2(60f, -165f), new Vector2(14f, 30f));
                    break;
            }
        }

        RectTransform CgBlock(Color colour, Vector2 at, Vector2 size, float turn = 0f)
        {
            var block = Box("Block", cgBlocks, new Vector2(.5f, .5f), at, size, new Vector2(.5f, .5f), colour);
            block.localRotation = Quaternion.Euler(0f, 0f, turn);
            return block;
        }
    }
}
