using System.Collections.Generic;
using BAIby.Simulation;
using BAIby.Simulation.Pets;
using Unity.Collections;
using UnityEngine;

namespace BAIby.Character
{
    // Draws one pet in the nursery: its awake and rest poses for the current stage, composited from the
    // species art (base, breed markings, accessory) and recolored with the shared recolor math. Only the
    // shown pet's poses stay in memory: a new look or stage destroys the old textures and sprites, each
    // pose is built when it is first needed, and decoded art is not kept between builds. Missing or
    // broken art logs a warning and shows a calm placeholder shape instead of the pet.
    public sealed class PetVisuals : MonoBehaviour
    {
        private const string Art = "BAIby/Pets/";
        private const float PixelsPerUnit = 100;
        private const int CachedPortraits = 8;
        private const float CueDuration = 2.2f;
        private const float NapDuration = 3.6f;

        private readonly List<Object> lookAssets = new List<Object>();
        private readonly List<Object> generatedAssets = new List<Object>();
        private readonly List<Portrait> portraits = new List<Portrait>();
        private readonly Dictionary<Species, PetLayout> layouts = new Dictionary<Species, PetLayout>();
        private readonly HashSet<Species> missingLayouts = new HashSet<Species>();
        private NurseryVisuals room;
        private Transform rig;
        private SpriteRenderer awake;
        private SpriteRenderer rest;
        private SpriteRenderer shade;
        private Sprite placeholder;
        private Texture2D decoder;
        private PetComposer composer;
        private string artFolder;
        private Look look;
        private Sprite awakeSprite;
        private Sprite restSprite;
        private bool awakeFailed;
        private bool restFailed;
        private bool wantsRest;
        private float restBlend;
        private float floorY = -1.62f;
        private float pixelScale = .62f;
        private float visibleWidth = 5f;
        private PetVerb cue;
        private float cueStarted = -100;
        private float arrived = -100;

        // The pet stands in the nursery's room: on its floor line, drawn at the layout's runtime scale
        // relative to the baby's pose pixels, following the room as the screen changes shape.
        public static PetVisuals Create(NurseryVisuals room)
        {
            var visuals = new GameObject("BAIby Pet").AddComponent<PetVisuals>();
            visuals.room = room;
            if (room != null)
                visuals.transform.SetParent(room.transform, false);
            visuals.Build();
            visuals.gameObject.SetActive(false);
            return visuals;
        }

        public void SetShown(bool shown)
        {
            if (gameObject.activeSelf != shown)
                gameObject.SetActive(shown);
        }

        // Shows a pet's look at a stage. resting shows the rest pose (napping or sleepy). Building happens
        // here and in Update, only for the poses that are about to be visible.
        public void Show(PetAppearance appearance, PetCatalog catalog, PetStage stage, bool resting)
        {
            if (appearance == null || catalog == null)
                return;

            var next = Look.Of(appearance, catalog, stage);
            if (look == null || !look.Equals(next))
            {
                var grewUp = look != null && look.SameLook(next) && look.Stage != next.Stage;
                DiscardLook();
                look = next;
                if (grewUp)
                    arrived = Time.unscaledTime;
                restBlend = resting ? 1 : 0;
            }

            wantsRest = resting;
            EnsureSprites();
        }

        // A short body cue for a care action. A nap blends into the rest pose for a while.
        public void PlayCue(PetVerb verb)
        {
            cue = verb;
            cueStarted = Time.unscaledTime;
        }

        // A little "new arrival" pop, for adoption and the breed picker.
        public void PlayArrival() => arrived = Time.unscaledTime;

        // A portrait for a Home card: the awake pose of that look and stage at half size. Kept for the
        // few cards on Home and freed by ReleasePortraits. Null when the art could not be built.
        public Sprite PortraitFor(PetAppearance appearance, PetCatalog catalog, PetStage stage)
        {
            if (appearance == null || catalog == null)
                return null;

            var key = Look.Of(appearance, catalog, stage);
            for (int i = 0; i < portraits.Count; i++)
            {
                if (portraits[i].Key.Equals(key))
                    return portraits[i].Sprite;
            }

            Sprite sprite = null;
            try
            {
                sprite = BuildSprite(key, false, 2, "portrait");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("The pet portrait could not be built: " + exception.Message);
            }
            finally
            {
                ReleaseBuildScratch();
            }

            portraits.Insert(0, new Portrait(key, sprite));
            while (portraits.Count > CachedPortraits)
            {
                var evicted = portraits[portraits.Count - 1];
                portraits.RemoveAt(portraits.Count - 1);
                DestroySprite(evicted.Sprite);
            }

            return sprite;
        }

