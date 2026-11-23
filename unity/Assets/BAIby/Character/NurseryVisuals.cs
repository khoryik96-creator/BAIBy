using System.Collections.Generic;
using BAIby.Simulation;
using UnityEngine;

namespace BAIby.Character
{
    public sealed class NurseryVisuals : MonoBehaviour
    {
        private const string Art = "BAIby/";
        private const float QuiltSurface = -1.62f;
        private const float PlayDuration = 2.35f;
        private const float CrawlDuration = 3.10f;
        private readonly List<Object> generatedAssets = new List<Object>();
        private Camera nurseryCamera;
        private SpriteRenderer backdrop;
        private SpriteRenderer rug;
        private SpriteRenderer cushion;
        private SpriteRenderer shade;
        private SpriteRenderer newborn;
        private SpriteRenderer newbornRest;
        private SpriteRenderer sitter;
        private SpriteRenderer sitterRest;
        private SpriteRenderer crawler;
        private SpriteRenderer crawlerRest;
        private Transform toy;
        private SpriteRenderer rattle;
        private SpriteRenderer plush;
        private Toy ambientToy;
        private Toy playedToy;
        private Transform baby;
        private float visibleWidth;
        private float aspect;
        private float reactionStarted;
        private Reaction reaction;
        private bool hasPresented;
        private int targetStage;
        private float stageBlend;

        public static NurseryVisuals Create()
        {
            var visuals = new GameObject("BAIby Nursery").AddComponent<NurseryVisuals>();
            visuals.Build();
            return visuals;
        }

        public void Present(bool adopted, bool canSitAndReach, Reaction newReaction,
            Toy selectedToy = Toy.Rattle, Toy? ambientSelection = null, bool canCrawl = false)
        {
            toy.gameObject.SetActive(adopted);
            targetStage = canCrawl ? 2 : canSitAndReach ? 1 : 0;
            if (!hasPresented)
            {
                stageBlend = targetStage;
                hasPresented = true;
            }

            ambientToy = ambientSelection ?? selectedToy;
            if (newReaction != Reaction.None)
            {
                reaction = newReaction;
                reactionStarted = Time.unscaledTime;
                if (IsPlayReaction(reaction))
                    playedToy = selectedToy;
            }

            ShowToy(IsPlayReaction(reaction) && Time.unscaledTime - reactionStarted < ReactionDuration(reaction)
                ? playedToy : ambientToy);
        }

