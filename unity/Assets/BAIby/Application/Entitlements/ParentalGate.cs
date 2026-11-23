using System;

namespace BAIby.Application
{
    // An adult task in front of every purchase, restore and external link: type a number that is written
    // in words, using digits. The number changes every time the gate opens and after every wrong answer,
    // and the gate gives no hints. It is not a password; it keeps young children from wandering through.
    public sealed class ParentalGate
    {
        private static readonly string[] Words =
        {
            "twenty-seven", "thirty-four", "forty-one", "forty-eight", "fifty-three", "fifty-nine",
            "sixty-two", "sixty-six", "seventy-one", "seventy-five", "eighty-four", "eighty-seven",
            "ninety-two", "ninety-six", "one hundred and thirteen", "one hundred and fifty"
        };

        private static readonly int[] Numbers =
        {
            27, 34, 41, 48, 53, 59, 62, 66, 71, 75, 84, 87, 92, 96, 113, 150
        };

        private readonly Func<int, int> pick;
        private int current = -1;

        // pick(n) returns a random index from 0 to n - 1, such as System.Random.Next.
        public ParentalGate(Func<int, int> pick)
        {
            this.pick = pick ?? throw new ArgumentNullException(nameof(pick));
            Next();
        }

        public static int ChallengeCount => Words.Length;

        // The challenge in words, for example "forty-eight".
        public string Prompt => Words[current];

        // Draws a new challenge that differs from the one on screen.
        public void Next()
        {
            var index = pick(Words.Length);
            if (index < 0 || index >= Words.Length)
                throw new InvalidOperationException("The gate picked a challenge outside its list.");
            if (index == current)
                index = (index + 1) % Words.Length;
            current = index;
        }

        // Accepts the number in digits only, ignoring surrounding spaces. Anything else is wrong.
        public bool Check(string answer)
        {
            if (answer == null)
                return false;

            var trimmed = answer.Trim();
            if (trimmed.Length == 0 || trimmed.Length > 4)
                return false;

            foreach (var character in trimmed)
            {
                if (character < '0' || character > '9')
                    return false;
            }

            return int.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture) == Numbers[current];
        }

        // The digits for a prompt from the fixed list, for automated UI tests. Null for anything else.
        public static string AnswerFor(string prompt)
        {
            var index = Array.IndexOf(Words, prompt);
            return index < 0 ? null : Numbers[index].ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