        public void ReleasePortraits()
        {
            foreach (var portrait in portraits)
                DestroySprite(portrait.Sprite);
            portraits.Clear();
        }

        private void Build()
        {
            rig = new GameObject("Pet rig").transform;
            rig.SetParent(transform, false);
            shade = AddLayer(transform, "Pet contact shade", SoftShape(true), new Color32(89, 68, 56, 56), -5);
            awake = AddLayer(rig, "Pet awake artwork", null, Color.white, 1);
            rest = AddLayer(rig, "Pet rest artwork", null, Color.white, 2);
            placeholder = SoftShape(false);
        }

        private void EnsureSprites()
        {
            if (look == null)
                return;

            bool built = false;
            try
            {
                if (awakeSprite == null && !awakeFailed && (restBlend < 1 || !wantsRest))
                {
                    built = true;
                    awakeSprite = TryBuild(false, ref awakeFailed);
                }

                if (restSprite == null && !restFailed && (wantsRest || restBlend > 0))
                {
                    built = true;
                    restSprite = TryBuild(true, ref restFailed);
                }
            }
            finally
            {
                if (built)
                    ReleaseBuildScratch();
            }

            awake.sprite = awakeSprite != null ? awakeSprite : placeholder;
            rest.sprite = restSprite != null ? restSprite : awake.sprite;
            var tint = awakeSprite == null ? PlaceholderTint() : Color.white;
            awake.color = new Color(tint.r, tint.g, tint.b, awake.color.a);
            var restTint = restSprite == null && awakeSprite == null ? PlaceholderTint() : Color.white;
            rest.color = new Color(restTint.r, restTint.g, restTint.b, rest.color.a);
        }

        private Sprite TryBuild(bool resting, ref bool failed)
        {
            try
            {
                return BuildSprite(look, resting, 1, resting ? "rest" : "awake", lookAssets);
            }
            catch (System.Exception exception)
            {
                failed = true;
                Debug.LogWarning("The pet's " + (resting ? "rest" : "awake") + " pose could not be built, so a placeholder is shown: " +
                    exception.Message);
                return null;
            }
        }

        private Sprite BuildSprite(Look key, bool resting, int downsample, string label, List<Object> owner = null)
        {
            var layout = LayoutFor(key.Species);
            if (layout == null)
                throw new System.InvalidOperationException("There is no art for the " + key.Species + " yet.");

            if (composer == null || composer.Layout != layout)
                composer = new PetComposer(layout);
            artFolder = Art + key.Species + "/";
            var pose = PetLayout.PoseName(key.Stage, resting);
            var pixels = composer.Compose(key.Breed, key.Coat, key.Accessory, key.Accent, pose, LoadArt, PixelRowOrder.BottomUp,
                out int width, out int height);

            if (downsample > 1)
                pixels = Downsample(pixels, ref width, ref height, downsample);

            int length = width * height * AppearanceRecolor.Channels;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = key.Species + " " + pose + " " + label;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var data = texture.GetPixelData<byte>(0);
            NativeArray<byte>.Copy(pixels, 0, data, 0, length);
            texture.Apply(false, true);

            // Pivot at the bottom center, so the pet stands on the floor line at any scale.
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, 0),
                PixelsPerUnit / downsample, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            sprite.name = texture.name;
            if (owner != null)
            {
                owner.Add(texture);
                owner.Add(sprite);
            }

            return sprite;
        }

