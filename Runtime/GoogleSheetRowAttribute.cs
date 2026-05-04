using System;

namespace ChopChopGames.UGM.GoogleSheetTable
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GoogleSheetRowAttribute : Attribute
    {
        public string TableName { get; }

        public GoogleSheetRowAttribute() { }
        public GoogleSheetRowAttribute(string tableName) { TableName = tableName; }
    }
}
