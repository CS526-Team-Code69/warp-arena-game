using System;
using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/V2 Interstellar Art Set")]
    public class V2ArtSet : ScriptableObject
    {
        public Texture2D themeBackground;
        public Texture2D player;
        public Texture2D floor;
        public Texture2D wall;
        [Tooltip("Concept reference, not a sliced runtime sprite atlas.")] public Texture2D monsterConceptSheet;
        [Tooltip("Fire, Poison, Frost, Lightning, Earth, Light, Void, in that order.")]
        public Sprite[] elementIcons = new Sprite[7];
        public Sprite weaponSlotFrame;
        public Sprite cardFrame;
        [Tooltip("WeaponForm enum order, one appearance per form rather than per anomaly.")]
        public Sprite[] weaponSprites = new Sprite[17];
        [Tooltip("SkillId order, including the three Greater Void skills.")]
        public Sprite[] skillIcons = new Sprite[16];
        [Tooltip("StatId order.")]
        public Sprite[] statIcons = new Sprite[14];
        [Tooltip("Infusion, Power, Tempo, Reach, Twist, Tame.")]
        public Sprite[] axisIcons = new Sprite[6];
        public Sprite[] bossPhaseMarks = new Sprite[3];
        public Sprite telegraphRing, telegraphCleave, telegraphCharge;
        public Sprite phaseDagger, plasmaBlob, frostShard, fireball, sporePod;
        public Sprite flameField, blizzardField, poisonField;
        [Header("Radial and confirmed-hit presentation")]
        public Sprite holeCore, holeSwirl, holeGravity, holePortal;
        public Sprite hitImpact, knifeReturnMarker;
        public Texture2D blackHoleSquare;
        public Texture2D gravitySmall;
        public Texture2D gravityLarge;
        public Texture2D portal;
        public Texture2D strikeMark;
        [Tooltip("Final transparent per-enemy images. A concept sheet is never assigned here.")]
        public EnemyImage[] enemyImages = Array.Empty<EnemyImage>();

        [Serializable]
        public struct EnemyImage
        {
            public string id;
            public Sprite sprite;
            public Texture2D windup;
            public float height;
        }
    }
}