        private PetLayout LayoutFor(Species species)
        {
            if (layouts.TryGetValue(species, out var cached))
                return cached;
            if (missingLayouts.Contains(species))
                return null;

            var asset = Resources.Load<TextAsset>(Art + species + "/layout");
            if (asset == null)
            {
                missingLayouts.Add(species);
                Debug.LogWarning("Missing Resources/" + Art + species + "/layout.json; the pet is shown as a placeholder.");
                return null;
            }

            try
            {
                var layout = PetLayout.FromData(JsonUtility.FromJson<PetLayoutData>(asset.text));
                layouts.Add(species, layout);
                return layout;
            }
            catch (System.Exception exception)
            {
                missingLayouts.Add(species);
                Debug.LogWarning("The " + species + " layout could not be read: " + exception.Message);
                return null;
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        // Decodes Resources/BAIby/Pets/{Species}/{path}.bytes into RGBA bytes with bottom-up rows, reusing
        // one decode texture for the whole build.
        private byte[] LoadArt(string path, int width, int height)
        {
            var asset = Resources.Load<TextAsset>(artFolder + path);
            if (asset == null)
                throw new System.InvalidOperationException("Missing Resources/" + artFolder + path + ".bytes.");

            try
            {
                if (decoder == null)
                {
                    decoder = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    decoder.name = "Pet art decoder";
                }

                if (!ImageConversion.LoadImage(decoder, asset.bytes, false))
                    throw new System.InvalidOperationException(path + " is not a readable PNG.");
                if (decoder.width != width || decoder.height != height)
                    throw new System.InvalidOperationException(path + " is " + decoder.width + "x" + decoder.height +
                        " but the layout expects " + width + "x" + height + ".");

                return NurseryVisuals.ReadRgba(decoder, width * height);
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        // Alpha-weighted box filter; the result is a new tightly packed buffer.
        private static byte[] Downsample(byte[] pixels, ref int width, ref int height, int factor)
        {
            int outWidth = Mathf.Max(1, width / factor);
            int outHeight = Mathf.Max(1, height / factor);
            var output = new byte[outWidth * outHeight * AppearanceRecolor.Channels];
            for (int y = 0; y < outHeight; y++)
            {
                for (int x = 0; x < outWidth; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0, count = 0;
                    for (int dy = 0; dy < factor; dy++)
                    {
                        for (int dx = 0; dx < factor; dx++)
                        {
                            int source = ((y * factor + dy) * width + x * factor + dx) * AppearanceRecolor.Channels;
                            int alpha = pixels[source + 3];
                            r += pixels[source] * alpha;
                            g += pixels[source + 1] * alpha;
                            b += pixels[source + 2] * alpha;
                            a += alpha;
                            count++;
                        }
                    }

                    int target = (y * outWidth + x) * AppearanceRecolor.Channels;
                    if (a > 0)
                    {
                        output[target] = (byte)((r + a / 2) / a);
                        output[target + 1] = (byte)((g + a / 2) / a);
                        output[target + 2] = (byte)((b + a / 2) / a);
                    }

                    output[target + 3] = (byte)((a + count / 2) / count);
                }
            }

            width = outWidth;
            height = outHeight;
            return output;
        }

        private void Update()
        {
            if (look == null)
                return;

            if (room != null)
            {
                floorY = room.FloorY;
                pixelScale = room.PosePixelScale > 0 ? room.PosePixelScale : pixelScale;
                visibleWidth = room.VisibleWidth > 0 ? room.VisibleWidth : visibleWidth;
            }

            var now = Time.unscaledTime;
            var sinceCue = now - cueStarted;
            bool napping = cue == PetVerb.Nap && sinceCue < NapDuration;
            float targetRest = wantsRest || napping ? 1 : 0;
            restBlend = Mathf.MoveTowards(restBlend, targetRest, Time.unscaledDeltaTime * 1.6f);
            if ((restBlend > 0 && restSprite == null && !restFailed) || (restBlend < 1 && awakeSprite == null && !awakeFailed))
            {
                // A nap cue can ask for the rest pose before Show did.
                bool previous = wantsRest;
                wantsRest = wantsRest || napping;
                EnsureSprites();
                wantsRest = previous;
            }

            float resting = Mathf.SmoothStep(0, 1, restBlend);
            SetAlpha(awake, 1 - resting);
            SetAlpha(rest, resting);

            float active = sinceCue < CueDuration ? Mathf.Sin(Mathf.Clamp01(sinceCue / CueDuration) * Mathf.PI) : 0;
            float hop = cue == PetVerb.Wand ? Mathf.Abs(Mathf.Sin(sinceCue * 7.5f)) * active : 0;
            float purr = cue == PetVerb.Pet ? Mathf.Sin(now * 30f) * active : 0;
            float eat = cue == PetVerb.Feed ? active : 0;
            float groom = cue == PetVerb.Brush ? Mathf.Sin(sinceCue * 5f) * active : 0;
            float pop = Mathf.Clamp01(1 - (now - arrived) / .45f);
            float breath = Mathf.Sin(now * 2.1f) * Mathf.Lerp(.010f, .005f, resting);

            float scale = pixelScale * (float)RuntimeScale();
            float squash = 1 - .05f * eat + .03f * Mathf.Sin(pop * Mathf.PI);
            rig.localPosition = new Vector3(visibleWidth * -.02f, floorY + .32f * hop - .02f * eat, 0);
            rig.localRotation = Quaternion.Euler(0, 0, 3.5f * groom + .6f * purr);
            rig.localScale = new Vector3(scale * (1 + breath * .5f + .02f * eat), scale * (squash + breath), 1);

            float shadeWidth = PoseWidth() * scale * Mathf.Lerp(.78f, .62f, hop);
            shade.transform.localPosition = new Vector3(visibleWidth * -.02f, floorY - .03f, 0);
            shade.transform.localScale = new Vector3(Mathf.Max(.4f, shadeWidth), .34f, 1);
        }

        private double RuntimeScale()
        {
            if (look != null && layouts.TryGetValue(look.Species, out var layout))
                return layout.RuntimeScale(look.Stage);
            return look != null && look.Stage == PetStage.Grown ? .7 : .55;
        }

        // The unscaled width of the shown pose (pose art is 100 pixels per unit).
        private float PoseWidth()
        {
            var sprite = awake.sprite;
            return sprite == null ? 4.2f : sprite.bounds.size.x;
        }

        private Color PlaceholderTint()
        {
            if (look == null)
                return Color.white;
            var coat = PetPalette.FallbackCoat(look.Coat);
            return new Color((float)coat.R / 255f, (float)coat.G / 255f, (float)coat.B / 255f, 1);
        }

        private static void SetAlpha(SpriteRenderer layer, float alpha)
        {
            layer.enabled = alpha > 0 && layer.sprite != null;
            var color = layer.color;
            layer.color = new Color(color.r, color.g, color.b, alpha);
        }

        private void DiscardLook()
        {
            foreach (var asset in lookAssets)
                Destroy(asset);
            lookAssets.Clear();
            awakeSprite = null;
            restSprite = null;
            awakeFailed = false;
            restFailed = false;
            if (awake != null)
                awake.sprite = null;
            if (rest != null)
                rest.sprite = null;
        }

        private void ReleaseBuildScratch()
        {
            // Runtime textures are not assets, so they can be freed immediately.
            if (decoder != null)
                DestroyImmediate(decoder);
            decoder = null;
            composer?.Release();
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
                return;
            var texture = sprite.texture;
            Destroy(sprite);
            if (texture != null)
                Destroy(texture);
        }

        // A soft rounded blob: the contact shade (feathered) and the placeholder body.
        private Sprite SoftShape(bool feather)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size - .5f;
                    float dy = (y + .5f) / size - .5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((.49f - distance) / (feather ? .49f : .03f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            // The shade is one unit across and centered; the placeholder is about as big as the pose art and
            // stands on the floor like it.
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, feather ? .5f : 0),
                feather ? size : size / 4.2f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            generatedAssets.Add(sprite);
            generatedAssets.Add(texture);
            return sprite;
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

        private void OnDestroy()
        {
            DiscardLook();
            ReleasePortraits();
            ReleaseBuildScratch();
            foreach (var asset in generatedAssets)
                Destroy(asset);
            generatedAssets.Clear();
        }

        // The parts of a look that change its pixels: breed, the coat actually rendered, the accessory
        // that has art, its accent and the stage.
        private sealed class Look : System.IEquatable<Look>
        {
            private Look(Species species, PetBreed breed, PetCoat coat, PetAccessory accessory, Outfit accent, PetStage stage)
            {
                Species = species;
                Breed = breed;
                Coat = coat;
                Accessory = accessory;
                Accent = accent;
                Stage = stage;
            }

            public Species Species { get; }
            public PetBreed Breed { get; }
            public PetCoat Coat { get; }
            public PetAccessory Accessory { get; }
            public Outfit Accent { get; }
            public PetStage Stage { get; }

            // A saved coat or accessory the catalog no longer offers renders with the breed default.
            public static Look Of(PetAppearance appearance, PetCatalog catalog, PetStage stage)
            {
                var coat = appearance.Coat;
                var accessory = appearance.Accessory;
                if (catalog.TryGet(appearance.Breed, out _))
                {
                    coat = catalog.EffectiveCoat(appearance);
                    accessory = catalog.EffectiveAccessory(appearance);
                }

                return new Look(appearance.Species, appearance.Breed, coat, accessory, appearance.AccessoryAccent, stage);
            }

            public bool SameLook(Look other) =>
                other != null && Species == other.Species && Breed == other.Breed && Coat == other.Coat &&
                Accessory == other.Accessory && Accent == other.Accent;

            public bool Equals(Look other) => SameLook(other) && Stage == other.Stage;

            public override bool Equals(object obj) => Equals(obj as Look);

            public override int GetHashCode() =>
                ((((int)Breed * 31 + (int)Coat) * 31 + (int)Accessory) * 31 + (int)Accent) * 31 + (int)Stage;
        }

        private readonly struct Portrait
        {
            public Portrait(Look key, Sprite sprite)
            {
                Key = key;
                Sprite = sprite;
            }

            public Look Key { get; }
            public Sprite Sprite { get; }
        }
    }
}