        private void Build()
        {
            nurseryCamera = new GameObject("Nursery Camera").AddComponent<Camera>();
            nurseryCamera.transform.SetParent(transform, false);
            nurseryCamera.transform.localPosition = new Vector3(0, 0, -10);
            nurseryCamera.tag = "MainCamera";
            nurseryCamera.clearFlags = CameraClearFlags.SolidColor;
            nurseryCamera.backgroundColor = new Color32(207, 221, 205, 255);
            nurseryCamera.orthographic = true;
            nurseryCamera.orthographicSize = 3.60f;
            nurseryCamera.allowHDR = false;

            var roomArt = LoadSprite(Art + "Nursery/Backdrop");
            if (roomArt != null)
            {
                backdrop = AddLayer(transform, "Nursery illustration", roomArt, Color.white, -10);
            }
            else
            {
                var panel = Shape(.14f, false);
                var ellipse = Shape(.49f, false);
                var floor = AddLayer(transform, "Warm floor", panel, new Color32(227, 202, 173, 255), -9);
                floor.transform.localPosition = new Vector3(0, -3.35f, 0);
                floor.transform.localScale = new Vector3(40, 4.10f, 1);
                var light = AddLayer(transform, "Window light", ellipse, new Color32(236, 229, 197, 255), -8);
                light.transform.localPosition = new Vector3(-1.12f, .80f, 0);
                light.transform.localScale = new Vector3(3.85f, 5.10f, 1);
                light.transform.localRotation = Quaternion.Euler(0, 0, -25);
                rug = AddLayer(transform, "Sage play mat", ellipse, new Color32(125, 157, 141, 255), -7);
                cushion = AddLayer(transform, "Quilt", ellipse, new Color32(247, 232, 207, 255), -6);
            }

            shade = AddLayer(transform, "Soft contact shade", Shape(.49f, true),
                new Color32(89, 68, 56, 64), -5);

            baby = new GameObject("Baby rig").transform;
            baby.SetParent(transform, false);
            newborn = AddLayer(baby, "Newborn artwork", LoadSprite(Art + "Baby/Newborn"), Color.white, 0);
            sitter = AddLayer(baby, "Sitter artwork", LoadSprite(Art + "Baby/Sitter"), Color.white, 2);
            newbornRest = AddOptionalStage("Newborn nap artwork", "Baby/NewbornRest", 1);
            sitterRest = AddOptionalStage("Sitter nap artwork", "Baby/SitterRest", 3);
            crawler = AddLayer(baby, "Crawler artwork", LoadSprite(Art + "Baby/Crawler"), Color.white, 4);
            crawlerRest = AddOptionalStage("Crawler nap artwork", "Baby/CrawlerRest", 5);
            SetStageOpacity(0);
            if (newborn.sprite == null || sitter.sprite == null || crawler.sprite == null)
                Debug.LogError("BAIby requires Newborn, Sitter and Crawler sprites in Resources/BAIby/Baby.");

            toy = new GameObject("Nursery toys").transform;
            toy.SetParent(transform, false);
            rattle = AddLayer(toy, "Nursery rattle", LoadSprite(Art + "Baby/Toy"), Color.white, 6);
            plush = AddLayer(toy, "Nursery plush", LoadSprite(Art + "Baby/Plush"), Color.white, 6);
            if (rattle.sprite == null)
                BuildFallbackToy();
            if (plush.sprite == null)
                Debug.LogError("BAIby requires the Plush sprite in Resources/BAIby/Baby.");

            ShowToy(Toy.Rattle);
            toy.gameObject.SetActive(false);
            Frame();
        }

        private SpriteRenderer AddOptionalStage(string label, string resourcePath, int order)
        {
            var sprite = LoadSprite(Art + resourcePath);
            return sprite == null ? null : AddLayer(baby, label, sprite, Color.white, order);
        }

        private void BuildFallbackToy()
        {
            var shape = Shape(.47f, false);
            var handle = AddLayer(rattle.transform, "Rattle handle", shape, new Color32(182, 130, 92, 255), 6);
            handle.transform.localScale = new Vector3(.09f, .25f, 1);
            var head = AddLayer(rattle.transform, "Rattle top", shape, new Color32(228, 165, 83, 255), 7);
            head.transform.localPosition = new Vector3(0, .13f, 0);
            head.transform.localScale = new Vector3(.22f, .21f, 1);
        }

        private void Update()
        {
            if (!Mathf.Approximately(aspect, nurseryCamera.aspect))
                Frame();

            var now = Time.unscaledTime;
            var elapsed = now - reactionStarted;
            float response = Window(elapsed, 0, .22f, 1.55f, PlayDuration);
            float feed = reaction == Reaction.Satisfied ? response : 0;
            float soothe = reaction == Reaction.Calmed ? response : 0;
            float rest = reaction == Reaction.Rested ? Window(elapsed, 0, .65f, 3.10f, 4.35f) : 0;
            bool playing = IsPlayReaction(reaction) && elapsed < ReactionDuration(reaction);
            bool crawling = playing && reaction == Reaction.CrawlsToToy;
            float play = playing && !crawling ? response : 0;
            float reach = reaction == Reaction.ReachesForToy ? Window(elapsed, .25f, .85f, 1.40f, 2.20f) : 0;
            float crawl = crawling ? Window(elapsed, .35f, 1.45f, 2.15f, CrawlDuration) : 0;
            float stride = Mathf.Sin(crawl * Mathf.PI * 6);

            stageBlend = Mathf.MoveTowards(stageBlend, targetStage, Time.unscaledDeltaTime * 2.45f);
            SetStageOpacity(Mathf.SmoothStep(0, 1, rest));

            float breath = Mathf.Sin(now * 2.25f) * Mathf.Lerp(.008f, .004f, rest);
            float crawlerOffset = -.04f * Mathf.Clamp01(stageBlend - 1);
            float crawlShift = visibleWidth * (crawlerOffset + .13f * (crawl + stride * .022f));
            float crawlLift = Mathf.Abs(stride) * .055f;
            baby.localPosition = new Vector3(.08f * play + .10f * reach + crawlShift,
                QuiltSurface - .024f * rest + .022f * feed + crawlLift, 0);
            baby.localRotation = Quaternion.Euler(0, 0,
                Mathf.Sin(now * 1.25f) * soothe * 4 - 4 * rest - 4 * reach - stride * 1.65f);
            baby.localScale = new Vector3(1 + breath * .5f, 1 + breath + .022f * feed, 1);
            shade.transform.localPosition = new Vector3(crawlShift, QuiltSurface - .06f, 0);

            var displayedToy = playing ? playedToy : ambientToy;
            ShowToy(displayedToy);
            PoseToy(displayedToy, now, play, reach, crawling, crawl);
        }

