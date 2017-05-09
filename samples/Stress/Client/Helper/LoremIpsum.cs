using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Helper
{
    // http://stackoverflow.com/questions/4286487/is-there-any-lorem-ipsum-generator-in-c
    public class LoremIpsum
    {
        private static readonly string[] Words = {"lorem", "ipsum", "dolor", "sit", "amet", "consectetuer",
            "adipiscing", "elit", "sed", "diam", "nonummy", "nibh", "euismod",
            "tincidunt", "ut", "laoreet", "dolore", "magna", "aliquam", "erat"};

        public static string Generate(int minWords, int maxWords, int minSentences, int maxSentences, int numParagraphs)
        {
            var rand = new Random();
            int numSentences = rand.Next(maxSentences - minSentences) + minSentences + 1;
            int numWords = rand.Next(maxWords - minWords) + minWords + 1;

            StringBuilder result = new StringBuilder();

            for (int p = 0; p < numParagraphs; p++)
            {
                result.Append("\r\n");
                for (int s = 0; s < numSentences; s++)
                {
                    for (int w = 0; w < numWords; w++)
                    {
                        if (w > 0) { result.Append(" "); }
                        result.Append(Words[rand.Next(Words.Length)]);
                    }
                    result.Append(". ");
                }
                result.Append("\r\n");
            }

            return result.ToString();
        }
    }
}
