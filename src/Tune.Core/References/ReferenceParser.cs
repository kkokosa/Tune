namespace Tune.Core.References
{
    public class ReferenceParser
    {
        private static readonly string referenceTag = "//#r";

        public static bool TryParse(string input, out string reference)
        {
            reference = string.Empty;
            var indexOfMetaTag = input.IndexOf(referenceTag);

            if (indexOfMetaTag == -1)
                return false;

            reference = input.Remove(indexOfMetaTag, referenceTag.Length).Trim();
            return true;
        }
    }
}