        private static bool IsPlayReaction(Reaction value)
        {
            return value == Reaction.TracksToy || value == Reaction.ReachesForToy || value == Reaction.CrawlsToToy;
        }

        private static float ReactionDuration(Reaction value)
        {
            return value == Reaction.CrawlsToToy ? CrawlDuration : PlayDuration;
        }

        private void ShowToy(Toy selectedToy)
        {
            bool showPlush = selectedToy == Toy.Plush && plush.sprite != null;
            if (rattle.gameObject.activeSelf == showPlush)
                rattle.gameObject.SetActive(!showPlush);
            if (plush.gameObject.activeSelf != showPlush)
                plush.gameObject.SetActive(showPlush);
        }

        private void PoseToy(Toy displayedToy, float now, float play, float reach, bool crawling, float crawl)
        {
            bool isPlush = displayedToy == Toy.Plush;
            var offered = new Vector3(visibleWidth * (.39f - play * .07f),
                -1.17f + play * .79f + Mathf.Sin(now * 8f) * play * .075f, 0);
            if (crawling)
            {
                toy.localPosition = offered + new Vector3(0, Mathf.Max(0, crawl - .85f) * .45f, 0);
            }
            else if (reach > 0 && sitter.sprite != null)
            {
                // Align the lower toy edge with the raised hand painted into the sitter pose.
                var spriteSize = sitter.sprite.bounds.size;
                var hand = sitter.transform.TransformPoint(new Vector3(spriteSize.x * .37f, spriteSize.y * .075f, 0));
                var held = transform.InverseTransformPoint(hand) + new Vector3(.045f, isPlush ? .20f : .13f, 0);
                held.x = Mathf.Min(held.x, visibleWidth * .45f - .14f);
                toy.localPosition = Vector3.Lerp(offered, held, reach);
            }
            else
            {
                toy.localPosition = offered;
            }

            var angle = isPlush ? -6 : 25;
            float wiggle = play * (isPlush ? 12 : 23) * (1 - reach);
            float contact = crawling ? Mathf.Max(0, crawl - .85f) * 70 : 0;
            toy.localRotation = Quaternion.Euler(0, 0, angle + Mathf.Sin(now * 9f) * (wiggle + contact));
        }

