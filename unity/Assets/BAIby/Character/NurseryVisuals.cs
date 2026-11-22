using BAIby.Simulation;
using UnityEngine;
using static BAIby.Character.Modeling;

namespace BAIby.Character
{
    public sealed class NurseryVisuals : MonoBehaviour
    {
        private BabyRig baby;
        private Transform toy;
        private Transform pillow;
        private Camera nurseryCamera;
        private Reaction reaction;
        private float reactionStarted;
        private bool canSit;
        private float sitBlend;
        private float aspect;

        public static NurseryVisuals Create()
        {
            var visuals = new GameObject("BAIby Nursery").AddComponent<NurseryVisuals>();
            visuals.Build();
            return visuals;
        }

        public void Present(bool adopted, bool canSitAndReach, Reaction newReaction)
        {
            baby.Root.gameObject.SetActive(adopted);
            canSit = canSitAndReach;
            if (newReaction != Reaction.None)
            {
                reaction = newReaction;
                reactionStarted = Time.unscaledTime;
            }
        }

        private void Build()
        {
            nurseryCamera = new GameObject("Nursery Camera").AddComponent<Camera>();
            nurseryCamera.transform.SetParent(transform, false);
            nurseryCamera.tag = "MainCamera";
            nurseryCamera.clearFlags = CameraClearFlags.SolidColor;
            nurseryCamera.backgroundColor = new Color(.68f, .75f, .66f);
            nurseryCamera.orthographic = true;
            nurseryCamera.transform.position = new Vector3(0.35f, 2.30f, -6.1f);
            nurseryCamera.transform.LookAt(new Vector3(0, 0.75f, 0));
            nurseryCamera.farClipPlane = 35;
            nurseryCamera.allowHDR = false;
            nurseryCamera.allowMSAA = true;
            FrameCamera();

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.54f, .50f, .46f);
            Light("Afternoon window", new Color(1f, .88f, .76f), 1.35f, new Vector3(48, -36, 0));
            Light("Soft bounce", new Color(.80f, .90f, 1f), .48f, new Vector3(18, 46, 0));
            Light("Rim", new Color(1f, .91f, .79f), .65f, new Vector3(55, 165, 0));

            var wall = Matte("#A7B6A3");
            var cream = Matte("#F2E5CF");
            var wood = Matte("#CB9C78");
            var darkWood = Matte("#AC7E60");
            var moss = Matte("#587871");
            var pale = Matte("#8EAD9D");
            var saffron = Matte("#E6B36C");
            var clay = Matte("#BE755D");

            Box("Sage back wall", new Vector3(0, 1.8f, 2.4f), new Vector3(16, 5, .14f), wall);
            Box("Wooden floor", new Vector3(0, -.095f, -.2f), new Vector3(16, .15f, 12), wood);
            Box("Baseboard", new Vector3(0, .09f, 2.29f), new Vector3(16, .16f, .09f), cream);
            for (int board = -7; board <= 7; board++)
                Box("Floorboard seam", new Vector3(board * .62f, -.018f, 0),
                    new Vector3(.010f, .005f, 10), darkWood);

            Form(transform, "Oval nursery rug", new Vector3(0, .008f, -.21f),
                new Vector3(3.45f, .072f, 2.59f), moss);
            Form(transform, "Woven rug inset", new Vector3(0, .032f, -.21f),
                new Vector3(3.26f, .039f, 2.40f), pale);
            Form(transform, "Quilt base", new Vector3(0, .076f, -.095f),
                new Vector3(1.51f, .15f, 1.56f), darkWood);
            Form(transform, "Quilt cushion", new Vector3(0, .137f, -.075f),
                new Vector3(1.53f, .16f, 1.57f), cream);
            pillow = Form(transform, "Back support cushion", new Vector3(0, .255f, .275f),
                new Vector3(.92f, .32f, .57f), saffron);
            pillow.localRotation = Quaternion.Euler(-18, 0, 0);

            Box("Oak ledge", new Vector3(1.25f, 1.18f, 2.02f), new Vector3(1.54f, .085f, .43f), wood);
            for (int book = 0; book < 4; book++)
            {
                Box("Picture book", new Vector3(.73f + book * .11f, 1.36f, 1.98f),
                    new Vector3(.09f, .28f + (book % 3) * .032f, .22f),
                    book % 2 == 0 ? moss : clay).localRotation =
                    Quaternion.Euler(0, 0, book == 3 ? -10 : 0);
            }
            Box("Picture frame", new Vector3(-.89f, 1.76f, 2.28f), new Vector3(.63f, .67f, .05f), darkWood);
            Box("Print paper", new Vector3(-.89f, 1.76f, 2.249f), new Vector3(.54f, .58f, .01f), cream);
            Form(transform, "Sun illustration", new Vector3(-.89f, 1.79f, 2.235f),
                new Vector3(.25f, .25f, .014f), saffron);

