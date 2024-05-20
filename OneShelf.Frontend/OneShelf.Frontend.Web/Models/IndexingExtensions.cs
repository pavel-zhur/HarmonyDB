namespace OneShelf.Frontend.Web.Models
{
    public static class IndexingExtensions
    {
        public static string ToLetterIndex(this int index)
        {
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            if (index >= letters.Length * (letters.Length + 1))
            {
                return index.ToString();
            }

            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Negatives are not supported.");

            var a = index % letters.Length;
            var b = index / letters.Length;

            return b == 0 ? letters[a].ToString() : $"{letters[b - 1]}{letters[a]}";
        }
    }
}
