using System.Collections.Generic;
using UnityEngine;

namespace DroneSimulator.Environment
{
    /// <summary>
    /// Physical obstacle geometry per terrain preset. Urban = street-canyon
    /// building blocks; Rural = scattered rocks, boulders and poles. All
    /// primitives get real colliders, so obstacles affect navigation
    /// (drones physically collide) — not just visuals.
    /// Deterministic seed keeps a preset's layout stable between rebuilds.
    /// </summary>
    public sealed class ObstacleField : MonoBehaviour
    {
        [SerializeField] private int seed = 1337;
        [Tooltip("Half-extent of the obstacle area on X (arena is ~25 m).")]
        [Min(5f)] [SerializeField] private float extentX = 24f;
        [Tooltip("Arena depth on Z (arena spans roughly -5..45).")]
        [Min(5f)] [SerializeField] private float extentZ = 44f;
        [Tooltip("Keep-out circle around the launch/gate area at the origin.")]
        [Min(0f)] [SerializeField] private float centerClearRadius = 11f;

        private static readonly Color[] BuildingColors =
        {
            new Color(0.46f, 0.48f, 0.51f),
            new Color(0.55f, 0.53f, 0.50f),
            new Color(0.40f, 0.44f, 0.49f),
            new Color(0.60f, 0.58f, 0.55f)
        };

        private static readonly Color[] RuralColors =
        {
            new Color(0.36f, 0.42f, 0.30f),
            new Color(0.48f, 0.44f, 0.35f),
            new Color(0.52f, 0.52f, 0.50f)
        };

        private static readonly Dictionary<Color, Material> s_materials = new Dictionary<Color, Material>();
        private GameObject root;

        public int ObstacleCount => root != null ? root.transform.childCount : 0;

        public void Rebuild(TerrainPreset preset)
        {
            Clear();

            root = new GameObject("Obstacles");
            root.transform.SetParent(transform, false);

            var rng = new System.Random(seed + (int)preset * 7919);
            if (preset == TerrainPreset.Urban)
            {
                BuildUrban(rng);
            }
            else
            {
                BuildRural(rng);
            }
        }

        public void Clear()
        {
            if (root == null)
            {
                return;
            }

            Destroy(root);
            root = null;
        }

        private void BuildUrban(System.Random rng)
        {
            // Grid of blocks with jittered footprints, gaps form street canyons.
            const float cell = 9f;
            for (float z = 4f; z <= extentZ; z += cell)
            {
                for (float x = -extentX; x <= extentX; x += cell)
                {
                    float px = x + Jitter(rng, 2f);
                    float pz = z + Jitter(rng, 2f);
                    if (new Vector2(px, pz).magnitude < centerClearRadius)
                    {
                        continue;
                    }

                    float w = 3.5f + (float)rng.NextDouble() * 3f;
                    float d = 3.5f + (float)rng.NextDouble() * 3f;
                    float h = 5f + (float)rng.NextDouble() * 17f;
                    var color = BuildingColors[rng.Next(BuildingColors.Length)];
                    CreateBlock(
                        new Vector3(px, h * 0.5f, pz),
                        new Vector3(w, h, d),
                        color,
                        $"Bldg_{rng.Next(1000, 9999)}");
                }
            }
        }

        private void BuildRural(System.Random rng)
        {
            const int count = 40;
            for (int i = 0; i < count; i++)
            {
                float px = Jitter(rng, extentX);
                float pz = 4f + (float)rng.NextDouble() * (extentZ - 4f);
                if (new Vector2(px, pz).magnitude < centerClearRadius)
                {
                    continue;
                }

                int kind = rng.Next(3);
                if (kind == 0)
                {
                    // Boulder: squashed sphere.
                    float r = 1.5f + (float)rng.NextDouble() * 2.5f;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = $"Rock_{rng.Next(1000, 9999)}";
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(px, r * 0.7f, pz);
                    go.transform.localScale = new Vector3(r * 2f, r * 1.4f, r * 2f);
                    go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    ApplyMaterial(go, RuralColors[2]);
                }
                else if (kind == 1)
                {
                    // Crate/stack: box obstacle.
                    float w = 2f + (float)rng.NextDouble() * 2.5f;
                    float h = 2f + (float)rng.NextDouble() * 4f;
                    CreateBlock(
                        new Vector3(px, h * 0.5f, pz),
                        new Vector3(w, h, w),
                        RuralColors[1],
                        $"Crate_{rng.Next(1000, 9999)}");
                }
                else
                {
                    // Pole/tree trunk: narrow tall cylinder.
                    float h = 5f + (float)rng.NextDouble() * 5f;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = $"Pole_{rng.Next(1000, 9999)}";
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(px, h * 0.5f, pz);
                    go.transform.localScale = new Vector3(0.7f, h * 0.5f, 0.7f);
                    ApplyMaterial(go, RuralColors[0]);
                }
            }
        }

        private void CreateBlock(Vector3 position, Vector3 scale, Color color, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            ApplyMaterial(go, color);
        }

        private static void ApplyMaterial(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterial = GetMaterial(color);
        }

        private static Material GetMaterial(Color color)
        {
            // Cached per exact color value; materials are shared (no per-obstacle
            // instances). The cache is static, so it can outlive the runtime
            // materials it holds (play mode exit destroys them) — treat a dead
            // reference as a miss and rebuild instead of assigning null.
            if (s_materials.TryGetValue(color, out var existing) && existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader != null ? shader : Shader.Find("Standard"))
            {
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            s_materials[color] = material;
            return material;
        }

        private static float Jitter(System.Random rng, float range)
        {
            return ((float)rng.NextDouble() * 2f - 1f) * range;
        }
    }
}