            toy = Joint(transform, "Wooden rattle", new Vector3(.59f, .22f, -.49f));
            Form(toy, "Rattle stem", Vector3.zero, new Vector3(.032f, .111f, .032f), wood, PrimitiveType.Cylinder);
            Form(toy, "Rattle cap", new Vector3(0, -.102f, 0), new Vector3(.082f, .081f, .083f), moss);
            Form(toy, "Rattle head", new Vector3(0, .114f, 0), new Vector3(.135f, .138f, .136f), saffron);
            Form(toy, "Rattle collar", new Vector3(0, .069f, 0), new Vector3(.10f, .06f, .10f), clay);
            baby = new BabyRig(transform);
            baby.Root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Mathf.Approximately(aspect, nurseryCamera.aspect))
                FrameCamera();

            if (!baby.Root.gameObject.activeSelf)
                return;

            float t = Time.unscaledTime;
            float elapsed = t - reactionStarted;
            float response = Mathf.Clamp01(elapsed * 6f) * Mathf.Clamp01((2.6f - elapsed) * 1.4f);
            float tracking = reaction == Reaction.TracksToy || reaction == Reaction.ReachesForToy ? response : 0;
            float reaching = reaction == Reaction.ReachesForToy ? response : 0;
            float calming = reaction == Reaction.Calmed ? response : 0;
            float feeding = reaction == Reaction.Satisfied ? response : 0;
            sitBlend = Mathf.MoveTowards(sitBlend, canSit ? 1 : 0, Time.unscaledDeltaTime * 1.35f);

            baby.Torso.localRotation = Quaternion.Euler(Mathf.Lerp(43, 3, sitBlend) - 12 * reaching + 4 * calming, 0, 0);
            baby.Torso.localScale = Vector3.one * (1f + .052f * sitBlend + Mathf.Sin(t * 2.4f) * .008f);
            baby.Head.localRotation = Quaternion.Euler(-21 * (1f - sitBlend) + 6 * feeding - 3 * calming,
                -23 * tracking + Mathf.Sin(t * 1.37f) * 2.7f, 2 * calming);
            float mouthOpen = feeding * (.5f + Mathf.Sin(elapsed * 13f) * .5f);
            baby.Mouth.localScale = new Vector3(.075f, .009f + mouthOpen * .023f, .017f);
            baby.LowerLip.localPosition = new Vector3(0, -.008f - .017f * mouthOpen, -.005f);
            float blink = Mathf.Clamp01(Mathf.Abs(Mathf.Repeat(t + 1.1f, 3.8f) - 3.6f) / .095f);
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                float activeReach = i == 1 ? reaching : 0;
                baby.Eyes[i].localScale = new Vector3(1, Mathf.Max(.075f, blink * (1f - .50f * calming)), 1);
                baby.Pupils[i].localPosition = new Vector3(.0075f * tracking, -.002f * tracking, -.021f);
                baby.Arms[i].localRotation = Quaternion.Euler(
                    Mathf.Lerp(38f, 12f, sitBlend) + 64 * activeReach, -18 * activeReach,
                    side * (34 + Mathf.Sin(t * 2.8f + i) * 3.5f));
                baby.Elbows[i].localRotation = Quaternion.Euler(40 - 29 * activeReach, 0, -side * 27);
                baby.Legs[i].localRotation = Quaternion.Euler(
                    72 + 12 * sitBlend + Mathf.Sin(t * 2.7f + i) * (1 - sitBlend) * 4,
                    -side * 16, side * 31);
            }

            toy.localPosition = Vector3.Lerp(new Vector3(.59f, .22f, -.49f),
                new Vector3(.375f, .63f, -.36f), tracking);
            toy.localRotation = Quaternion.Euler(0, 0, 52 + tracking * Mathf.Sin(t * 12) * 11);
            pillow.localScale = Vector3.Lerp(new Vector3(.92f, .32f, .57f),
                new Vector3(.92f, .22f, .57f), sitBlend);
        }

        private void FrameCamera()
        {
            aspect = nurseryCamera.aspect;
            nurseryCamera.orthographicSize = Mathf.Max(1.47f, .82f / Mathf.Max(.25f, aspect));
        }

        private Transform Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            return Form(transform, name, position, scale, material, PrimitiveType.Cube);
        }

        private void Light(string label, Color color, float intensity, Vector3 rotation)
        {
            var light = Joint(transform, label, Vector3.zero).gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.transform.rotation = Quaternion.Euler(rotation);
        }
    }
}
