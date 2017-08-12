using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Server
{
    public static class ExcelUtility
    {
        public static void CreateExcel(string destination, IEnumerable<DataTable> dataTables, string[] sheetNames = null)
        {
            using (var workbook = SpreadsheetDocument.Create(destination, SpreadsheetDocumentType.Workbook))
            {
                workbook.AddWorkbookPart();
                workbook.WorkbookPart.Workbook = new Workbook
                {
                    Sheets = new Sheets()
                };
                uint sheetId = 1;
                var isAddStyle = false;
                foreach (var table in dataTables)
                {
                    var sheetPart = workbook.WorkbookPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    sheetPart.Worksheet = new Worksheet();
                    if (!isAddStyle)
                    {
                        var stylesPart = workbook.WorkbookPart.AddNewPart<WorkbookStylesPart>();
                        var styles = new Stylesheet();
                        styles.Save(stylesPart);
                        isAddStyle = true;
                    }
                    var headColumns = CrateColunms(table);
                    sheetPart.Worksheet.Append(headColumns);
                    var sheets = workbook.WorkbookPart.Workbook.GetFirstChild<Sheets>();
                    var relationshipId = workbook.WorkbookPart.GetIdOfPart(sheetPart);

                    if (sheets.Elements<Sheet>().Any())
                    {
                        sheetId =
                            sheets.Elements<Sheet>().Select(s => s.SheetId.Value).Max() + 1;
                    }
                    var sheetName = string.Empty;
                    if (sheetNames != null)
                    {
                        if (sheetNames.Length >= sheetId)
                        {
                            sheetName = sheetNames[sheetId - 1];
                        }
                    }
                    else
                    {
                        sheetName = table.TableName ?? sheetId.ToString();
                    }

                    var sheet = new Sheet { Id = relationshipId, SheetId = sheetId, Name = sheetName };
                    sheets.Append(sheet);

                    var headerRow = new Row();

                    var columns = new List<string>();
                    foreach (DataColumn column in table.Columns)
                    {
                        columns.Add(column.ColumnName);

                        var cell = new Cell
                        {
                            DataType = CellValues.String,
                            StyleIndex = 11,
                            CellValue = new CellValue(column.ColumnName)
                        };
                        headerRow.AppendChild(cell);
                    }
                    sheetData.AppendChild(headerRow);

                    foreach (DataRow dsrow in table.Rows)
                    {
                        var newRow = new Row();
                        foreach (var col in columns)
                        {
                            var cell = new Cell
                            {
                                DataType = CellValues.String,
                                StyleIndex = 10,
                                CellValue = new CellValue(dsrow[col].ToString())
                            };
                            newRow.AppendChild(cell);
                        }
                        sheetData.AppendChild(newRow);
                    }
                    sheetPart.Worksheet.Append(sheetData);
                }
                workbook.Close();
            }
        }

        private static Columns CrateColunms(DataTable table)
        {
            var columns = new Columns();
            for (var col = 0; col < table.Columns.Count; col++)
            {
                var maxWidth = table.Columns[col].ColumnName.Length;
                for (var row = 0; row < table.Rows.Count; row++)
                {
                    var valueWidth = table.Rows[row][col].ToString().Trim().Length;
                    if (maxWidth < valueWidth)
                    {
                        maxWidth = valueWidth;
                    }
                }
                var c = new Column();
                columns.Append(c);
            }
            return columns;
        }
    }
}
