using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Office.Core;
using Excel = Microsoft.Office.Interop.Excel;

namespace ParseHelper
{
    public static class Extender
    {
        public static string ToString(this List<Node> lectionList, char separator)
        {
            string result = string.Empty;
            
            for (var i = 0; i < lectionList.Count-1; i++)
                result += lectionList[i].ToString() + separator;

            result += lectionList[lectionList.Count - 1].ToString();

            

            return string.Join(separator.ToString(), result.Split(new[] { '\n', separator }, StringSplitOptions.RemoveEmptyEntries).Distinct());
        }
    }
    public partial class ParserHelper
    {
        public async Task<IEnumerable<string>> GetLinksRecursiveAsyncTask(string website)
        {
            List<string> result = new List<string>();

                string mask = WebLinkMask.Match(website).Groups["mask"].Value + '/';

                string mainData;

                try
                {
                    mainData = await HClient.GetStringAsync(website);
                }
                catch (Exception e)
                {
                    ExceptionEvent?.Invoke(e);
                    return new List<string>();
                }

                var filteredMatches = WebLinkAddress.Matches(mainData).Cast<Match>()
                    .Where(t => !t.Groups["address"].Value.Contains("http") && t.Groups["linkname"].Value
                                    .Any(c => char.IsDigit(c) || char.IsLetter(c)))
                    .Select(t => t.Groups["address"].Value)
                    .Distinct()
                    .ToList();

                if (!filteredMatches.Any())
                {
                    filteredMatches = WebLinkAddressShort.Matches(mainData).Cast<Match>()
                        .Where(t => !t.Groups["address"].Value.Contains("http"))
                        .Select(t => t.Groups["address"].Value)
                        .Distinct()
                        .ToList();
                }

                if (filteredMatches.Any())
                {
                    List<Task<IEnumerable<string>>> threads = new List<Task<IEnumerable<string>>>();
                    foreach (var match in filteredMatches)
                    {
                        threads.Add(GetLinksRecursiveAsyncTask(mask + match));
                    }

                    var tempResults = await Task.WhenAll(threads.ToArray());

                    foreach (var tempResult in tempResults)
                        result.AddRange(tempResult);

                    return result;
                }

                result.Add(website);
                return result;


                ////////////////////////////
                //filteredMatches.Clear();

        }

        public async Task<IEnumerable<Schedule>> FillTableRecurcieveAsyncTask(string website, NodeType nType)
        {
            var webLinks = await GetLinksRecursiveAsyncTask(website);
            List<Task<IEnumerable<Schedule>>> threads = new List<Task<IEnumerable<Schedule>>>();
            foreach (var webLink in webLinks)
            {
                threads.Add(Task.Factory.StartNew(async () =>
                {
                    string source;
                    try
                    {
                        source = Encoding.Default.GetString(await HClient.GetByteArrayAsync(webLink));
                    }
                    catch (Exception e)
                    {
                        ExceptionEvent?.Invoke(e);
                        return new List<Schedule>();
                    }
                    return FillTable(nType, source);
                }).Unwrap());
            }

            List<Schedule> result = new List<Schedule>();

            var tempResults = await Task.WhenAll(threads.ToArray());

            foreach (var tempResult in tempResults)
                result.AddRange(tempResult);

            return result;
        }
        /// <summary>
        ///  Для упрощенной загрузки таблиц расписания рекомендуется использовать данный метод (самый жесткий и быстрый способ)
        /// </summary>
        /// <param name="webLink">
        /// ссылка с сайта portal.ESSTU.ru
        /// </param>
        /// <param name="nType">
        /// тип в который будет конвертированы записи таблиц
        /// </param>
        /// <returns>
        /// возвращает список классов расписаний
        /// </returns>
        public IEnumerable<Schedule> FillTableRecurcieveAsyncAndWait(string webLink, NodeType nType)
        {
            List<Schedule> result = new List<Schedule>();
            FillTableRecurcieveAsync(webLink, nType, result, SynchronizationContext.Current, true);
            return result;
        }
        /// <summary>
        /// Для упрощенной загрузки таблиц расписания рекомендуется использовать данный метод (самый жесткий и быстрый способ)
        /// </summary>
        /// <param name="webLinks">
        /// список ссылок с сайта portal.ESSTU.ru 
        /// </param>
        /// <param name="nType">
        /// тип в который будет конвертированы записи таблиц
        /// </param>
        /// <returns>
        /// возвращает список классов расписаний
        /// </returns>
        public IEnumerable<Schedule> FillTableRecurcieveAsyncAndWait(IEnumerable<string> webLinks, NodeType nType)
        {
            List<Schedule> result = new List<Schedule>();
            FillTableRecurcieveAsync(webLinks, nType, result, SynchronizationContext.Current, true);
            return result;
        }

