using ParseHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Metadata.Edm;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SheduleParcer
{
    class Program
    {
        //=================================================
        static void Main(string[] args)
        {
            // !!!THREADWATCHER DEBUG!!!
            //ThreadWatcher thWatcher = new ThreadWatcher();
            //object obj = new object();
            //int i = 0;
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 1"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 2"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 3"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 4"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 5"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 6"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 7"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 8"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 9"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000);
            //    lock (obj)
            //    {
            //        thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 20"); } }));
            //        thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 21"); } }));

            //        i++; Console.WriteLine($"{i} завершено 10");
            //    } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 11"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 12"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 13"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 14"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 15"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000);
            //    lock (obj)
            //    {
            //        thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 22"); } }));
            //        i++; Console.WriteLine($"{i} завершено 16");
            //    } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000);
            //    lock (obj)
            //    {
            //        thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 23"); } }));

            //        i++; Console.WriteLine($"{i} завершено 17");
            //    } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 18"); } }));
            //thWatcher.Add(new Thread(() => { Thread.Sleep(10000); lock (obj) { i++; Console.WriteLine($"{i} завершено 19"); } }));

            //int oldval = 0;
            //while (true)
            //{
            //    var r = thWatcher.ActiveSessionsCount;
            //    if(r != oldval)
            //        Console.WriteLine($"{thWatcher.ActiveSessionsCount} {thWatcher.AllowSessionsCount}");
            //    oldval = r;
            //    //Thread.Sleep(500);

            //    //Console.CursorTop = 0;
            //}
            // !!!THREADWATCHER DEBUG!!!

            ParserHelper parser = ParserHelper.InitClass();

            //var res = parser.SelectTables(
            //    parser.FillTable(parser.GetLinksRecursive("https://portal.esstu.ru/bakalavriat/raspisan.htm"), NodeType.Student), 
            //    new Regex(@"ОЗБ\d{3}", RegexOptions.IgnoreCase),
            //    SearchLevel.InScheduleName).ToList();

            //var loadedTables =
            //    parser.FillTableRecurcieve("https://portal.esstu.ru/spezialitet/raspisan.htm", NodeType.Student);

            //List<Node> firstNodes = new List<Node>();

            //foreach (var loadedTable in loadedTables)
            //{
            //    foreach (var schedule in loadedTable.TablesList)
            //    {
            //        firstNodes.AddRange(schedule.LectionList);
            //    }
            //}


            //var ress = parser.ConvertToAuditorySchedule(loadedTables);

            //var ss = ress.Select(t => t.GetAsTables().ToList()).ToList();

            List<string> resultlist = new List<string>();

            if (SynchronizationContext.Current == null)
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            }

            //parser.GetLinksRecursiveAsync("https://portal.esstu.ru/spezialitet/raspisan.htm", resultlist);

            //var ressss = parser.GetLinksRecursive("https://portal.esstu.ru/spezialitet/raspisan.htm");

            ObservableCollection<Schedule> reslist = new  ObservableCollection<Schedule>();
            //reslist.CollectionChanged += (sender, e) =>
            //{
            //    MessageBox.Show("добавлена запись "+ reslist[e.NewStartingIndex].Name);
            //};

            int exccount = 0;
            Exception lsstException = null;
            parser.ExceptionEvent += e =>
            {
                exccount++;
                lsstException = e;
            };

            //var res = reslist.Union(parser.FillTableRecurcieve(new[]
            //{
            //    "https://portal.esstu.ru/spezialitet/raspisan.htm",
            //    "https://portal.esstu.ru/bakalavriat/raspisan.htm",
            //    "https://portal.esstu.ru/zo1/raspisan.htm",
            //    "https://portal.esstu.ru/zo1/raspisan.htm",
            //    "https://portal.esstu.ru/zo2/raspisan.htm"
            //},
            //    NodeType.Student)).ToList();

            //parser.FillTableRecurcieveAsync(new[]
            //    {
            //        "https://portal.esstu.ru/spezialitet/raspisan.htm",
            //        "https://portal.esstu.ru/bakalavriat/raspisan.htm",
            //        "https://portal.esstu.ru/zo1/raspisan.htm",
            //        "https://portal.esstu.ru/zo1/raspisan.htm",
            //        "https://portal.esstu.ru/zo2/raspisan.htm"
            //    },
            //    NodeType.Student, reslist, SynchronizationContext.Current, true);
            //List<string> ss = new List<string>();
           // parser.GetLinksRecursiveAsync("https://portal.esstu.ru/menu.htm",ss,true);
           List<string> sites = new List<string>();
            //parser.GetLinksRecursiveAsync("https://portal.esstu.ru/menu.htm", sites, SynchronizationContext.Current, true);
            // sites.AddRange(parser.GetLinksRecursive("https://portal.esstu.ru/menu.htm"));
            //parser.FillTableRecurcieve(new[]
            //    {

            //        "https://portal.esstu.ru/spezialitet/raspisan.htm",
            //        "https://portal.esstu.ru/bakalavriat/raspisan.htm",
            //        "https://portal.esstu.ru/zo1/raspisan.htm",
            //        "https://portal.esstu.ru/zo1/raspisan.htm",
            //        "https://portal.esstu.ru/zo2/raspisan.htm"
            //    },
            //    NodeType.Student);

            //WebRequest request = WebRequest.Create("https://portal.esstu.ru/menu.htm");
            //string somevalue = String.Empty;

            //var f=  request.BeginGetResponse(call =>
            //{
            //    using (var stream = new StreamReader((call.AsyncState as WebRequest)?.EndGetResponse(call).GetResponseStream() ?? throw new InvalidOperationException()))
            //    {
            //        somevalue = stream.ReadToEnd();
            //    }


            //}, request);

            //HttpClient client = new HttpClient();
            //client.GetStringAsync()

            //var res = await parser.GetLinksRecursiveAsyncTask("https://portal.esstu.ru/menu.htm");

            //var res = await parser.FillTableRecurcieveAsyncTask("https://portal.esstu.ru/menu.htm", NodeType.Student); //1623
            //var res1 = parser.GetLinksRecursive("https://portal.esstu.ru/menu.htm");

            //parser.FillTableRecurcieveAsync("https://portal.esstu.ru/menu.htm",NodeType.Student, reslist, SynchronizationContext.Current, true); //1623 //113

            //var res1 = parser.FillTableRecurcieveAsyncAndWait("https://portal.esstu.ru/bakalavriat/9.htm", NodeType.Student);
            var res1 = new List<Schedule>();
             //var res1 = parser.FillTableRecurcieveAsyncAndWait(new[]
             //    {

             //        "https://portal.esstu.ru/spezialitet/raspisan.htm",
             //        "https://portal.esstu.ru/bakalavriat/raspisan.htm",
             //    }, NodeType.Student);

             //foreach (var schedule in res1)
             //{
             //    foreach (var scheduleTable in schedule.TablesList)
             //    {
             //        //foreach (var node in scheduleTable.LectionList)
             //        {
             //            var test = scheduleTable.LectionList.ToString('\n');
             //        }
             //    }
             //}

             var xheck = res1.Select(t=>t.Name).Distinct().ToList();
            xheck.Sort();

            var ffgf = parser.RemoveRepeats(res1).Select(t => t.Name).ToList();
            ffgf.Sort();


            var filter = GroupOfSchedule.DefaultFilters["студенты"]["Бакалавриат"];

            GroupOfSchedule mainGroupContainer = new GroupOfSchedule();

            mainGroupContainer.LoadSchedules(new []
            {
                new GroupOfSchedule.Source("https://portal.esstu.ru/spezialitet/raspisan.htm", NodeType.Student), 
                new GroupOfSchedule.Source("https://portal.esstu.ru/bakalavriat/raspisan.htm", NodeType.Student)
            });

            mainGroupContainer.LoadAsAuditoriums(mainGroupContainer.Schedules);

            mainGroupContainer.LoadSchedules(new[]
            {
                new GroupOfSchedule.Source("https://portal.esstu.ru/spezialitet/craspisanEdt.htm", NodeType.Teacher),
                new GroupOfSchedule.Source("https://portal.esstu.ru/bakalavriat/craspisanEdt.htm", NodeType.Teacher)
            });

            mainGroupContainer.PutToSubgroups(GroupOfSchedule.DefaultFilters);

            //var r = string.Join("\n", mainGroupContainer.Schedules?.Select(t => t.Name).Where(t=>!t.ToLower().Contains("фкс")&& !t.ToLower().Contains("вакансия")) ?? new List<string>()); 
            //var r1 = mainGroupContainer["студенты"]["бакалавриат"];
            parser.ExcelExport(mainGroupContainer.Schedules,Directory.GetCurrentDirectory()+"\\ExcelTables");

            if (exccount!=0) MessageBox.Show(lsstException?.Message, "Возникла ошибка загрузки ("+ exccount +")", MessageBoxButtons.OK);
            //do
            //{
            //    Console.ReadKey();
            //} while (true);
        }
    }
}
