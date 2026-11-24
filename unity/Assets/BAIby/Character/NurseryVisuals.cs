using System.Collections.Generic;
using BAIby.Simulation;
using Unity.Collections;
using UnityEngine;

namespace BAIby.Character
{
    public sealed class NurseryVisuals : MonoBehaviour
    {
        private const string Art = "BAIby/";
        private const string AppearanceArt = Art + "Baby/Appearance/";
        private const float QuiltSurface = -1.62f;
        private const float PoseHeight = 3.68f;
        private const float WalkerGround = .22f;
        private const float WalkerHeight = 3.47f;
        // The top of the tallest pose's art rectangle (the walker stands on a raised ground). The rectangles
        // leave transparent room above every head, which covers the small breathing and stride lifts. UI that
        // must never cover the baby's head stays above it.
        private const float PoseCeiling = QuiltSurface + WalkerGround + WalkerHeight;
        private const float PlayDuration = 2.35f;
        private const float CrawlDuration = 3.10f;
        private const float WalkDuration = 3.40f;
        private const float DiaperCueDuration = 2.40f;
        private const float BathCueDuration = 2.75f;
        private const float BlocksCueDuration = 2.55f;
        private const float RefuseCueDuration = 1.70f;
        // Fussy and Upset reuse the existing poses: a cooler tint, a whimpering fidget and, when upset, a droop.
        private static readonly Color UpsetTint = new Color(.84f, .88f, .98f, 1);
        // Mess spots sit on the floor beside the baby, clear of the toy and the trays at 390 x 844.
        private static readonly float[] MessX = { -.42f, -.30f, .43f };
        private static readonly float[] MessY = { -1.40f, -1.56f, -1.52f };
        private static readonly float[] MessAngle = { -12f, 9f, 16f };
        // The health axis uses simple shapes over the existing art: under the weather a paler tint, a droop and a
        // sniffle bubble; sick the nap pose tucked under a quilt with a thermometer bubble; recovering the quilt
        // and a medicine bubble.
        private static readonly Color PoorlyTint = new Color(.96f, .89f, .85f, 1);
        private static readonly Color BlanketTrimColor = new Color32(241, 236, 249, 255);
        // The quilt: lilac with lighter diagonal checks, a light fold along the top, a stitch line, and the baby
        // art's dark-brown outline.
        private static readonly Color32 QuiltFill = new Color32(217, 204, 240, 255);
        private static readonly Color32 QuiltOutline = new Color32(77, 41, 23, 255);
        private const int QuiltTexture = 128;
        private const float QuiltBorder = 48;
        private const float QuiltPixelsPerUnit = 180;
        private static readonly Color BubbleColor = new Color(1, 1, 1, .94f);
        private static readonly Color SniffleColor = new Color32(146, 194, 228, 255);
        private static readonly Color TubeColor = new Color32(228, 232, 238, 255);
        private static readonly Color FeverColor = new Color32(224, 104, 92, 255);
        private static readonly Color CapsuleWarm = new Color32(240, 172, 112, 255);
        private static readonly Color CapsuleLight = new Color32(250, 245, 232, 255);
        // Looks whose pose sprites are kept, most recent first, so switching back costs nothing.
        private const int CachedLooks = 2;
        private readonly List<Object> generatedAssets = new List<Object>();
        private readonly List<BuiltLook> builtLooks = new List<BuiltLook>();
        // Journal photo poses in other looks, most recent first.
        private const int CachedPhotos = 6;
        private readonly List<(int Pose, BuiltLook Look)> photoSprites = new List<(int Pose, BuiltLook Look)>();
        // Decoded appearance art (RGBA, bottom-up rows) keyed by resource path, decoded once.
        private readonly Dictionary<string, byte[]> appearanceSources = new Dictionary<string, byte[]>();
        private AppearanceLayout appearanceLayout;
        private BabyAppearance shownAppearance = BabyAppearance.Default;
        // The look whose art could not be built; it is not rebuilt again unless a retry is asked for.
        private BabyAppearance failedAppearance;
        // Null while the original art (the default look) is shown.
        private BuiltLook currentLook;
        private Sprite[] originalPoseSprites;
        private Texture2D appearanceDecoder;
        private byte[] appearancePixels;
        private byte[] appearanceRegions;
        private Camera nurseryCamera;
        private SpriteRenderer backdrop;
        private SpriteRenderer peachBackdrop;
        private float peachOpacity;
        private float targetPeachOpacity;
        private SpriteRenderer rug;
        private SpriteRenderer cushion;
        private SpriteRenderer shade;
        private SpriteRenderer newborn;
        private SpriteRenderer newbornRest;
        private SpriteRenderer sitter;
        private SpriteRenderer sitterRest;
        private SpriteRenderer crawler;
        private SpriteRenderer crawlerRest;
        private SpriteRenderer walker;
        private SpriteRenderer walkerRest;
        private Transform toy;
        private SpriteRenderer rattle;
        private SpriteRenderer plush;
        private SpriteRenderer storybook;
        private SpriteRenderer freshDiaper;
        private SpriteRenderer bathBasket;
        private SpriteRenderer blockTower;
        private float towerHeight;
        private SpriteRenderer nurseryChime;
        private bool playedSoundPreview;
        private float soundPreviewStarted;
        private readonly Dictionary<Food, SpriteRenderer> feedingProps = new Dictionary<Food, SpriteRenderer>();
        private Food servedFood;
        private Toy ambientToy;
        private Toy playedToy;
        private Transform baby;
        private float visibleWidth;
        private float aspect;
        // The open part of the screen, between the interface's top cards and its bottom sheet, as fractions of
        // the screen height (see SetOpenBand). The full screen until the interface reports otherwise.
        private float bandBottom;
        private float bandTop = 1;
        private float reactionStarted;
        private Reaction reaction;
        private bool hasPresented;
        private bool adopted;
        private int targetStage;
        private float stageBlend;
        private CareMood mood;
        private float distress;
        private int messItems;
        private readonly SpriteRenderer[] messSpots = new SpriteRenderer[CareRecord.MaxMess];
        private CareCondition condition;
        private bool snapHealthLook = true;
        private float unwellLook;
        private float sickLook;
        private float recoveringLook;
        private SpriteRenderer blanket;
        private SpriteRenderer blanketTrim;
        private Transform healthBadge;
        private SpriteRenderer badgeBubble;
        private SpriteRenderer sniffle;
        private SpriteRenderer sniffleShine;
        private SpriteRenderer thermometer;
        private SpriteRenderer thermometerFill;
        private SpriteRenderer thermometerBulb;
        private SpriteRenderer capsuleWarm;
        private SpriteRenderer capsuleLight;