        public void ExcelExport(IEnumerable<Schedule> savingSchedules, string path)
        {
            try
            {

                int errorBookCount = 0;
            Excel.Application excelFile = new Excel.Application();

            if(!Directory.Exists(path)) Directory.CreateDirectory(path);

            foreach (var savingSchedule in savingSchedules)
            {
                var currentBook = excelFile.Workbooks.Add();

                foreach (var scheduleTable in savingSchedule.TablesList)
                {
                    if (currentBook.Worksheets.Add() is Excel.Worksheet currentSheet)
                    {
                        var asListsTable = scheduleTable.ConvertToTable().ToList();
                        var height = asListsTable.Count;
                        var width = asListsTable.Select(t => t.Count()).Sum() / height;
                        if(!currentBook.Sheets.Cast<Excel.Worksheet>().Select(t=>t.Name).Contains(scheduleTable.SelectedWeek.ToString()))
                            currentSheet.Name = scheduleTable.SelectedWeek.ToString();
                        currentSheet.Range[currentSheet.Cells[1, 1], currentSheet.Cells[1, width+1]].Cells.Merge();
                        currentSheet.Cells[1, 1] = "Расписание: "+ savingSchedule.Name;
                        ((Excel.Range) currentSheet.Cells[1, 1]).Font.Size = 24;

                        var dayOfWeek = typeof(DayOfWeek).GetEnumNames();
                        var workingTime = typeof(WorkingTime).GetEnumNames();

                        for (int i = 0; i < workingTime.Length; i++)
                            currentSheet.Cells[3 + i, 1] = workingTime[i];

                        for (var i = 0; i < dayOfWeek.Length; i++)
                            currentSheet.Cells[2, 2 + i] = dayOfWeek[i];

                        string[,] savingTable = new string[height, width];

                        for (var i = 0; i < height; i++)
                        {
                            for (int j = 0; j < width; j++)
                                savingTable[i, j] =
                                    asListsTable.Select(t => t.Select(s => s?.ToList()).ToArray()).ToArray()[i][j]?
                                        .ToString('\n');
                        }


                        var modifyingArea = currentSheet.Range[currentSheet.Cells[3, 2], currentSheet.Cells[3 + height - 1, 2 + width - 1]];
                        
                        modifyingArea.Value = savingTable;

                        modifyingArea.CurrentRegion.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                        modifyingArea.VerticalAlignment = Excel.XlVAlign.xlVAlignTop;
                        modifyingArea.ColumnWidth = 200;
                        modifyingArea.EntireColumn.AutoFit();

                        currentSheet.UsedRange.BorderAround2(Excel.XlLineStyle.xlContinuous, Excel.XlBorderWeight.xlThick);
                    }
                }

                ((Excel.Worksheet)currentBook.Sheets[savingSchedule.TablesList.Count+1] ).Delete();

                var correctedName = savingSchedule.Name
                    .Replace('\\', ' ')
                    .Replace('/', ' ')
                    .Replace(':', ' ')
                    .Replace('*', ' ')
                    .Replace('?', ' ')
                    .Replace('"', ' ')
                    .Replace('<', ' ')
                    .Replace('>', ' ')
                    .Replace('|', ' ')
                    .Replace('.', ' ')
                    .Replace('[', ' ')
                    .Replace(']', ' ');
                    
                if (correctedName == string.Empty)
                {
                    correctedName = "(unnamed)" + errorBookCount;
                    errorBookCount++;
                }

                if (File.Exists(path + "\\" + correctedName + ".xls"))File.Delete(path + "\\" + correctedName + ".xls");
                currentBook.SaveAs(path + "\\"+ correctedName, Excel.XlFileFormat.xlExcel8);
                currentBook.Close();
            }
            excelFile.Quit();
            }
            catch (Exception e)
            {
               ExceptionEvent.Invoke(e);
            }
            //excelFile.Save(Directory.GetCurrentDirectory()+"\\test");
        }

        //public void ExcelExportAsync(IEnumerable<Schedule> savingSchedules, string path, bool isWait)
        //{
        //    Excel.Application excelFile;
        //    try
        //    {
        //        excelFile = (Excel.Application)Marshal.GetActiveObject("Excel.Application");
        //    }
        //    catch
        //    {
        //        excelFile = new Excel.Application();
        //    }

