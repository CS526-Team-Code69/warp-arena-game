using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The campaign screen layer in whitebox (the interface's pictures become plain shapes and colours too, and
    /// switching back brings the pictures back). The currencies' icons and the study tiles' icons come from WhiteboxIcons; the room's
    /// picture in the base panel becomes a floor band, the room's house in its state's colour and its level marks. Each follows
    /// Art.Enabled at its own refresh. The story cutscenes are staged in whitebox on their own (V2Cutscene).
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
    }
}