        private void SetStageOpacity(float resting)
        {
            float newbornAlpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(stageBlend));
            float crawlerAlpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(stageBlend - 1));
            float sitterAlpha = 1 - newbornAlpha - crawlerAlpha;
            SetPose(newborn, newbornRest, newbornAlpha, resting);
            SetPose(sitter, sitterRest, sitterAlpha, resting);
            SetPose(crawler, crawlerRest, crawlerAlpha, resting);
        }

        private static void SetPose(SpriteRenderer awake, SpriteRenderer nap, float opacity, float resting)
        {
            SetOpacity(awake, opacity * (nap == null ? 1 : 1 - resting));
            SetOpacity(nap, opacity * resting);
        }

        private static void SetOpacity(SpriteRenderer layer, float opacity)
        {
            if (layer == null)
                return;
            layer.enabled = opacity > 0;
            layer.color = new Color(1, 1, 1, opacity);
        }

        private void Frame()
        {
            aspect = nurseryCamera.aspect;
            float worldWidth = 2 * nurseryCamera.orthographicSize * aspect;
            visibleWidth = Mathf.Min(worldWidth * .95f, 5.17f);
            if (backdrop != null)
            {
                float worldHeight = 2 * nurseryCamera.orthographicSize;
                float artHeight = Fit(backdrop, worldWidth, worldHeight, true);
                float overscan = (artHeight - worldHeight) * .5f;
                // The quilt center is 24% up from the bottom of this illustration.
                float y = Mathf.Clamp(QuiltSurface + artHeight * .26f, -overscan, overscan);
                backdrop.transform.localPosition = new Vector3(0, y, 0);
            }
            if (rug != null)
            {
                rug.transform.localPosition = new Vector3(0, -1.76f, 0);
                rug.transform.localScale = new Vector3(visibleWidth * 1.34f, .83f, 1);
                cushion.transform.localPosition = new Vector3(0, -1.42f, 0);
                cushion.transform.localScale = new Vector3(visibleWidth * .92f, .73f, 1);
            }

            shade.transform.localPosition = new Vector3(0, QuiltSurface - .06f, 0);
            shade.transform.localScale = new Vector3(visibleWidth * .75f, .46f, 1);
            AnchorStage(newborn, .94f);
            AnchorStage(sitter, .87f);
            AnchorStage(newbornRest, .94f);
            AnchorStage(sitterRest, .87f);
            AnchorStage(crawler, .80f, 2.95f);
            AnchorStage(crawlerRest, .83f, 2.95f);
            Fit(rattle, .46f, .46f);
            Fit(plush, .59f, .59f);
            baby.localPosition = new Vector3(0, QuiltSurface, 0);
        }

        private void AnchorStage(SpriteRenderer layer, float widthFraction, float maxHeight = 3.68f)
        {
            if (layer == null)
                return;
            float height = Fit(layer, visibleWidth * widthFraction, maxHeight);
            layer.transform.localPosition = new Vector3(0, height * .5f, 0);
        }

        private static float Fit(SpriteRenderer layer, float width, float height, bool cover = false)
        {
            if (layer.sprite == null)
                return 0;
            var size = layer.sprite.bounds.size;
            float scale = cover ? Mathf.Max(width / size.x, height / size.y)
                : Mathf.Min(width / size.x, height / size.y);
            layer.transform.localScale = new Vector3(scale, scale, 1);
            return size.y * scale;
        }

        private static SpriteRenderer AddLayer(Transform parent, string name, Sprite sprite, Color tint, int order)
        {
            var layer = new GameObject(name).AddComponent<SpriteRenderer>();
            layer.transform.SetParent(parent, false);
            layer.sprite = sprite;
            layer.color = tint;
            layer.sortingOrder = order;
            return layer;
        }

        private Sprite LoadSprite(string resourcePath)
        {
            var imported = Resources.Load<Sprite>(resourcePath);
            if (imported != null)
                return imported;
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
                return null;
            var sprite = SpriteFrom(texture, 100);
            generatedAssets.Add(sprite);
            return sprite;
        }

        private Sprite Shape(float radius, bool feather)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs((x + .5f) / size - .5f);
                    float py = Mathf.Abs((y + .5f) / size - .5f);
                    float dx = Mathf.Max(0, px - .5f + radius);
                    float dy = Mathf.Max(0, py - .5f + radius);
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((radius - distance) / (feather ? radius : .032f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
                }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = SpriteFrom(texture, size);
            generatedAssets.Add(sprite);
            generatedAssets.Add(texture);
            return sprite;
        }

        private static Sprite SpriteFrom(Texture2D texture, float pixelsPerUnit)
        {
            var bounds = new Rect(0, 0, texture.width, texture.height);
            return Sprite.Create(texture, bounds, new Vector2(.5f, .5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero, false);
        }

        private static float Window(float elapsed, float start, float full, float release, float end)
        {
            float fadeIn = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, full, elapsed));
            float fadeOut = Mathf.SmoothStep(1, 0, Mathf.InverseLerp(release, end, elapsed));
            return fadeIn * fadeOut;
        }

        private void OnDestroy()
        {
            foreach (var asset in generatedAssets)
                Destroy(asset);
        }
    }
}
