namespace ChopChopGames.UGM.GoogleSheetTable
{
    public static class GoogleSheetLoader
    {
        private const string UrlFormat =
            "https://docs.google.com/spreadsheets/d/{0}/export?format=tsv&gid={1}";

        public static string BuildUrl(string spreadsheetId, string gid)
        {
            return string.Format(UrlFormat, spreadsheetId, gid);
        }
    }
}
