using UnityEngine;

namespace BAIby.Character
{
    internal static class Modeling
    {
        internal static Material Matte(string html, float smoothness = 0.12f)
        {
            var shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            ColorUtility.TryParseHtmlString(html, out var tone);
            var material = new Material(shader) { color = tone };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        internal static Transform Joint(Transform parent, string name, Vector3 at)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = at;
            return joint;
        }

        internal static Transform Form(Transform parent, string name, Vector3 at,
            Vector3 scale, Material material, PrimitiveType shape = PrimitiveType.Sphere)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = scale;
            Object.Destroy(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }

        // Each row is y, half width, half depth, and depth of the cross-section center.
        internal static Transform Loft(Transform parent, string name, Vector3 at, Material material,
            params Vector4[] sections)
        {
            const int sides = 32;
            const int subdivisions = 3;
            int rings = (sections.Length - 1) * subdivisions + 1;
            var vertices = new Vector3[rings * sides + 2];
            var triangles = new int[((rings - 1) * sides * 2 + sides * 2) * 3];
            int index = 0;
            for (int ring = 0; ring < rings; ring++)
            {
                int section = Mathf.Min(ring / subdivisions, sections.Length - 2);
                float t = (ring - section * subdivisions) / (float)subdivisions;
                var lo = sections[section];
                var hi = sections[section + 1];
                var prev = sections[Mathf.Max(0, section - 1)];
                var next = sections[Mathf.Min(sections.Length - 1, section + 2)];
                var profile = 0.5f * ((2 * lo) + (-prev + hi) * t +
                    (2 * prev - 5 * lo + 4 * hi - next) * t * t +
                    (-prev + 3 * lo - 3 * hi + next) * t * t * t);
                for (int side = 0; side < sides; side++)
                {
                    float angle = 2 * Mathf.PI * side / sides;
                    int here = ring * sides + side;
                    int adjacent = ring * sides + (side + 1) % sides;
                    vertices[here] = new Vector3(Mathf.Cos(angle) * Mathf.Max(0.001f, profile.y),
                        profile.x, Mathf.Sin(angle) * Mathf.Max(0.001f, profile.z) + profile.w);
                    if (ring == rings - 1)
                        continue;

                    triangles[index++] = here;
                    triangles[index++] = here + sides;
                    triangles[index++] = adjacent;

                    triangles[index++] = adjacent;
                    triangles[index++] = here + sides;
                    triangles[index++] = adjacent + sides;
                }
            }

            var first = sections[0];
            var last = sections[sections.Length - 1];
            vertices[vertices.Length - 2] = new Vector3(0, first.x, first.w);
            vertices[vertices.Length - 1] = new Vector3(0, last.x, last.w);
            for (int side = 0; side < sides; side++)
            {
                int adjacent = (side + 1) % sides;
                triangles[index++] = vertices.Length - 2;
                triangles[index++] = side;
                triangles[index++] = adjacent;

                int top = (rings - 1) * sides;
                triangles[index++] = vertices.Length - 1;
                triangles[index++] = top + adjacent;
                triangles[index++] = top + side;
            }

            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var part = Joint(parent, name, at);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }
    }
}
