using UnityEngine;
using static BAIby.Character.Modeling;

namespace BAIby.Character
{
    internal sealed class BabyRig
    {
        internal Transform Root { get; }
        internal Transform Torso { get; }
        internal Transform Head { get; }
        internal Transform Mouth { get; }
        internal Transform LowerLip { get; }
        internal Transform RightGrip { get; }
        internal Transform[] Eyes { get; } = new Transform[2];
        internal Transform[] ClosedLids { get; } = new Transform[2];
        internal Transform[] Pupils { get; } = new Transform[2];
        internal Transform[] Arms { get; } = new Transform[2];
        internal Transform[] Elbows { get; } = new Transform[2];
        internal Transform[] Legs { get; } = new Transform[2];

        internal BabyRig(Transform room)
        {
            var skin = Matte("#E8AA8C", 0.16f);
            var pink = Matte("#BD786B", 0.12f);
            var hair = Matte("#673F32");
            var brow = Matte("#9B684F");
            var eyelid = Matte("#CF957A");
            var white = Matte("#FCEDD9", 0.20f);
            var iris = Matte("#5B382E", 0.55f);
            var pupil = Matte("#251B20", 0.5f);
            var light = Matte("#FFFCF5");
            var jumper = Matte("#D48167");
            var seam = Matte("#F6C5A0");

            Root = Joint(room, "Baby rig", new Vector3(0, 0.37f, 0.00f));
            Torso = Joint(Root, "Torso", Vector3.zero);
            Loft(Torso, "Tailored romper", Vector3.zero, jumper,
                new Vector4(-.16f, .07f, .075f, .014f),
                new Vector4(-.12f, .17f, .13f, .016f),
                new Vector4(.01f, .217f, .145f, .01f),
                new Vector4(.15f, .198f, .146f, 0),
                new Vector4(.29f, .201f, .128f, .007f),
                new Vector4(.37f, .183f, .107f, .019f),
                new Vector4(.415f, .09f, .09f, .015f));
            Form(Torso, "Neck", new Vector3(0, .416f, -.006f), new Vector3(.18f, .18f, .16f), skin);
            Form(Torso, "Folded collar", new Vector3(0, .39f, -.075f), new Vector3(.233f, .065f, .066f), seam);
            for (int button = 0; button < 3; button++)
                Form(Torso, "Romper snap", new Vector3(0, .29f - button * .07f, -.13f - button * .007f),
                    new Vector3(.028f, .028f, .014f), seam);

            Head = Joint(Torso, "Head", new Vector3(0, .436f, -.015f));
            Loft(Head, "Baby face and cranium", Vector3.zero, skin,
                new Vector4(0, .06f, .067f, -.005f),
                new Vector4(.031f, .129f, .129f, -.032f),
                new Vector4(.076f, .208f, .173f, -.024f),
                new Vector4(.145f, .262f, .217f, -.002f),
                new Vector4(.230f, .278f, .228f, .012f),
                new Vector4(.328f, .271f, .238f, .026f),
                new Vector4(.421f, .231f, .213f, .025f),
                new Vector4(.479f, .155f, .160f, .019f),
                new Vector4(.509f, .022f, .036f, .014f));
            Loft(Head, "Small nose", new Vector3(0, .146f, -.198f), skin,
                new Vector4(.002f, .014f, .008f, .006f),
                new Vector4(.026f, .043f, .029f, -.012f),
                new Vector4(.047f, .037f, .030f, -.013f),
                new Vector4(.077f, .024f, .018f, -.007f),
                new Vector4(.100f, .006f, .007f, 0));
            var mouth = Joint(Head, "Mouth", new Vector3(0, .116f, -.209f));
            Mouth = Form(mouth, "Mouth opening", new Vector3(0, -.004f, -.003f),
                new Vector3(.075f, .009f, .017f), brow);
            LowerLip = Form(mouth, "Lower lip", new Vector3(0, -.008f, -.005f),
                new Vector3(.085f, .021f, .017f), pink);
            Form(mouth, "Upper lip", new Vector3(0, .005f, -.001f),
                new Vector3(.089f, .014f, .017f), pink);
            Form(Head, "Swept crown hair", new Vector3(-.012f, .467f, -.035f),
                new Vector3(.245f, .086f, .253f), hair).localRotation = Quaternion.Euler(0, -17, 10);
            Form(Head, "Side-swept curl", new Vector3(.042f, .467f, -.115f),
                new Vector3(.149f, .089f, .128f), hair).localRotation = Quaternion.Euler(0, -10, 27);

            for (int index = 0; index < 2; index++)
            {
                int side = index == 0 ? -1 : 1;
                Form(Head, "Ear", new Vector3(side * .264f, .171f, .014f),
                    new Vector3(.090f, .121f, .065f), skin);
                Form(Head, "Ear fold", new Vector3(side * .281f, .174f, -.012f),
                    new Vector3(.041f, .067f, .022f), pink);
                var socket = Joint(Head, "Eye socket", new Vector3(side * .112f, .236f, -.196f));
                socket.localRotation = Quaternion.Euler(0, -side * 20, side * 3);
                Form(socket, "Soft eyelid", Vector3.zero, new Vector3(.126f, .071f, .042f), eyelid);
                var eye = Eyes[index] = Joint(socket, "Blink", new Vector3(0, -.001f, -.008f));
                Form(eye, "Sclera", Vector3.zero, new Vector3(.109f, .057f, .039f), white);
                var gaze = Pupils[index] = Joint(eye, "Gaze", new Vector3(0, 0, -.018f));
                Form(gaze, "Iris", Vector3.zero, new Vector3(.048f, .051f, .013f), iris);
                Form(gaze, "Pupil", new Vector3(0, 0, -.006f), new Vector3(.029f, .033f, .005f), pupil);
                Form(gaze, "Catchlight", new Vector3(-.007f, .01f, -.009f), new Vector3(.010f, .010f, .005f), light);
                ClosedLids[index] = Form(socket, "Closed eyelid crease", new Vector3(0, -.003f, -.021f),
                    new Vector3(.096f, .011f, .008f), brow);
                ClosedLids[index].gameObject.SetActive(false);
                Form(socket, "Soft eyebrow", new Vector3(0, .059f, .004f),
                    new Vector3(.075f, .011f, .015f), brow);
                Form(Head, "Nostril", new Vector3(side * .020f, .171f, -.230f),
                    new Vector3(.011f, .006f, .005f), pink);

                var arm = Arms[index] = Joint(Torso, "Shoulder", new Vector3(side * .181f, .319f, .005f));
                Loft(arm, "Romper sleeve", Vector3.zero, jumper,
                    new Vector4(-.179f, .064f, .071f, 0),
                    new Vector4(-.109f, .078f, .08f, 0),
                    new Vector4(-.006f, .080f, .081f, 0),
                    new Vector4(.052f, .026f, .033f, 0));
                var elbow = Elbows[index] = Joint(arm, "Elbow", new Vector3(0, -.154f, 0));
                Loft(elbow, "Forearm", Vector3.zero, skin,
                    new Vector4(-.154f, .040f, .043f, .003f),
                    new Vector4(-.076f, .057f, .056f, -.001f),
                    new Vector4(.023f, .062f, .063f, .001f));
                Form(elbow, "Palm", new Vector3(0, -.159f, -.011f),
                    new Vector3(.10f, .118f, .066f), skin);
                Form(elbow, "Bent thumb", new Vector3(-side * .043f, -.157f, -.036f),
                    new Vector3(.055f, .070f, .047f), skin).localRotation = Quaternion.Euler(0, 0, -side * 21);
                for (int finger = 0; finger < 4; finger++)
                    Form(elbow, "Curled finger", new Vector3(-.034f + .022f * finger, -.197f, -.027f),
                        new Vector3(.026f, .054f - .004f * Mathf.Abs(finger - 1), .034f), skin);

                if (side == 1)
                    RightGrip = Joint(elbow, "Right hand grip", new Vector3(0, -.174f, -.047f));

                var leg = Legs[index] = Joint(Root, "Hip", new Vector3(side * .112f, -.04f, -.025f));
                Loft(leg, "Footed romper leg", Vector3.zero, jumper,
                    new Vector4(-.277f, .053f, .059f, -.01f),
                    new Vector4(-.211f, .069f, .086f, -.014f),
                    new Vector4(-.129f, .085f, .094f, -.01f),
                    new Vector4(-.037f, .104f, .104f, 0),
                    new Vector4(.055f, .075f, .075f, 0));
                Form(leg, "Romper foot", new Vector3(0, -.282f, -.03f),
                    new Vector3(.143f, .138f, .207f), jumper);
                Form(leg, "Ribbed foot panel", new Vector3(0, -.320f, -.037f),
                    new Vector3(.126f, .065f, .179f), seam);
            }
        }
    }
}