        //    if(!Directory.Exists(path)) Directory.CreateDirectory(path);

        //    ///////////////////////////////////////////////////////////
        //    List<Thread> threads = new List<Thread>();
        //    foreach (var savingSchedule in savingSchedules)
        //    {
        //        Thread local = new Thread(e =>
        //        {
        //            ExcelExport(savingSchedule, (Excel.Application)e, path);
        //        });

        //        local.Start(excelFile);
        //        threads.Add(local);
        //    }

        //    if (isWait)
        //    {
        //        foreach (var thread in threads)
        //            thread.Join();
        //        ///////////////////////////////////////////////////////////
        //        threads.Clear();
        //        foreach (var excelFileWorkbook in excelFile.Workbooks)
        //            ((Excel.Workbook) excelFileWorkbook).Close();
        //    }

        //    GC.Collect();
        //}

        [Obsolete("не используется")]
        private void ExcelExport(Schedule savingSchedule, Excel.Application excelFile, string path)
        {
            var currentBook = excelFile.Workbooks.Add();

            foreach (var scheduleTable in savingSchedule.TablesList)
            {
                if (currentBook.Worksheets.Add() is Excel.Worksheet currentSheet)
                {
                    var asListsTable = scheduleTable.ConvertToTable().ToList();
                    var height = asListsTable.Count;
                    var width = asListsTable.Select(t => t.Count()).Sum() / height;
                    if (!currentBook.Sheets.Cast<Excel.Worksheet>().Select(t => t.Name).Contains(scheduleTable.SelectedWeek.ToString()))
                        currentSheet.Name = scheduleTable.SelectedWeek.ToString();
                    currentSheet.Range[currentSheet.Cells[1, 1], currentSheet.Cells[1, width + 1]].Cells.Merge();
                    currentSheet.Cells[1, 1] = "Расписание: " + savingSchedule.Name;
                    ((Excel.Range)currentSheet.Cells[1, 1]).Font.Size = 24;

                    var dayOfWeek = typeof(DayOfWeek).GetEnumNames();
                    var workingTime = typeof(WorkingTime).GetEnumNames();

                    for (int i = 0; i < workingTime.Length; i++)
                        currentSheet.Cells[3 + i, 1] = workingTime[i];

                    for (var i = 0; i < dayOfWeek.Length; i++)
                        currentSheet.Cells[2, 2 + i] = dayOfWeek[i];

                    string[,] savingTable = new string[height, width];

                    for (var i = 0; i < height; i++)
                    {
                        for (int j = 0; j < width; j++)
                            savingTable[i, j] =
                                asListsTable.Select(t => t.Select(s => s?.ToList()).ToArray()).ToArray()[i][j]?
                                    .ToString('\n');
                    }


                    var modifyingArea = currentSheet.Range[currentSheet.Cells[3, 2], currentSheet.Cells[3 + height - 1, 2 + width - 1]];

                    modifyingArea.Value = savingTable;

                    modifyingArea.CurrentRegion.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                    modifyingArea.VerticalAlignment = Excel.XlVAlign.xlVAlignTop;
                    modifyingArea.ColumnWidth = 200;
                    modifyingArea.EntireColumn.AutoFit();

                    currentSheet.UsedRange.BorderAround2(Excel.XlLineStyle.xlContinuous, Excel.XlBorderWeight.xlThick);
                }
            }

            ((Excel.Worksheet)currentBook.Sheets[savingSchedule.TablesList.Count + 1]).Delete();

            var correctedName = savingSchedule.Name
                .Replace('\\', ' ')
                .Replace('/', ' ')
                .Replace(':', ' ')
                .Replace('*', ' ')
                .Replace('?', ' ')
                .Replace('"', ' ')
                .Replace('<', ' ')
                .Replace('>', ' ')
                .Replace('|', ' ')
                .Replace('.', ' ')
                .Replace('[', ' ')
                .Replace(']', ' ');

            if (File.Exists(path + "\\" + correctedName + ".xls")) File.Delete(path + "\\" + correctedName + ".xls");
            try
            {
                currentBook.SaveAs(path + "\\" + correctedName, Excel.XlFileFormat.xlExcel8);
            } catch (Exception e) { ExceptionEvent.Invoke(e);
                MessageBox.Show(e.Message);
            }
            
        }
    }
}
