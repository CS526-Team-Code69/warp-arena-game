using UnityEngine;

namespace AnomalyArena
{

    [DisallowMultipleComponent]
    public sealed class SpireWavePresentation : MonoBehaviour
    {
        [Tooltip("Ring colour in the illustrated art: the Spire's cool light, drawn additively bright enough to bloom.")]
        public Color artColour = new(.5f, .85f, 1f, 1f);

        [Tooltip("Ring colour in whitebox: deeper, so it reads on the bare floor without any glow.")]
        public Color whiteboxColour = new(.12f, .5f, 1f, .9f);

        [Min(.05f), Tooltip("Length of the soft trail behind the front, cells.")]
        public float trailLength = 1.6f;

        [Min(0f), Tooltip("Brightness of the ring in the illustrated art; above 1 it reaches the bloom.")]
        public float artIntensity = 2.4f;

        [Min(0f), Tooltip("Seconds the ring takes to fade where it stops, at the sentry range. A wave cut short (a stun, Void Walk, the Spire's or the player's death) goes at once.")]
        public float endFade = .18f;

        [Tooltip("Flash on the player the moment the wave catches them, in the illustrated art; whitebox flashes white.")]
        public Color catchColour = new(.85f, .97f, 1f, 1f);

        [Min(0f), Tooltip("Size of the flash on the player, cells.")]
        public float catchFlashSize = 2.6f;

        Enemy enemy;
        LightWaveRing ring;
        int drawnWave = -1, seenWaves = -1, seenCatches = -1;
        float fadeLeft;

        public LightWaveRing Ring => ring;

        void LateUpdate()
        {
            if (!enemy) enemy = GetComponent<Enemy>();
            if (!enemy) return;
            bool spire = enemy.IsAlive && enemy.definition && enemy.definition.kind == EnemyKind.Spire;
            Flash(spire);
            if (!spire)
            {
                Hide();
                return;
            }

            if (!ring)
            {
                ring = LightWaveRing.Create(transform, "SpireLightWave");
                ring.shadowWalls = true;
            }

            ring.shadowReach = enemy.LightWaveReach;                                                           
            if (enemy.LightWaveActive)
            {
                drawnWave = enemy.LightWaveSequence;
                fadeLeft = endFade;
                Draw(1f);
                return;
            }

            bool reachedEnd = drawnWave == enemy.LightWaveSequence && enemy.LightWaveRadius >= enemy.LightWaveReach - .001f;
            if (reachedEnd && fadeLeft > 0f)
            {
                var game = GameManager.Instance;
                fadeLeft -= game ? game.WorldDeltaTime : Time.deltaTime;
                if (fadeLeft > 0f)
                {
                    Draw(fadeLeft / Mathf.Max(.01f, endFade));
                    return;
                }
            }

            Hide();
        }

        void OnDisable() => Hide();

        void Hide()
        {
            drawnWave = -1;
            if (ring) ring.Hide();
        }

        void Draw(float alpha)
        {
            bool art = V5Vfx.On;
            var colour = art ? artColour : whiteboxColour;
            colour.a *= alpha;
            ring.Draw(enemy.LightWaveCentre, enemy.LightWaveRadius, trailLength, colour, art ? artIntensity : 1f);
        }

        void Flash(bool spire)
        {
            bool first = seenCatches < 0;
            bool newWave = !first && enemy.LightWaveSequence != seenWaves;
            bool caught = !first && enemy.LightWaveCatches != seenCatches;
            seenWaves = enemy.LightWaveSequence;
            seenCatches = enemy.LightWaveCatches;
            if (!spire) return;
            if (newWave && V5Vfx.On) Fx.Pop(Query.AtCastHeight(enemy.Position) + Vector3.up * .6f, artColour, 2.2f);
            if (caught) FlashPlayer();
        }

        void FlashPlayer()
        {
            var game = GameManager.Instance;
            var player = game ? game.player : null;
            if (!player || !player.IsAlive) return;
            bool art = V5Vfx.On;
            Fx.Pop(Query.AtCastHeight(player.Position) + Vector3.up * .3f, art ? catchColour : Color.white, catchFlashSize);
            Fx.Ring(new Vector3(player.Position.x, .09f, player.Position.z), Mathf.Max(1f, player.Radius * 2.4f), art ? catchColour : whiteboxColour, .3f);
        }
    }
}
