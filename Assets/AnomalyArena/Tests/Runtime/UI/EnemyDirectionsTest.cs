using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// The screen-edge markers for monsters off the screen (V2EnemyDirections) point at the monsters. The game's camera is orthographic
    /// and tilted, so ground far toward the bottom of the screen lies behind the camera's plane; a marker for a monster there used to
    /// be turned round, as a perspective camera needs, and pointed at the opposite edge.
    /// </summary>
    [TestFixture]
    public class EnemyDirectionsTest
    {
        Camera made;

        [TearDown]
        public void TearDown()
        {
            if (made) Object.DestroyImmediate(made.gameObject);
        }

        /// <summary>A camera as CameraRig's corner view sets it: looking down 50 degrees, turned 45, 30 away from the point it looks at.</summary>
        Camera GameView(bool orthographic, Vector3 focus)
        {
            made = new GameObject("EnemyDirectionsTest camera").AddComponent<Camera>();
            made.enabled = false;
            made.orthographic = orthographic;
            made.orthographicSize = 10f;
            made.fieldOfView = 35f;
            made.transform.rotation = Quaternion.Euler(50f, 45f, 0f);
            made.transform.position = focus + Vector3.up - made.transform.forward * 30f;
            return made;
        }

        [Test]
        public void OrthographicCamera_MonsterOffAnyEdgeAtAnyDistance_DirectionPointsAtIt()
        {
            Vector3 focus = new(12f, 0f, -7f);
            var cam = GameView(true, focus);
            int behind = 0;
            foreach (float distance in new[] { 30f, 50f, 60f, 90f, 140f })
                for (int bearing = 0; bearing < 360; bearing += 15)
                {
                    Vector3 offset = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward * distance;
                    Vector3 monster = focus + offset;
                    Vector3 seen = monster - cam.transform.position;
                    var truth = new Vector2(Vector3.Dot(seen, cam.transform.right), Vector3.Dot(seen, cam.transform.up));
                    if (Vector3.Dot(seen, cam.transform.forward) <= 0f) behind++;
                    Assert.That(V2EnemyDirections.OffScreenDirection(cam, monster, out var direction), Is.True, $"{distance} cells away at {bearing} degrees is off the screen");
                    Assert.That(Vector2.Angle(direction, truth), Is.LessThan(1f),
                        $"a monster {distance} cells away at {bearing} degrees lies {Mathf.Atan2(truth.y, truth.x) * Mathf.Rad2Deg:0} degrees on the screen; the marker's direction is {Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg:0}");
                }
            Assert.That(behind, Is.GreaterThan(10), "the cases include monsters behind the camera's plane (far toward the bottom of the screen)");
        }

        [Test]
        public void OrthographicCamera_MonsterFarBelowTheScreen_FallsToALowerMarker()
        {
            Vector3 focus = Vector3.zero;
            var cam = GameView(true, focus);
            // Straight toward the bottom of the screen on the ground: against the camera's heading.
            Vector3 down = -Query.Flat(cam.transform.forward).normalized;
            Vector3 monster = focus + down * 70f;
            Assert.That(cam.WorldToViewportPoint(monster).z, Is.LessThan(0f), "the monster stands behind the camera's plane");
            Assert.That(V2EnemyDirections.OffScreenDirection(cam, monster, out var direction), Is.True);
            Assert.That(direction.y, Is.LessThan(0f), "the direction points down the screen");
            Assert.That(V2EnemyDirections.Bin(direction), Is.EqualTo(6), "it falls to the marker at the bottom edge");
        }

        [Test]
        public void MonsterOnTheScreen_HasNoMarker()
        {
            var cam = GameView(true, Vector3.zero);
            Assert.That(V2EnemyDirections.OffScreenDirection(cam, new Vector3(3f, 0f, 2f), out _), Is.False);
        }

        [Test]
        public void PerspectiveCamera_PointBehindIt_IsTurnedBack()
        {
            var cam = GameView(false, Vector3.zero);
            Vector3 behind = cam.transform.position - cam.transform.forward * 20f + cam.transform.right * 8f;
            Vector3 viewport = cam.WorldToViewportPoint(behind);
            Assume.That(viewport.z, Is.LessThan(0f), "the point is behind the camera");
            Assert.That(V2EnemyDirections.OffScreenDirection(cam, behind, out var direction), Is.True);
            Assert.That(direction.x, Is.GreaterThan(0f), "a point behind and to the right of a perspective camera is shown to the right");
        }

        [Test]
        public void Bin_EightMarkers_FortyFiveDegreesEach()
        {
            Assert.That(new[] { Vector2.right, new Vector2(1f, 1f), Vector2.up, new Vector2(-1f, 1f), Vector2.left, new Vector2(-1f, -1f), Vector2.down, new Vector2(1f, -1f) }
                .Select(V2EnemyDirections.Bin).ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
        }
    }
}