        // The screen-space y (pixels from the bottom) of the pose ceiling at the current aspect.
        public float PoseCeilingScreenY =>
            nurseryCamera == null ? 0 : nurseryCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(0, PoseCeiling, 0))).y;

        // Where a pet stands in the room, and the world size of one baby pose pixel (pose art is drawn at
        // 100 pixels per unit), so PetVisuals can draw a pet at its layout's runtime scale beside the baby.
        public float FloorY => QuiltSurface;
        public float PosePixelScale => sitter == null || sitter.sprite == null ? .62f : sitter.transform.localScale.x;
        public float VisibleWidth => visibleWidth;

        // Fits the stage into the part of the screen the interface leaves open: bottom and top are that band's
        // edges as fractions of the screen height. The camera moves down (and zooms out a little when the band is
        // short) so the quilt sits just above the bottom sheet and the tallest pose stays under the top cards.
        // The room's backdrop still covers everything the sheet does not.
        public void SetOpenBand(float bottom, float top)
        {
            bottom = Mathf.Clamp(bottom, 0, .7f);
            top = Mathf.Clamp(top, bottom + .2f, 1);
            if (Mathf.Abs(bottom - bandBottom) < .002f && Mathf.Abs(top - bandTop) < .002f)
                return;

            bandBottom = bottom;
            bandTop = top;
            ApplyOpenBand();
        }

        private void ApplyOpenBand()
        {
            if (nurseryCamera == null)
                return;

            const float roomSize = 3.60f;
            const float floor = QuiltSurface - .14f;
            const float span = PoseCeiling + .06f - floor;
            var open = Mathf.Max(.25f, bandTop - bandBottom - .02f);
            var size = Mathf.Clamp(span / (2 * open), roomSize, roomSize * 1.32f);
            // The floor lands on the band's bottom edge; the camera never rises above the room's centre, so the
            // backdrop always covers the top of the screen.
            var y = Mathf.Min(0, floor + size - bandBottom * 2 * size);
            nurseryCamera.orthographicSize = size;
            nurseryCamera.transform.localPosition = new Vector3(0, y, -10);
            Frame();
        }

        // Hides the baby and its contact shade while a pet's room is shown. Toys and cues follow Present.
        public void SetBabyShown(bool shown)
        {
            if (baby.gameObject.activeSelf != shown)
                baby.gameObject.SetActive(shown);
            if (shade.gameObject.activeSelf != shown)
                shade.gameObject.SetActive(shown);
            if (healthBadge.gameObject.activeSelf != shown)
                healthBadge.gameObject.SetActive(shown);
        }

        public static NurseryVisuals Create()
        {
            var visuals = new GameObject("BAIby Nursery").AddComponent<NurseryVisuals>();
            visuals.Build();
            return visuals;
        }

        public void Present(bool adopted, bool canSitAndReach, Reaction newReaction,
            Toy selectedToy = Toy.Rattle, Toy? ambientSelection = null, bool canCrawl = false, Food food = Food.Milk,
            bool canWalk = false)
        {
            this.adopted = adopted;
            targetStage = canWalk ? 3 : canCrawl ? 2 : canSitAndReach ? 1 : 0;
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
                // A refused toy is the one that was offered, so that is the toy pushed away.
                if (IsPlayReaction(reaction) || reaction == Reaction.PushesToyAway)
                    playedToy = selectedToy;
                if (reaction == Reaction.Satisfied || reaction == Reaction.TooFull)
                    servedFood = food;
            }

            float elapsed = Time.unscaledTime - reactionStarted;
            UpdateToyVisibility(elapsed);
            ShowToy(ShowsOfferedToy(elapsed) ? playedToy : ambientToy);
        }

        // While a play cue or a refusal runs, the toy on screen is the one the player offered.
        private bool ShowsOfferedToy(float elapsed) =>
            (IsPlayReaction(reaction) && elapsed < ReactionDuration(reaction)) ||
            (reaction == Reaction.PushesToyAway && elapsed < RefuseCueDuration);

        // Prepares the room for a different baby: the next Present snaps to that baby's stage, and the
        // previous baby's reaction cues and sound preview do not carry over.
        public void ResetPresentation()
        {
            hasPresented = false;
            reaction = Reaction.None;
            playedSoundPreview = false;
            distress = -1;
            snapHealthLook = true;
        }

        public void SetTheme(NurseryTheme theme)
        {
            targetPeachOpacity = theme == NurseryTheme.Peach ? 1 : 0;
        }

        // The baby's place on the needs axis and the health axis and the room's mess, shown with the existing art: a
        // fussy fidget, an upset droop and tint, up to three spots on the floor, and the health cues (BuildHealthCues).
        public void SetCondition(CareMood newMood, int mess, CareCondition health = CareCondition.Well)
        {
            mood = newMood;
            messItems = Mathf.Clamp(mess, 0, messSpots.Length);
            condition = health;
        }

        // Shows the baby's chosen look on all eight pose illustrations. The default look keeps the
        // original imported sprites; for other looks each pose is composited (base, hair, headwear) and
        // recolored when it is first needed: the visible stage's awake and nap poses now, the rest when
        // they fade in. The last few looks stay cached. Returns false when the look's art could not be
        // built; the original art is shown then, and that look is only tried again with retryFailed.
        public bool SetAppearance(BabyAppearance appearance, bool retryFailed = false)
        {
            if (appearance == null)
                appearance = BabyAppearance.Default;
            if (!retryFailed && appearance.Equals(failedAppearance))
                return false;
            if (appearance.Equals(shownAppearance))
                return true;

            var layers = PoseLayers();
            if (originalPoseSprites == null)
            {
                originalPoseSprites = new Sprite[layers.Length];
                for (int i = 0; i < layers.Length; i++)
                    originalPoseSprites[i] = layers[i] == null ? null : layers[i].sprite;
            }

            failedAppearance = null;
            shownAppearance = appearance;
            if (appearance.Equals(BabyAppearance.Default))
            {
                currentLook = null;
                ShowPoseSprites(layers, originalPoseSprites);
                AnchorStages();
                return true;
            }

            currentLook = CachedLook(appearance);
            // Poses not built yet show the original art (same size) until they are needed.
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null)
                    layers[i].sprite = currentLook.Sprites[i] != null ? currentLook.Sprites[i] : originalPoseSprites[i];
            }

            var needed = new List<int>();
            for (int stage = 0; stage < 4; stage++)
            {
                if (stage == targetStage || StageOpacity(stage) > 0)
                {
                    needed.Add(stage * 2);
                    needed.Add(stage * 2 + 1);
                }
            }

            return BuildPoses(needed);
        }

        private BuiltLook CachedLook(BabyAppearance appearance)
        {
            for (int i = 0; i < builtLooks.Count; i++)
            {
                var look = builtLooks[i];
                if (!look.Appearance.Equals(appearance))
                    continue;
                builtLooks.RemoveAt(i);
                builtLooks.Insert(0, look);
                return look;
            }

            var created = new BuiltLook(appearance, PoseLayers().Length);
            builtLooks.Insert(0, created);
            // The look on screen is never evicted here: it is either Default or now second in the list.
            while (builtLooks.Count > CachedLooks)
                DiscardLook(builtLooks[builtLooks.Count - 1]);
            return created;
        }

        // A journal photo's picture: the pose of a stage (0 newborn to 3 walker), awake or resting, in the look the photo
        // kept. Drawn from the pose illustrations when the journal is read; nothing is captured from the screen. The
        // on-screen look is reused, other looks are built once and a few are kept; when a look cannot be built the
        // original art stands in.
        public Sprite PhotoSprite(BabyAppearance appearance, int stage, bool rest)
        {
            var pose = Mathf.Clamp(stage, 0, 3) * 2 + (rest ? 1 : 0);
            var layers = PoseLayers();
            var original = originalPoseSprites != null ? originalPoseSprites[pose]
                : layers[pose] != null && shownAppearance.Equals(BabyAppearance.Default) ? layers[pose].sprite : null;
            if (original == null)
                original = LoadSprite(Art + "Baby/" + AppearanceLayout.PoseNames[pose]);
            if (appearance == null || appearance.Equals(BabyAppearance.Default) || layers[pose] == null)
                return original;
            if (currentLook != null && currentLook.Appearance.Equals(appearance) && currentLook.Sprites[pose] != null)
                return currentLook.Sprites[pose];

            for (int i = 0; i < photoSprites.Count; i++)
            {
                var kept = photoSprites[i];
                if (kept.Pose == pose && kept.Look.Appearance.Equals(appearance))
                    return kept.Look.Sprites[0] ?? original;
            }

            var built = new BuiltLook(appearance, 1);
            try
            {
                var layout = LoadAppearanceLayout();
                built.Sprites[0] = BuildPoseSprite(layout, AppearanceLayout.PoseNames[pose], appearance,
                    AppearancePalette.Targets(appearance, layout.Reference), built.Assets);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("A journal photo's look could not be drawn, so the original artwork is shown: " + exception.Message);
            }
            finally
            {
                ReleaseAppearanceDecoder();
            }

            photoSprites.Insert(0, (pose, built));
            while (photoSprites.Count > CachedPhotos)
            {
                var last = photoSprites[photoSprites.Count - 1];
                photoSprites.RemoveAt(photoSprites.Count - 1);
                foreach (var asset in last.Look.Assets)
                    Destroy(asset);
            }

            return built.Sprites[0] ?? original;
        }

        private void DiscardLook(BuiltLook look)
        {
            builtLooks.Remove(look);
            foreach (var asset in look.Assets)
                Destroy(asset);
            look.Assets.Clear();
        }

        // Builds the current look's missing sprites for the given pose indices and puts them on screen.
        // On any failure the whole look is dropped and the original art is shown on every pose.
        private bool BuildPoses(List<int> poses)
        {
            var look = currentLook;
            if (look == null)
                return true;

            var layers = PoseLayers();
            try
            {
                AppearanceLayout layout = null;
                AppearanceColors targets = null;
                foreach (int pose in poses)
                {
                    if (layers[pose] == null || look.Sprites[pose] != null)
                        continue;
                    if (layout == null)
                    {
                        layout = LoadAppearanceLayout();
                        targets = AppearancePalette.Targets(look.Appearance, layout.Reference);
                    }

                    var sprite = BuildPoseSprite(layout, AppearanceLayout.PoseNames[pose], look.Appearance, targets, look.Assets);
                    look.Sprites[pose] = sprite;
                    layers[pose].sprite = sprite;
                }

                return true;
            }
            catch (System.Exception exception)
            {
                failedAppearance = look.Appearance;
                shownAppearance = BabyAppearance.Default;
                currentLook = null;
                ShowPoseSprites(layers, originalPoseSprites);
                DiscardLook(look);
                Debug.LogWarning("The baby's look could not be applied, so the original artwork is shown: " + exception.Message);
                return false;
            }
            finally
            {
                ReleaseAppearanceDecoder();
                AnchorStages();
            }
        }

        private void EnsurePoseBuilt(int pose)
        {
            if (currentLook != null && currentLook.Sprites[pose] == null)
                BuildPoses(new List<int> { pose });
        }

        public void PlayNewSoundCue()
        {
            if (playedSoundPreview)
                return;

            playedSoundPreview = true;
            soundPreviewStarted = Time.unscaledTime;
            try
            {
                var clip = CreateChimeClip();
                if (clip == null)
                    return;

                generatedAssets.Add(clip);
                if (FindFirstObjectByType<AudioListener>() == null)
                    nurseryCamera.gameObject.AddComponent<AudioListener>();
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                source.PlayOneShot(clip, .30f);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("The nursery chime could not play: " + exception.Message);
            }
        }

        private static AudioClip CreateChimeClip()
        {
            const int sampleRate = 22050;
            const float seconds = .95f;
            var samples = new float[Mathf.RoundToInt(sampleRate * seconds)];
            for (int sample = 0; sample < samples.Length; sample++)
            {
                float t = sample / (float)sampleRate;
                float tail = Mathf.Clamp01((seconds - t) * 10);
                samples[sample] = tail * (ChimeNote(t, 523.25f) + .72f * ChimeNote(t - .23f, 659.25f));
            }

            var clip = AudioClip.Create("Nursery chime audio", samples.Length, 1, sampleRate, false);
            if (clip != null && clip.SetData(samples, 0))
                return clip;

            if (clip != null)
                Destroy(clip);
            return null;
        }

        private static float ChimeNote(float seconds, float frequency)
        {
            if (seconds < 0)
                return 0;

            float phase = seconds * 2 * Mathf.PI * frequency;
            float swell = Mathf.Clamp01(seconds / .019f);
            float decay = Mathf.Exp(-seconds * 6.2f);
            return swell * decay * (.24f * Mathf.Sin(phase) + .043f * Mathf.Sin(phase * 2.02f));
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
                backdrop = AddLayer(transform, "Nursery illustration", roomArt, Color.white, -11);
                var peachArt = LoadSprite(Art + "Nursery/BackdropPeach");
                if (peachArt == null)
                    Debug.LogError("Missing peach nursery illustration in Resources/BAIby/Nursery/BackdropPeach.");
                else
                    peachBackdrop = AddLayer(transform, "Nursery illustration peach", peachArt, Color.white, -10);
                SetOpacity(peachBackdrop, 0);
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
            walker = AddLayer(baby, "Walker artwork", LoadSprite(Art + "Baby/Walker"), Color.white, 6);
            walkerRest = AddOptionalStage("Walker nap artwork", "Baby/WalkerRest", 7);
            SetStageOpacity(0);
            if (newborn.sprite == null || sitter.sprite == null || crawler.sprite == null || walker.sprite == null)
                Debug.LogError("BAIby requires Newborn, Sitter, Crawler and Walker sprites in Resources/BAIby/Baby.");

            toy = new GameObject("Nursery toys").transform;
            toy.SetParent(transform, false);
            rattle = AddLayer(toy, "Nursery rattle", LoadSprite(Art + "Baby/Toy"), Color.white, 10);
            plush = AddLayer(toy, "Nursery plush", LoadSprite(Art + "Baby/Plush"), Color.white, 10);
            if (rattle.sprite == null)
                BuildFallbackToy();
            if (plush.sprite == null)
                Debug.LogError("BAIby requires the Plush sprite in Resources/BAIby/Baby.");

            storybook = AddCue("Nursery storybook", "Storybook");
            freshDiaper = AddCue("Nursery fresh diaper", "FreshDiaper");
            bathBasket = AddCue("Nursery bath basket", "BathBasket");
            blockTower = AddCue("Nursery block tower", "BlockTower");
            nurseryChime = AddCue("Nursery chime", "NurseryChime");
            var chimeHinge = new GameObject("Chime hinge").transform;
            chimeHinge.SetParent(transform, false);
            nurseryChime.transform.SetParent(chimeHinge, false);
            feedingProps.Add(Food.Milk, AddCue("Nursery milk", "Milk"));
            feedingProps.Add(Food.Banana, AddCue("Nursery banana", "Banana"));
            feedingProps.Add(Food.Oats, AddCue("Nursery oats", "Oats"));
            // The diaper exercise's spot art doubles as the messy-room cue, drawn between the shade and the baby.
            var spotArt = LoadSprite(Art + "Baby/CleanupSpot");
            for (var i = 0; i < messSpots.Length; i++)
            {
                messSpots[i] = AddLayer(transform, "Nursery mess " + (i + 1), spotArt, new Color(1, 1, 1, .9f), -4);
                messSpots[i].enabled = false;
            }

            BuildHealthCues();
            ShowToy(Toy.Rattle);
            toy.gameObject.SetActive(false);
            Frame();
        }

        private SpriteRenderer AddCue(string label, string fileName)
        {
            var location = Art + "Baby/" + fileName;
            var cue = AddLayer(transform, label, LoadSprite(location), Color.white, 12);
            if (cue.sprite == null)
                Debug.LogError("Missing nursery illustration in Resources/" + location + ".");
            cue.enabled = false;
            return cue;
        }

        private SpriteRenderer AddOptionalStage(string label, string resourcePath, int order)
        {
            var sprite = LoadSprite(Art + resourcePath);
            return sprite == null ? null : AddLayer(baby, label, sprite, Color.white, order);
        }

        private SpriteRenderer[] PoseLayers()
        {
            // Same order as AppearanceLayout.PoseNames.
            return new[] { newborn, newbornRest, sitter, sitterRest, crawler, crawlerRest, walker, walkerRest };
        }

        private static void ShowPoseSprites(SpriteRenderer[] layers, Sprite[] sprites)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null)
                    layers[i].sprite = sprites[i];
            }
        }

        private void ReleaseAppearanceDecoder()
        {
            // Runtime textures are not assets, so they can be freed immediately rather than at frame end.
            if (appearanceDecoder != null)
                DestroyImmediate(appearanceDecoder);
            appearanceDecoder = null;
        }

        private AppearanceLayout LoadAppearanceLayout()
        {
            if (appearanceLayout != null)
                return appearanceLayout;

            var asset = Resources.Load<TextAsset>(AppearanceArt + "layout");
            if (asset == null)
                throw new System.InvalidOperationException("Missing Resources/" + AppearanceArt + "layout.json.");
            try
            {
                appearanceLayout = AppearanceLayout.FromData(JsonUtility.FromJson<AppearanceLayoutData>(asset.text));
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
            return appearanceLayout;
        }

        private Sprite BuildPoseSprite(AppearanceLayout layout, string pose, BabyAppearance appearance,
            AppearanceColors targets, List<Object> created)
        {
            if (!layout.TryGetPoseSize(pose, out int width, out int height))
                throw new System.InvalidOperationException("The appearance layout has no " + pose + " pose.");

            int length = width * height * AppearanceRecolor.Channels;
            if (appearancePixels == null || appearancePixels.Length < length)
            {
                appearancePixels = new byte[length];
                appearanceRegions = new byte[length];
            }

            var basePath = AppearanceLayout.BasePath(pose);
            System.Buffer.BlockCopy(LoadAppearanceSource(basePath, width, height), 0, appearancePixels, 0, length);
            System.Buffer.BlockCopy(LoadAppearanceSource(AppearanceLayout.RegionsPath(basePath), width, height),
                0, appearanceRegions, 0, length);
            CompositeAppearanceOverlay(layout, AppearanceLayout.HairPath(appearance.HairStyle, pose), pose, width, height);
            var headwearPath = AppearanceLayout.HeadwearPath(appearance.Headwear, pose);
            if (headwearPath != null)
                CompositeAppearanceOverlay(layout, headwearPath, pose, width, height);
            AppearanceRecolor.Recolor(appearancePixels, appearanceRegions, width * height, layout.Reference, targets);
            // The imported sprites get this edge fill from "Alpha Is Transparency"; match them.
            AppearanceRecolor.BleedTransparentEdges(appearancePixels, width, height);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            created.Add(texture);
            texture.name = "Baby " + pose + " look";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            // RGBA32 pixel data is tightly packed RGBA with bottom-up rows, matching the scratch buffer.
            var data = texture.GetPixelData<byte>(0);
            NativeArray<byte>.Copy(appearancePixels, 0, data, 0, length);
            texture.Apply(false, true);

            // Same pivot, pixels per unit and pixel size as the imported pose sprites, so framing holds.
            var sprite = SpriteFrom(texture, 100);
            sprite.name = texture.name;
            created.Add(sprite);
            return sprite;
        }

        private void CompositeAppearanceOverlay(AppearanceLayout layout, string path, string pose, int width, int height)
        {
            if (!layout.TryGetOverlay(path, out var overlay) || overlay.Pose != pose)
                throw new System.InvalidOperationException("The appearance layout has no " + path + " overlay.");

            var pixels = LoadAppearanceSource(path, overlay.Width, overlay.Height);
            var regions = LoadAppearanceSource(AppearanceLayout.RegionsPath(path), overlay.Width, overlay.Height);
            // Unity buffers are bottom-up while the layout measures y from the top; CompositeOver converts.
            AppearanceRecolor.CompositeOver(appearancePixels, appearanceRegions, width, height,
                pixels, regions, overlay.Width, overlay.Height, overlay.X, overlay.YFromTop, PixelRowOrder.BottomUp);
        }

        // Decodes a PNG stored as Resources/BAIby/Baby/Appearance/{path}.bytes into RGBA bytes with
        // bottom-up rows (Unity texture order). Decoded art is cached. One decode texture is reused for
        // every image and freed by BuildPoses once the batch is done.
        private byte[] LoadAppearanceSource(string path, int width, int height)
        {
            if (appearanceSources.TryGetValue(path, out var cached))
                return cached;

            var asset = Resources.Load<TextAsset>(AppearanceArt + path);
            if (asset == null)
                throw new System.InvalidOperationException("Missing Resources/" + AppearanceArt + path + ".bytes.");

            try
            {
                if (appearanceDecoder == null)
                {
                    appearanceDecoder = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    appearanceDecoder.name = "Baby look decoder";
                }

                var texture = appearanceDecoder;
                if (!ImageConversion.LoadImage(texture, asset.bytes, false))
                    throw new System.InvalidOperationException(path + " is not a readable PNG.");
                if (texture.width != width || texture.height != height)
                    throw new System.InvalidOperationException(path + " is " + texture.width + "x" + texture.height +
                        " but the layout expects " + width + "x" + height + ".");

                var bytes = ReadRgba(texture, width * height);
                appearanceSources.Add(path, bytes);
                return bytes;
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        // Copies the decoded pixels straight from the texture data where the layout is known, avoiding
        // a Color32[] per image; other formats go through GetPixels32.
        internal static byte[] ReadRgba(Texture2D texture, int pixelCount)
        {
            int length = pixelCount * AppearanceRecolor.Channels;
            var bytes = new byte[length];
            var format = texture.format;
            if (format == TextureFormat.RGBA32 || format == TextureFormat.ARGB32)
            {
                var data = texture.GetPixelData<byte>(0);
                if (data.Length >= length)
                {
                    NativeArray<byte>.Copy(data, 0, bytes, 0, length);
                    if (format == TextureFormat.ARGB32)
                    {
                        for (int offset = 0; offset < length; offset += AppearanceRecolor.Channels)
                        {
                            byte alpha = bytes[offset];
                            bytes[offset] = bytes[offset + 1];
                            bytes[offset + 1] = bytes[offset + 2];
                            bytes[offset + 2] = bytes[offset + 3];
                            bytes[offset + 3] = alpha;
                        }
                    }
                    return bytes;
                }
            }

            var colors = texture.GetPixels32();
            for (int i = 0; i < colors.Length && i < pixelCount; i++)
            {
                var color = colors[i];
                int offset = i * AppearanceRecolor.Channels;
                bytes[offset] = color.r;
                bytes[offset + 1] = color.g;
                bytes[offset + 2] = color.b;
                bytes[offset + 3] = color.a;
            }
            return bytes;
        }

        // Shapes only, no new art: a blanket on the baby rig and a small bubble beside the baby holding a sniffle
        // drop, a thermometer or a medicine capsule.
        private void BuildHealthCues()
        {
            var disc = Shape(.5f, false);
            var soft = Shape(.22f, false);
            blanket = AddLayer(baby, "Health blanket", Quilt(), Color.white, 8);
            blanket.drawMode = SpriteDrawMode.Tiled;
            blanket.tileMode = SpriteTileMode.Adaptive;
            blanket.adaptiveModeThreshold = 0.5f;
            blanketTrim = AddLayer(baby, "Health blanket trim", soft, BlanketTrimColor, 9);

            healthBadge = new GameObject("Health badge").transform;
            healthBadge.SetParent(transform, false);
            badgeBubble = AddLayer(healthBadge, "Health badge bubble", disc, BubbleColor, 12);
            badgeBubble.transform.localScale = new Vector3(.56f, .56f, 1);

            sniffle = AddLayer(healthBadge, "Sniffle drop", disc, SniffleColor, 13);
            sniffle.transform.localPosition = new Vector3(0, -.02f, 0);
            sniffle.transform.localScale = new Vector3(.17f, .23f, 1);
            sniffleShine = AddLayer(healthBadge, "Sniffle shine", disc, Color.white, 14);
            sniffleShine.transform.localPosition = new Vector3(-.035f, .02f, 0);
            sniffleShine.transform.localScale = new Vector3(.05f, .07f, 1);

            var fever = new GameObject("Fever icon").transform;
            fever.SetParent(healthBadge, false);
            fever.localRotation = Quaternion.Euler(0, 0, -28);
            thermometer = AddLayer(fever, "Fever thermometer", soft, TubeColor, 13);
            thermometer.transform.localPosition = new Vector3(0, .04f, 0);
            thermometer.transform.localScale = new Vector3(.10f, .34f, 1);
            thermometerFill = AddLayer(fever, "Fever thermometer fill", soft, FeverColor, 14);
            thermometerFill.transform.localPosition = new Vector3(0, -.03f, 0);
            thermometerFill.transform.localScale = new Vector3(.045f, .19f, 1);
            thermometerBulb = AddLayer(fever, "Fever thermometer bulb", disc, FeverColor, 14);
            thermometerBulb.transform.localPosition = new Vector3(0, -.14f, 0);
            thermometerBulb.transform.localScale = new Vector3(.12f, .12f, 1);

            var medicine = new GameObject("Medicine icon").transform;
            medicine.SetParent(healthBadge, false);
            medicine.localRotation = Quaternion.Euler(0, 0, 35);
            capsuleWarm = AddLayer(medicine, "Medicine capsule", soft, CapsuleWarm, 13);
            capsuleWarm.transform.localPosition = new Vector3(-.065f, 0, 0);
            capsuleWarm.transform.localScale = new Vector3(.15f, .12f, 1);
            capsuleLight = AddLayer(medicine, "Medicine capsule light", soft, CapsuleLight, 13);
            capsuleLight.transform.localPosition = new Vector3(.065f, 0, 0);
            capsuleLight.transform.localScale = new Vector3(.15f, .12f, 1);
            PoseHealth(0, 0);
        }

        // The health cues follow the pose on screen: the blanket covers the lower body (the legs of a nap pose,
        // the lap of an awake one) and the bubble floats beside the head, inside the room.
        private void PoseHealth(float now, float resting)
        {
            float badge = Mathf.Max(unwellLook, Mathf.Max(sickLook, recoveringLook));
            SetShade(badgeBubble, BubbleColor, badge);
            SetShade(sniffle, SniffleColor, unwellLook);
            SetShade(sniffleShine, Color.white, unwellLook);
            SetShade(thermometer, TubeColor, sickLook);
            SetShade(thermometerFill, FeverColor, sickLook);
            SetShade(thermometerBulb, FeverColor, sickLook);
            SetShade(capsuleWarm, CapsuleWarm, recoveringLook);
            SetShade(capsuleLight, CapsuleLight, recoveringLook);
            float cover = Mathf.Max(sickLook, recoveringLook * .9f);
            SetShade(blanket, Color.white, cover);
            // The quilt draws its own fold; the old trim strip stays hidden.
            SetShade(blanketTrim, BlanketTrimColor, 0);

            var pose = ShownPose(resting > .5f);
            if (pose == null || (badge <= 0 && cover <= 0))
                return;

            var size = pose.sprite.bounds.size;
            float width = size.x * pose.transform.localScale.x;
            float height = size.y * pose.transform.localScale.y;
            bool napping = resting > .5f;
            var blanketCenter = napping ? new Vector2(-.10f * width, .19f * height) : new Vector2(0, .14f * height);
            var blanketSize = napping ? new Vector2(.70f * width, .38f * height) : new Vector2(.76f * width, .30f * height);
            blanket.transform.localPosition = new Vector3(blanketCenter.x, blanketCenter.y, 0);
            blanket.transform.localScale = Vector3.one;
            // Tiled, so the outline and checks keep their size whatever the quilt's shape.
            blanket.size = blanketSize;
            float trim = Mathf.Min(.12f, blanketSize.y * .24f);
            blanketTrim.transform.localPosition = new Vector3(blanketCenter.x, blanketCenter.y + (blanketSize.y - trim) * .5f, 0);
            blanketTrim.transform.localScale = new Vector3(blanketSize.x * .98f, trim, 1);

            float left = Mathf.Max(baby.localPosition.x - .5f * width - .05f, -.5f * visibleWidth + .32f);
            float bob = Mathf.Sin(now * 1.6f) * .03f;
            healthBadge.localPosition = new Vector3(left, baby.localPosition.y + .80f * height + bob, 0);
        }

        // The awake or nap layer of the stage being shown, or null without art.
        private SpriteRenderer ShownPose(bool napping)
        {
            var layers = PoseLayers();
            int stage = Mathf.Clamp(Mathf.RoundToInt(stageBlend), 0, 3);
            var layer = napping && layers[stage * 2 + 1] != null ? layers[stage * 2 + 1] : layers[stage * 2];
            return layer == null || layer.sprite == null ? null : layer;
        }

        private static void SetShade(SpriteRenderer layer, Color color, float opacity)
        {
            if (layer == null)
                return;
            layer.enabled = opacity > 0;
            layer.color = new Color(color.r, color.g, color.b, color.a * opacity);
        }

        private void BuildFallbackToy()
        {
            var shape = Shape(.47f, false);
            var handle = AddLayer(rattle.transform, "Rattle handle", shape, new Color32(182, 130, 92, 255), 10);
            handle.transform.localScale = new Vector3(.09f, .25f, 1);
            var head = AddLayer(rattle.transform, "Rattle top", shape, new Color32(228, 165, 83, 255), 11);
            head.transform.localPosition = new Vector3(0, .13f, 0);
            head.transform.localScale = new Vector3(.22f, .21f, 1);
        }

        private void Update()
        {
            if (!Mathf.Approximately(aspect, nurseryCamera.aspect))
                Frame();
            UpdateTheme();

            var now = Time.unscaledTime;
            var elapsed = now - reactionStarted;
            float response = Window(elapsed, 0, .22f, 1.55f, PlayDuration);
            float feed = reaction == Reaction.Satisfied ? response : 0;
            float soothe = reaction == Reaction.Calmed ? response : 0;
            float rest = reaction == Reaction.Rested ? Window(elapsed, 0, .65f, 3.10f, 4.35f) : 0;
            float soundComfort = reaction == Reaction.NewSoundComforted ? Window(elapsed, 0, .40f, 1.47f, 2.52f) : 0;
            float soundExplore = reaction == Reaction.NewSoundExplored ? Window(elapsed, 0, .36f, 2.13f, 2.87f) : 0;
            float reading = reaction == Reaction.EnjoysStory ? Window(elapsed, 0, .40f, 2.20f, 2.92f) : 0;
            float fresh = reaction == Reaction.FreshDiaper ? Window(elapsed, 0, .32f, 1.66f, DiaperCueDuration) : 0;
            float bath = reaction == Reaction.FreshBath ? Window(elapsed, 0, .36f, 1.90f, BathCueDuration) : 0;
            float blocks = reaction == Reaction.StackedBlocks ? Window(elapsed, 0, .30f, 1.75f, BlocksCueDuration) : 0;
            // A bored baby gives the same small head shake, without pushing anything away.
            bool refusing = reaction == Reaction.PushesToyAway || reaction == Reaction.TooFull || reaction == Reaction.TooPoorly ||
                reaction == Reaction.Bored;
            float refuse = refusing ? Window(elapsed, 0, .18f, 1.15f, RefuseCueDuration) : 0;
            float refuseShake = Mathf.Sin(elapsed * 17f) * 5.5f * refuse;
            float pushAway = reaction == Reaction.PushesToyAway ? refuse : 0;
            float tooFull = reaction == Reaction.TooFull ? refuse : 0;
            float targetDistress = !adopted ? 0 : mood == CareMood.Upset ? 1 : mood == CareMood.Fussy ? .5f : 0;
            // A new baby on screen snaps to its mood; a change in mood eases in.
            distress = distress < 0 ? targetDistress : Mathf.MoveTowards(distress, targetDistress, Time.unscaledDeltaTime * 1.2f);
            EaseHealthLook();
            // A sick baby stays in bed: the nap pose holds while the condition lasts.
            float resting = Mathf.Max(rest, sickLook);
            float upset = Mathf.Max(0, distress - .5f) * 2;
            float fidget = (Mathf.Sin(now * 6.1f) * 2.2f + Mathf.Sin(now * 14.3f) * 1.6f * upset) * distress * (1 - resting);
            bool playing = IsPlayReaction(reaction) && elapsed < ReactionDuration(reaction);
            bool crawling = playing && reaction == Reaction.CrawlsToToy;
            bool walking = playing && reaction == Reaction.WalksToToy;
            float play = playing && !crawling && !walking ? response : 0;
            float reach = reaction == Reaction.ReachesForToy ? Window(elapsed, .25f, .85f, 1.40f, 2.20f) : 0;
            float crawl = crawling ? Window(elapsed, .35f, 1.45f, 2.15f, CrawlDuration) : 0;
            float stride = Mathf.Sin(crawl * Mathf.PI * 6);
            float walk = walking ? Window(elapsed, .32f, 1.68f, 2.34f, WalkDuration) : 0;
            float step = Mathf.Sin(walk * Mathf.PI * 6);

            stageBlend = Mathf.MoveTowards(stageBlend, targetStage, Time.unscaledDeltaTime * 2.45f);
            SetStageOpacity(Mathf.SmoothStep(0, 1, resting));

            float breath = Mathf.Sin(now * 2.25f) * Mathf.Lerp(.008f, .004f, Mathf.Max(resting, bath));
            float walkerWeight = StageOpacity(3);
            float stageOffset = -.04f * StageOpacity(2) - .085f * walkerWeight;
            // Leave the walker's feet above the expanded nursery card.
            float stageGround = QuiltSurface + WalkerGround * walkerWeight;
            float travel = visibleWidth * (stageOffset + .13f * (crawl + stride * .022f) + .215f * (walk + step * .023f));
            float lift = Mathf.Abs(stride) * .055f + Mathf.Abs(step) * .075f;
            baby.localPosition = new Vector3(.08f * play + .10f * reach + travel - .05f * pushAway,
                stageGround - .024f * Mathf.Min(1, resting + soundComfort) - .018f * bath + .022f * feed + lift - .03f * upset
                - .02f * unwellLook, 0);
            baby.localRotation = Quaternion.Euler(0, 0,
                Mathf.Sin(now * 1.25f) * soothe * 4 - 4 * resting - 4 * reach - stride * 1.65f
                - 2.6f * reading + 2.8f * soundComfort - 3.8f * soundExplore + 1.7f * bath - 2.8f * step
                - 2.8f * blocks + fidget + refuseShake + 3.5f * upset * (1 - resting) + 2.4f * unwellLook * (1 - resting));
            baby.localScale = new Vector3(1 + breath * .5f,
                1 + breath + .022f * feed + .016f * (reading + soundExplore) + .010f * fresh + .014f * blocks
                - .025f * upset - .018f * unwellLook, 1);
            shade.transform.localPosition = new Vector3(travel, stageGround - .06f, 0);
            shade.transform.localScale = new Vector3(visibleWidth * Mathf.Lerp(.75f, .45f, walkerWeight * (1 - rest)), .46f, 1);

            var displayedToy = ShowsOfferedToy(elapsed) ? playedToy : ambientToy;
            UpdateToyVisibility(elapsed);
            ShowToy(displayedToy);
            PoseToy(displayedToy, now, play, reach, crawling || walking, crawl + walk, pushAway);
            PoseStorybook(elapsed, reading);
            PoseFreshDiaper(elapsed, fresh);
            PoseBath(elapsed, bath);
            PoseBlockTower(elapsed, blocks);
            PoseFeeding(elapsed, Mathf.Max(feed, tooFull * .85f));
            PoseMess();
            PoseHealth(now, resting);
            PoseChime(now, elapsed, soundComfort, soundExplore);
        }

        // A new baby on screen snaps to its condition; a change eases in.
        private void EaseHealthLook()
        {
            float targetUnwell = adopted && condition == CareCondition.UnderTheWeather ? 1 : 0;
            float targetSick = adopted && condition == CareCondition.Sick ? 1 : 0;
            float targetRecovering = adopted && condition == CareCondition.Recovering ? 1 : 0;
            float step = snapHealthLook ? 1 : Time.unscaledDeltaTime * 1.4f;
            snapHealthLook = false;
            unwellLook = Mathf.MoveTowards(unwellLook, targetUnwell, step);
            sickLook = Mathf.MoveTowards(sickLook, targetSick, step);
            recoveringLook = Mathf.MoveTowards(recoveringLook, targetRecovering, step);
        }

        private void UpdateTheme()
        {
            if (peachBackdrop == null || peachOpacity == targetPeachOpacity)
                return;

            const float fadeSeconds = .28f;
            peachOpacity = Mathf.MoveTowards(peachOpacity, targetPeachOpacity, Time.unscaledDeltaTime / fadeSeconds);
            SetOpacity(peachBackdrop, Mathf.SmoothStep(0, 1, peachOpacity));
            backdrop.enabled = peachOpacity < 1;
        }

        private void PoseChime(float now, float elapsed, float comfort, float explore)
        {
            float previewAge = now - soundPreviewStarted;
            float preview = playedSoundPreview ? Window(previewAge, 0, .26f, .80f, 1.46f) : 0;
            float opacity = Mathf.Max(preview, Mathf.Max(comfort, explore));
            SetOpacity(nurseryChime, opacity);
            if (opacity <= 0)
                return;

            // The hinge is attached to the room, so the ornament swings away from the baby.
            float previewSwing = Mathf.Sin(previewAge * 9f) * preview * 6;
            float responseSwing = Mathf.Sin(elapsed * 8f) * (explore * 5 + comfort * Mathf.Exp(-elapsed * 2.2f) * 3);
            nurseryChime.transform.parent.localRotation = Quaternion.Euler(0, 0, previewSwing + responseSwing);
        }

        private void UpdateToyVisibility(float elapsed)
        {
            bool diaperCue = reaction == Reaction.FreshDiaper && elapsed < DiaperCueDuration;
            bool feedingCue = (reaction == Reaction.Satisfied && elapsed < PlayDuration) ||
                (reaction == Reaction.TooFull && elapsed < RefuseCueDuration);
            bool bathCue = reaction == Reaction.FreshBath && elapsed < BathCueDuration;
            bool blocksCue = reaction == Reaction.StackedBlocks && elapsed < BlocksCueDuration;
            bool visible = adopted && !diaperCue && !feedingCue && !bathCue && !blocksCue;
            if (toy.gameObject.activeSelf != visible)
                toy.gameObject.SetActive(visible);
        }

        private void PoseBlockTower(float elapsed, float blocks)
        {
            SetOpacity(blockTower, blocks);
            if (blocks <= 0)
                return;

            float settling = Mathf.Exp(-elapsed * 5.4f);
            blockTower.transform.localPosition = new Vector3(visibleWidth * .38f,
                -1.16f + towerHeight * .5f + .15f * settling, 0);
            blockTower.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(elapsed * 14f) * 6 * settling);
        }

        private void PoseFeeding(float elapsed, float feed)
        {
            foreach (var entry in feedingProps)
                SetOpacity(entry.Value, entry.Key == servedFood ? feed : 0);

            if (feed <= 0 || !feedingProps.TryGetValue(servedFood, out var prop))
                return;

            float lift = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .43f, elapsed));
            prop.transform.localPosition = new Vector3(visibleWidth * .37f, -.57f + lift * .40f, 0);
            var tilt = servedFood == Food.Oats ? -2 : -8;
            prop.transform.localRotation = Quaternion.Euler(0, 0, lift * tilt);
        }

        private void PoseFreshDiaper(float elapsed, float fresh)
        {
            SetOpacity(freshDiaper, fresh);
            if (fresh <= 0)
                return;

            float lift = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .44f, elapsed));
            freshDiaper.transform.localPosition = new Vector3(visibleWidth * .37f, -.57f + lift * .34f, 0);
            freshDiaper.transform.localRotation = Quaternion.Euler(0, 0, 8 - lift * 12);
        }

        private void PoseBath(float elapsed, float bath)
        {
            SetOpacity(bathBasket, bath);
            if (bath <= 0)
                return;

            float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .46f, elapsed));
            bathBasket.transform.localPosition = new Vector3(visibleWidth * .365f, -.48f + settle * .29f, 0);
            bathBasket.transform.localRotation = Quaternion.Euler(0, 0, 6 - settle * 8);
        }

        private void PoseStorybook(float elapsed, float reading)
        {
            SetOpacity(storybook, reading);
            if (reading <= 0)
                return;

            float lift = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .55f, elapsed));
            storybook.transform.localPosition = new Vector3(visibleWidth * .385f,
                -.11f + lift * .32f + Mathf.Sin(elapsed * 3.2f) * .017f, 0);
            storybook.transform.localRotation = Quaternion.Euler(0, 0, 10 - lift * 15);
        }

        private static bool IsPlayReaction(Reaction value)
        {
            return value == Reaction.TracksToy || value == Reaction.ReachesForToy ||
                value == Reaction.CrawlsToToy || value == Reaction.WalksToToy;
        }

        private static float ReactionDuration(Reaction value)
        {
            switch (value)
            {
                case Reaction.CrawlsToToy: return CrawlDuration;
                case Reaction.WalksToToy: return WalkDuration;
                default: return PlayDuration;
            }
        }

        private void ShowToy(Toy selectedToy)
        {
            bool showPlush = selectedToy == Toy.Plush && plush.sprite != null;
            if (rattle.gameObject.activeSelf == showPlush)
                rattle.gameObject.SetActive(!showPlush);
            if (plush.gameObject.activeSelf != showPlush)
                plush.gameObject.SetActive(showPlush);
        }

        private void PoseToy(Toy displayedToy, float now, float play, float reach, bool floorPlay, float approach,
            float pushAway)
        {
            bool isPlush = displayedToy == Toy.Plush;
            // A pushed-away toy slides a little further from the baby.
            var offered = new Vector3(visibleWidth * (.39f - play * .07f + pushAway * .045f),
                -1.17f + play * .79f + Mathf.Sin(now * 8f) * play * .075f, 0);
            if (floorPlay)
            {
                float touch = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.84f, 1, approach));
                if (reaction == Reaction.CrawlsToToy && crawler.sprite != null)
                {
                    // The shorter crawler artwork plants its forward hand inside the silhouette.
                    var crawlerSize = crawler.sprite.bounds.size;
                    var hand = crawler.transform.TransformPoint(new Vector3(crawlerSize.x * .31f, -crawlerSize.y * .38f, 0));
                    float handX = transform.InverseTransformPoint(hand).x + .17f;
                    offered.x = Mathf.Lerp(offered.x, Mathf.Min(offered.x, handX), touch);
                }

                toy.localPosition = offered + new Vector3(0, touch * .067f, 0);
            }
            else if (reach > 0 && sitter.sprite != null)
            {
                // Align the lower toy edge with the raised hand painted into the sitter pose.
                var spriteSize = sitter.sprite.bounds.size;
                var hand = sitter.transform.TransformPoint(new Vector3(spriteSize.x * .40f, -spriteSize.y * .08f, 0));
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
            float contact = floorPlay ? Mathf.Max(0, approach - .85f) * 70 : 0;
            toy.localRotation = Quaternion.Euler(0, 0, angle + Mathf.Sin(now * 9f) * (wiggle + contact));
        }

        private void SetStageOpacity(float resting)
        {
            SetPose(0, newborn, newbornRest, StageOpacity(0), resting);
            SetPose(1, sitter, sitterRest, StageOpacity(1), resting);
            SetPose(2, crawler, crawlerRest, StageOpacity(2), resting);
            SetPose(3, walker, walkerRest, StageOpacity(3), resting);
        }

        private float StageOpacity(int stage)
        {
            return Mathf.SmoothStep(1, 0, Mathf.Abs(stageBlend - stage));
        }

        // Pose indices follow PoseLayers: stage * 2 is awake, stage * 2 + 1 is the nap.
        private void SetPose(int stage, SpriteRenderer awake, SpriteRenderer nap, float opacity, float resting)
        {
            float awakeOpacity = opacity * (nap == null ? 1 : 1 - resting);
            float napOpacity = opacity * resting;
            // A pose of the current look that is about to appear is built first (stage changes only).
            if (awakeOpacity > 0)
                EnsurePoseBuilt(stage * 2);
            if (napOpacity > 0 && nap != null)
                EnsurePoseBuilt(stage * 2 + 1);
            var poorly = Mathf.Clamp01(.6f * unwellLook + sickLook + .3f * recoveringLook);
            var tint = Color.Lerp(Color.white, UpsetTint, Mathf.Clamp01(distress)) * Color.Lerp(Color.white, PoorlyTint, poorly);
            SetTinted(awake, awakeOpacity, tint);
            SetTinted(nap, napOpacity, tint);
        }

        private static void SetTinted(SpriteRenderer layer, float opacity, Color tint)
        {
            if (layer == null)
                return;
            layer.enabled = opacity > 0;
            layer.color = new Color(tint.r, tint.g, tint.b, opacity);
        }

        private void PoseMess()
        {
            for (var i = 0; i < messSpots.Length; i++)
            {
                var spot = messSpots[i];
                if (spot != null)
                    spot.enabled = adopted && spot.sprite != null && i < messItems;
            }
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
            float worldHeight = 2 * nurseryCamera.orthographicSize;
            FrameBackdrop(backdrop, worldWidth, worldHeight);
            FrameBackdrop(peachBackdrop, worldWidth, worldHeight);
            if (rug != null)
            {
                rug.transform.localPosition = new Vector3(0, -1.76f, 0);
                rug.transform.localScale = new Vector3(visibleWidth * 1.34f, .83f, 1);
                cushion.transform.localPosition = new Vector3(0, -1.42f, 0);
                cushion.transform.localScale = new Vector3(visibleWidth * .92f, .73f, 1);
            }

            shade.transform.localPosition = new Vector3(0, QuiltSurface - .06f, 0);
            shade.transform.localScale = new Vector3(visibleWidth * .75f, .46f, 1);
            AnchorStages();
            Fit(rattle, .46f, .46f);
            Fit(plush, .59f, .59f);
            Fit(storybook, Mathf.Min(1.06f, visibleWidth * .24f), .76f);
            Fit(freshDiaper, Mathf.Min(1f, visibleWidth * .23f), .66f);
            Fit(bathBasket, Mathf.Min(1.13f, visibleWidth * .25f), .83f);
            towerHeight = Fit(blockTower, Mathf.Min(.98f, visibleWidth * .22f), 1.22f);
            float foodWidth = Mathf.Min(1f, visibleWidth * .23f);
            foreach (var prop in feedingProps.Values)
                Fit(prop, foodWidth, .83f);
            for (var i = 0; i < messSpots.Length; i++)
            {
                if (messSpots[i] == null || messSpots[i].sprite == null)
                    continue;
                Fit(messSpots[i], Mathf.Min(.40f, visibleWidth * .11f), .26f);
                messSpots[i].transform.localPosition = new Vector3(visibleWidth * MessX[i], MessY[i], 0);
                messSpots[i].transform.localRotation = Quaternion.Euler(0, 0, MessAngle[i]);
            }
            float chimeHeight = Fit(nurseryChime, Mathf.Min(.65f, visibleWidth * .17f), 1.08f);
            nurseryChime.transform.localPosition = new Vector3(0, -chimeHeight * .5f, 0);
            nurseryChime.transform.parent.localPosition = new Vector3(visibleWidth * .405f, 2.06f, 0);
            baby.localPosition = new Vector3(0, QuiltSurface, 0);
        }

        private static void FrameBackdrop(SpriteRenderer layer, float worldWidth, float worldHeight)
        {
            if (layer == null)
                return;

            float artHeight = Fit(layer, worldWidth, worldHeight, true);
            float overscan = Mathf.Max(0, (artHeight - worldHeight) * .5f);
            // Both illustrations place the quilt center 24% up from the bottom.
            float y = Mathf.Clamp(QuiltSurface + artHeight * .26f, -overscan, overscan);
            layer.transform.localPosition = new Vector3(0, y, 0);
        }

        private void AnchorStages()
        {
            AnchorStage(newborn, .94f);
            AnchorStage(sitter, .87f);
            AnchorStage(newbornRest, .94f);
            AnchorStage(sitterRest, .87f);
            AnchorStage(crawler, .80f, 2.95f);
            AnchorStage(crawlerRest, .83f, 2.95f);
            AnchorStage(walker, .68f, WalkerHeight);
            AnchorStage(walkerRest, .76f, 3.07f);
        }

        private void AnchorStage(SpriteRenderer layer, float widthFraction, float maxHeight = PoseHeight)
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

        // A nine-slice quilt tile: rounded top corners, softer bottom ones, a 4 px outline, diagonal checks whose
        // period matches the tiled centre, a light fold along the top and a dashed stitch inside the edge.
        private Sprite Quilt()
        {
            const int size = QuiltTexture;
            const float outline = 4;
            const float topRadius = 44;
            const float bottomRadius = 30;
            const float check = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Quilt",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            var half = size / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    float px = x + .5f - half;
                    float py = y + .5f - half;
                    float radius = py > 0 ? topRadius : bottomRadius;
                    float qx = Mathf.Abs(px) - (half - radius);
                    float qy = Mathf.Abs(py) - (half - radius);
                    float distance = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude +
                        Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                    float coverage = Mathf.Clamp01(.5f - distance);
                    var fill = (Color)QuiltFill;
                    bool light = (Mathf.FloorToInt((x + y) / check) + Mathf.FloorToInt((x - y + size) / check)) % 2 == 0;
                    if (light)
                        fill = Color.Lerp(fill, Color.white, .35f);
                    float fromTop = size - y;
                    if (fromTop > outline && fromTop < outline + 11)
                        fill = Color.Lerp(fill, Color.white, .45f);
                    float inside = -distance;
                    if (inside > 11 && inside < 13.5f && (x + y) % 16 < 7)
                        fill = Color.Lerp(fill, Color.white, .6f);
                    float edge = Mathf.Clamp01(outline + .5f - inside);
                    var color = Color.Lerp(fill, (Color)QuiltOutline, edge);
                    color.a = coverage;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), QuiltPixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(QuiltBorder, QuiltBorder, QuiltBorder, QuiltBorder), false);
            sprite.name = "Quilt";
            generatedAssets.Add(sprite);
            generatedAssets.Add(texture);
            return sprite;
        }

        internal static Sprite SpriteFrom(Texture2D texture, float pixelsPerUnit)
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
            while (builtLooks.Count > 0)
                DiscardLook(builtLooks[0]);
            foreach (var (_, look) in photoSprites)
            {
                foreach (var asset in look.Assets)
                    Destroy(asset);
            }

            photoSprites.Clear();
            ReleaseAppearanceDecoder();
        }

        private sealed class BuiltLook
        {
            public BuiltLook(BabyAppearance appearance, int poseCount)
            {
                Appearance = appearance;
                Sprites = new Sprite[poseCount];
            }

            public BabyAppearance Appearance { get; }
            // Built pose sprites in PoseLayers order; null until a pose is needed.
            public Sprite[] Sprites { get; }
            // Textures and sprites owned by this look, destroyed when it leaves the cache.
            public List<Object> Assets { get; } = new List<Object>();
        }
    }
}
